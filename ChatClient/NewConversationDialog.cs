using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ChatClient;

public class NewConversationDialog : Window
{
    public string ConversationType { get; private set; } = "group";
    public string? GroupName { get; private set; }
    public int[] MemberIds { get; private set; } = [];
    public int? JoinId { get; private set; }

    public NewConversationDialog(IEnumerable<OnlineUserItem> users, int? currentUserId)
    {
        Title = "Tạo / tham gia hội thoại";
        Width = 430; Height = 550;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(26, 26, 36));
        Foreground = Brushes.White;
        var panel = new StackPanel { Margin = new Thickness(20) };
        Content = panel;
        var mode = new ComboBox { ItemsSource = new[] { "Tạo nhóm", "Chat 1-1", "Tham gia nhóm bằng mã" }, SelectedIndex = 0, Margin = new Thickness(0, 0, 0, 16) };
        panel.Children.Add(mode);
        var label = new TextBlock { Text = "Tên nhóm", Margin = new Thickness(0, 0, 0, 6) };
        panel.Children.Add(label);
        var name = new TextBox { MaxLength = 100, Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 12) };
        panel.Children.Add(name);
        var hint = new TextBlock { Text = "Chọn nhiều thành viên online (không bắt buộc).\nCó thể chia sẻ mã nhóm để người khác vào sau.", TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(hint);
        var members = new ListBox
        {
            ItemsSource = users.Where(u => u.UserId != currentUserId).GroupBy(u => u.UserId).Select(g => g.First()).ToList(),
            DisplayMemberPath = "DisplayName", SelectionMode = SelectionMode.Multiple,
            Height = 240, Margin = new Thickness(0, 10, 0, 16)
        };
        panel.Children.Add(members);
        mode.SelectionChanged += (_, _) =>
        {
            bool joining = mode.SelectedIndex == 2;
            label.Text = joining ? "Mã nhóm (ID)" : "Tên nhóm";
            name.IsEnabled = mode.SelectedIndex != 1;
            members.IsEnabled = !joining;
            members.SelectionMode = mode.SelectedIndex == 1 ? SelectionMode.Single : SelectionMode.Multiple;
        };
        var submit = new Button { Content = "Tiếp tục", Padding = new Thickness(10), IsDefault = true };
        panel.Children.Add(submit);
        submit.Click += (_, _) =>
        {
            if (mode.SelectedIndex == 2)
            {
                if (!int.TryParse(name.Text, out int id) || id <= 0) { MessageBox.Show(this, "Nhập mã nhóm hợp lệ."); return; }
                JoinId = id;
            }
            else
            {
                ConversationType = mode.SelectedIndex == 0 ? "group" : "direct";
                GroupName = name.Text.Trim();
                MemberIds = members.SelectedItems.Cast<OnlineUserItem>().Select(u => u.UserId).ToArray();
                if (ConversationType == "group" && string.IsNullOrWhiteSpace(GroupName)) { MessageBox.Show(this, "Nhập tên nhóm."); return; }
                if (ConversationType == "direct" && MemberIds.Length != 1) { MessageBox.Show(this, "Chọn một người để chat."); return; }
            }
            DialogResult = true;
        };
    }
}
