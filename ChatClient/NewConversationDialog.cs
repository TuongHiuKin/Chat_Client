using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ChatClient
{
    /// <summary>
    /// Dialog đơn giản để chọn người dùng và tạo cuộc hội thoại 1-1 mới.
    /// Hiển thị danh sách người dùng đang online (trừ bản thân).
    /// </summary>
    public class NewConversationDialog : Window
    {
        /// <summary>UserId của người dùng được chọn để tạo hội thoại.</summary>
        public int? SelectedUserId { get; private set; }

        private readonly ListBox _listBox;

        public NewConversationDialog(
            IEnumerable<OnlineUserItem> onlineUsers,
            int? currentUserId)
        {
            Title = "Tạo cuộc hội thoại mới";
            Width = 320;
            Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = System.Windows.Media.Brushes.Transparent;

            var grid = new Grid { Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1A1A24")) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header
            var header = new TextBlock
            {
                Text = "Chọn người dùng để chat",
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(16, 16, 16, 12),
            };
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            // Danh sách người dùng online
            _listBox = new ListBox
            {
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 13,
                Margin = new Thickness(8, 0, 8, 0),
            };

            foreach (var user in onlineUsers)
            {
                // Không hiển thị bản thân trong danh sách
                if (user.UserId == currentUserId) continue;
                _listBox.Items.Add(new ListBoxItem
                {
                    Content = user.DisplayName,
                    Tag = user.UserId,
                    Padding = new Thickness(12, 8, 12, 8),
                    Foreground = System.Windows.Media.Brushes.White,
                    Background = System.Windows.Media.Brushes.Transparent,
                });
            }

            Grid.SetRow(_listBox, 1);
            grid.Children.Add(_listBox);

            // Nút hành động
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(16),
            };

            var btnCancel = new Button
            {
                Content = "Huỷ",
                Width = 80,
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            btnCancel.Click += (_, _) =>
            {
                DialogResult = false;
                Close();
            };

            var btnOk = new Button
            {
                Content = "Tạo",
                Width = 80,
                Height = 32,
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            btnOk.Click += (_, _) =>
            {
                if (_listBox.SelectedItem is ListBoxItem item && item.Tag is int uid)
                {
                    SelectedUserId = uid;
                    DialogResult = true;
                }
                else
                {
                    MessageBox.Show("Vui lòng chọn một người dùng.", "Thông báo",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            btnPanel.Children.Add(btnCancel);
            btnPanel.Children.Add(btnOk);
            Grid.SetRow(btnPanel, 2);
            grid.Children.Add(btnPanel);

            Content = grid;
        }
    }
}
