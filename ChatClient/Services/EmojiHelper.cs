using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ChatClient
{
    /// <summary>
    /// Tiện ích hỗ trợ hiển thị Emoji màu sắc độ nét cao (Color Emoji)
    /// trong TextBlock của bong bóng chat WPF thay cho nét đen trắng mặc định.
    /// </summary>
    public static class EmojiHelper
    {
        public static readonly DependencyProperty FormattedTextProperty =
            DependencyProperty.RegisterAttached(
                "FormattedText",
                typeof(string),
                typeof(EmojiHelper),
                new PropertyMetadata(null, OnFormattedTextChanged));

        public static string GetFormattedText(DependencyObject obj) =>
            (string)obj.GetValue(FormattedTextProperty);

        public static void SetFormattedText(DependencyObject obj, string value) =>
            obj.SetValue(FormattedTextProperty, value);

        private static readonly Dictionary<string, BitmapImage> _cache = new();

        public static BitmapImage? GetEmojiBitmap(string codePoint)
        {
            if (_cache.TryGetValue(codePoint, out var cached))
                return cached;

            string[] searchPaths =
            {
                Path.Combine(AppContext.BaseDirectory, "Assets", "Emojis", $"{codePoint}.png"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Emojis", $"{codePoint}.png"),
                Path.Combine(@"e:\PRN222\ChatSystem\ChatClient\Assets\Emojis", $"{codePoint}.png"),
            };

            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.UriSource = new Uri(path, UriKind.Absolute);
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.EndInit();
                        bi.Freeze();
                        _cache[codePoint] = bi;
                        return bi;
                    }
                    catch { }
                }
            }

            return null;
        }

        private static void OnFormattedTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBlock textBlock) return;
            textBlock.Inlines.Clear();

            string? text = e.NewValue as string;
            if (string.IsNullOrEmpty(text)) return;

            RenderEmojiText(textBlock, text);
        }

        public static void RenderEmojiText(TextBlock textBlock, string text)
        {
            // Kiểm tra xem tin nhắn có phải chỉ gồm 1-3 emoji không
            bool isOnlyEmojis = CheckIfOnlyEmojis(text, out int emojiCount);
            double emojiSize = (isOnlyEmojis && emojiCount <= 3) ? 36.0 : 20.0;

            var buffer = new StringBuilder();

            for (int i = 0; i < text.Length;)
            {
                int cp = char.ConvertToUtf32(text, i);
                int charLen = char.IsSurrogatePair(text, i) ? 2 : 1;

                if (IsEmojiCodePoint(cp))
                {
                    // Bỏ qua ký tự biến thể fe0f nếu có ngay sau
                    int totalLen = charLen;
                    if (i + charLen < text.Length)
                    {
                        int nextCp = char.ConvertToUtf32(text, i + charLen);
                        if (nextCp == 0xfe0f)
                        {
                            totalLen += char.IsSurrogatePair(text, i + charLen) ? 2 : 1;
                        }
                    }

                    string hexCode = cp.ToString("x");
                    var bmp = GetEmojiBitmap(hexCode);

                    if (bmp != null)
                    {
                        if (buffer.Length > 0)
                        {
                            textBlock.Inlines.Add(new Run(buffer.ToString()));
                            buffer.Clear();
                        }

                        var img = new Image
                        {
                            Source = bmp,
                            Width = emojiSize,
                            Height = emojiSize,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(isOnlyEmojis ? 3 : 1, 0, isOnlyEmojis ? 3 : 1, 0),
                            Stretch = Stretch.Uniform,
                        };

                        textBlock.Inlines.Add(new InlineUIContainer(img));
                        i += totalLen;
                        continue;
                    }
                }

                buffer.Append(text.Substring(i, charLen));
                i += charLen;
            }

            if (buffer.Length > 0)
            {
                textBlock.Inlines.Add(new Run(buffer.ToString()));
            }
        }

        private static bool IsEmojiCodePoint(int cp)
        {
            return (cp >= 0x1F300 && cp <= 0x1FAFF)
                || (cp >= 0x2600 && cp <= 0x27BF)
                || (cp >= 0x2B50 && cp <= 0x2B55)
                || (cp >= 0x231A && cp <= 0x23F3)
                || cp == 0x2764
                || cp == 0x2708
                || cp == 0x270C
                || cp == 0x270D
                || cp == 0x2600
                || cp == 0x2615
                || cp == 0x26A1
                || cp == 0x26BD
                || cp == 0x26BE;
        }

        private static bool CheckIfOnlyEmojis(string text, out int count)
        {
            count = 0;
            for (int i = 0; i < text.Length;)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                int cp = char.ConvertToUtf32(text, i);
                int charLen = char.IsSurrogatePair(text, i) ? 2 : 1;

                if (IsEmojiCodePoint(cp))
                {
                    count++;
                    i += charLen;
                    if (i < text.Length && char.ConvertToUtf32(text, i) == 0xfe0f)
                    {
                        i += char.IsSurrogatePair(text, i) ? 2 : 1;
                    }
                }
                else
                {
                    count = 0;
                    return false;
                }
            }

            return count > 0;
        }
    }
}
