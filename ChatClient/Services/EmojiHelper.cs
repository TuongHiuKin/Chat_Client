using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ChatClient;

public static class EmojiHelper
{
    public static readonly DependencyProperty FormattedTextProperty = DependencyProperty.RegisterAttached(
        "FormattedText", typeof(string), typeof(EmojiHelper), new PropertyMetadata(null, OnFormattedTextChanged));
    public static string GetFormattedText(DependencyObject obj) => (string)obj.GetValue(FormattedTextProperty);
    public static void SetFormattedText(DependencyObject obj, string value) => obj.SetValue(FormattedTextProperty, value);
    private static readonly ConcurrentDictionary<string, BitmapImage> Cache = new();

    public static BitmapImage? GetEmojiBitmap(string codePoint)
    {
        if (Cache.TryGetValue(codePoint, out var cached)) return cached;
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "Emojis", codePoint + ".png");
        if (!File.Exists(path)) return null;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path); bitmap.EndInit(); bitmap.Freeze();
            Cache[codePoint] = bitmap;
            return bitmap;
        }
        catch { return null; }
    }
    private static void OnFormattedTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBlock block) RenderEmojiText(block, e.NewValue as string ?? "");
    }
    public static void RenderEmojiText(TextBlock block, string text)
    {
        block.Inlines.Clear();
        var elements = new List<(string Text, BitmapImage? Image)>();
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            string element = enumerator.GetTextElement();
            string code = string.Join("-", element.EnumerateRunes().Where(r => r.Value != 0xfe0f).Select(r => r.Value.ToString("x")));
            elements.Add((element, GetEmojiBitmap(code)));
        }
        // Treat ZWJ / skin-tone sequences as a whole; unsupported sequences remain intact.
        bool onlyEmoji = elements.Count(e => e.Image != null) is >= 1 and <= 3
            && elements.All(e => e.Image != null || string.IsNullOrWhiteSpace(e.Text));
        double size = onlyEmoji ? 36 : 20;
        var buffer = new StringBuilder();
        foreach (var element in elements)
        {
            if (element.Image == null) { buffer.Append(element.Text); continue; }
            if (buffer.Length > 0) { block.Inlines.Add(new Run(buffer.ToString())); buffer.Clear(); }
            block.Inlines.Add(new InlineUIContainer(new Image
            {
                Source = element.Image, Width = size, Height = size, Stretch = Stretch.Uniform,
                Margin = new Thickness(onlyEmoji ? 3 : 1, 0, onlyEmoji ? 3 : 1, 0), ToolTip = element.Text
            }) { BaselineAlignment = BaselineAlignment.Center });
        }
        if (buffer.Length > 0) block.Inlines.Add(new Run(buffer.ToString()));
    }
}
