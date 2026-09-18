using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ChatClient.Models;
using ChatClient.Services;
using Microsoft.Win32;

namespace ChatClient;

public partial class MainWindow
{
    private readonly SemaphoreSlim _previewSlots = new(3, 3);

    private async Task LoadPreviewAsync(ChatMessage message)
    {
        if (!message.IsImageMessage || message.ImageSource != null || !message.AttachmentId.HasValue) return;
        await _previewSlots.WaitAsync();
        try
        {
            var bytes = await _client.GetPreviewAsync(message.AttachmentId.Value);
            message.ImageSource = await Task.Run(() => ChatMessage.LoadBitmapFromBytes(bytes));
        }
        catch { /* Download card remains available if an old/corrupt image has no preview. */ }
        finally { _previewSlots.Release(); }
    }

    private static byte[] CreateThumbnail(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        var frame = decoder.Frames[0];
        if ((long)frame.PixelWidth * frame.PixelHeight > 100_000_000) throw new InvalidOperationException("Ảnh quá lớn để xem trước. Hãy gửi dưới dạng tập tin.");
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path);
        if (frame.PixelWidth >= frame.PixelHeight) bitmap.DecodePixelWidth = Math.Min(640, frame.PixelWidth);
        else bitmap.DecodePixelHeight = Math.Min(640, frame.PixelHeight);
        bitmap.EndInit();
        bitmap.Freeze();
        var encoder = new JpegBitmapEncoder { QualityLevel = 75 };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = new MemoryStream();
        encoder.Save(output);
        if (output.Length > 256 * 1024) throw new InvalidOperationException("Ảnh xem trước quá lớn.");
        return output.ToArray();
    }

    private async Task PickAndSendAsync(bool image)
    {
        if (!_currentConversationId.HasValue) return;
        int conversationId = _currentConversationId.Value;
        var picker = new OpenFileDialog
        {
            Title = image ? "Chọn ảnh" : "Chọn tập tin (tối đa 500 MB)",
            Filter = image ? "Hình ảnh|*.png;*.jpg;*.jpeg;*.gif;*.bmp" : "Tất cả tập tin|*.*"
        };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            if (new FileInfo(picker.FileName).Length > ChatClientService.MaxFileSize)
                throw new InvalidOperationException("Kích thước tối đa là 500 MB.");
            byte[]? thumbnail = null;
            if (image)
            {
                thumbnail = await Task.Run(() => CreateThumbnail(picker.FileName));
                var panel = new DockPanel { Margin = new Thickness(12) };
                var send = new Button { Content = "Gửi ảnh", Padding = new Thickness(12), IsDefault = true };
                DockPanel.SetDock(send, Dock.Bottom);
                panel.Children.Add(send);
                panel.Children.Add(new Image { Source = ChatMessage.LoadBitmapFromBytes(thumbnail), Stretch = Stretch.Uniform });
                var preview = new Window { Owner = this, Title = "Xem trước: " + Path.GetFileName(picker.FileName), Width = 520, Height = 440,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel };
                send.Click += (_, _) => preview.DialogResult = true;
                if (preview.ShowDialog() != true) return;
            }
            await RunTransferAsync("Gửi: " + Path.GetFileName(picker.FileName),
                (progress, ct) => _client.SendFileAsync(conversationId, picker.FileName, image ? "image" : "file", thumbnail, progress, ct));
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private async Task SaveAttachmentAsync(ChatMessage message)
    {
        var picker = new SaveFileDialog { FileName = message.FileName ?? "download", Filter = "Tất cả tập tin|*.*" };
        if (picker.ShowDialog(this) != true) return;
        if (message.AttachmentId.HasValue)
            await RunTransferAsync("Tải: " + message.FileName,
                (progress, ct) => _client.DownloadFileAsync(message.AttachmentId.Value, picker.FileName, progress, ct));
        else if (message.RawFileData != null)
        {
            try { await File.WriteAllBytesAsync(picker.FileName, message.RawFileData); }
            catch (Exception ex) { ShowError(ex.Message); }
        }
    }

    private async Task RunTransferAsync(string title, Func<IProgress<double>, CancellationToken, Task> action)
    {
        using var cts = new CancellationTokenSource();
        var panel = new StackPanel { Margin = new Thickness(18) };
        var label = new TextBlock { Text = "Đang xử lý...", Margin = new Thickness(0, 0, 0, 10) };
        var bar = new ProgressBar { Minimum = 0, Maximum = 100, Height = 20 };
        var cancel = new Button { Content = "Hủy", Margin = new Thickness(0, 12, 0, 0), Padding = new Thickness(8) };
        panel.Children.Add(label); panel.Children.Add(bar); panel.Children.Add(cancel);
        var window = new Window { Owner = this, Title = title, Width = 410, Height = 190, Content = panel,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize };
        bool finished = false;
        window.Closing += (_, args) => { if (!finished) { cts.Cancel(); args.Cancel = true; label.Text = "Đang hủy..."; } };
        cancel.Click += (_, _) => { if (finished) window.Close(); else { cts.Cancel(); cancel.IsEnabled = false; } };
        double last = -1;
        var progress = new Progress<double>(value =>
        {
            if (finished || Math.Floor(value) == last) return;
            last = Math.Floor(value); bar.Value = value; label.Text = $"{value:F0}%";
        });
        window.Show();
        try { await action(progress, cts.Token); label.Text = "Hoàn tất — đã kiểm tra SHA-256."; bar.Value = 100; }
        catch (OperationCanceledException) { label.Text = "Đã hủy truyền tập tin."; }
        catch (Exception ex) { label.Text = "Lỗi: " + ex.Message; label.TextWrapping = TextWrapping.Wrap; }
        finally { finished = true; cancel.IsEnabled = true; cancel.Content = "Đóng"; }
    }
}
