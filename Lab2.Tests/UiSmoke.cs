using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ChatClient;
using ChatClient.Models;

static class UiSmoke
{
    public static void Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                var block = new TextBlock();
                EmojiHelper.RenderEmojiText(block, "Xin chào 😀 ❤️ 🎉");
                if (block.Inlines.OfType<InlineUIContainer>().Count() != 3) throw new Exception("Expected 3 color emoji images.");
                EmojiHelper.RenderEmojiText(block, "👨‍👩‍👧‍👦 👍🏽"); // Preserve unsupported grapheme sequences.
                var window = new MainWindow();
                ((FrameworkElement)window.FindName("AuthPanel")).Visibility = Visibility.Collapsed;
                ((FrameworkElement)window.FindName("ChatPanel")).Visibility = Visibility.Visible;
                ((TextBlock)window.FindName("TxtCurrentUser")).Text = "Minh";
                ((TextBlock)window.FindName("TxtConversationTitle")).Text = "#12 - Nhóm Lab2";
                ((TextBlock)window.FindName("TxtConversationType")).Text = "👥 Nhóm • 3 thành viên";
                var conversations = (ObservableCollection<ConversationItem>)typeof(MainWindow).GetField("_conversations", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
                conversations.Add(new() { ConversationId = 12, Name = "#12 - Nhóm Lab2", Type = "group", TypeLabel = "3 thành viên" });
                var messages = (ObservableCollection<ChatMessage>)typeof(MainWindow).GetField("_messages", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
                messages.Add(new() { SenderName = "Lan", Content = "Xin chào nhóm! 😀 ❤️ 🎉", IsOutgoing = false });
                messages.Add(new() { SenderName = "Minh", Content = "😀 😎", IsOutgoing = true });
                messages.Add(new() { SenderName = "Lan", MessageType = "image", ImageSource = EmojiHelper.GetEmojiBitmap("1f600"), FileName = "anh-demo.png", FileSize = 10240 });
                messages.Add(new() { SenderName = "Minh", MessageType = "file", FileName = "demo-500MB.bin", FileSize = 500L * 1024 * 1024, IsOutgoing = true });
                var root = (FrameworkElement)window.Content;
                root.Width = 1050; root.Height = 700;
                root.Measure(new Size(1050, 700)); root.Arrange(new Rect(0, 0, 1050, 700)); root.UpdateLayout();
                var bitmap = new RenderTargetBitmap(1050, 700, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(root);
                string output = Path.GetFullPath(Path.Combine(".lab2-tests", "ui-smoke.png"));
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                using (var stream = File.Create(output))
                {
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(stream);
                }
                var dialog = new NewConversationDialog(new[] { new OnlineUserItem { UserId = 2, DisplayName = "Lan" }, new OnlineUserItem { UserId = 3, DisplayName = "An" } }, 1);
                dialog.Close(); window.Close();
                Console.WriteLine("PASS: WPF templates, color emoji inlines, group dialog, image and file cards");
                Console.WriteLine(output);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure != null) throw new Exception("WPF smoke failed", failure);
    }
}
