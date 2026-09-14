using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ChatClient.Models;
using ChatClient.Services;

namespace ChatClient
{
    /// <summary>
    /// Code-behind cho MainWindow.
    /// Chịu trách nhiệm:
    ///   - Khởi tạo ChatClientService và đăng ký các sự kiện mạng.
    ///   - Chuyển đổi giữa giao diện Auth (đăng nhập/đăng ký) và giao diện Chat.
    ///   - Cập nhật danh sách tin nhắn, hội thoại, người dùng online an toàn với Dispatcher.
    ///   - Xử lý input của người dùng: kết nối, đăng nhập, đăng ký, gửi tin nhắn.
    /// </summary>
    public partial class MainWindow : Window
    {
        // ── Service & Dữ liệu ────────────────────────────────────────────────
        private readonly ChatClientService _client;

        /// <summary>Danh sách tin nhắn hiển thị trong khung chat hiện tại.</summary>
        private readonly ObservableCollection<ChatMessage> _messages = new();

        /// <summary>Danh sách cuộc hội thoại hiển thị trong sidebar.</summary>
        private readonly ObservableCollection<ConversationItem> _conversations = new();

        /// <summary>Danh sách người dùng online hiển thị trong sidebar.</summary>
        private readonly ObservableCollection<OnlineUserItem> _onlineUsers = new();

        // ── Trạng thái UI ────────────────────────────────────────────────────
        private bool _isLoginMode = true;       // True = đang ở tab Login, False = Register
        private int? _currentConversationId;    // ID hội thoại đang xem
        private int _historyOffset;             // Offset phân trang lịch sử tin nhắn

        // ── Khởi tạo ─────────────────────────────────────────────────────────

        public MainWindow()
        {
            InitializeComponent();

            // Khởi tạo ChatClientService và đăng ký sự kiện mạng
            _client = new ChatClientService();
            RegisterClientEvents();

            // Gán nguồn dữ liệu cho các control
            LstMessages.ItemsSource = _messages;
            LstConversations.ItemsSource = _conversations;
            LstOnlineUsers.ItemsSource = _onlineUsers;

            // Khởi tạo bảng chọn Emoji
            InitEmojiPicker();
        }

        // ══════════════════════════════════════════════════════════════════════
        // ĐĂNG KÝ SỰ KIỆN MẠNG
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Đăng ký tất cả sự kiện từ ChatClientService.
        /// Mọi cập nhật UI đều phải qua Dispatcher.Invoke vì sự kiện đến từ background thread.
        /// </summary>
        private void RegisterClientEvents()
        {
            // Kết nối thành công → ẩn loading
            _client.OnConnected += () =>
                Dispatcher.Invoke(() => HideLoading());

            // Mất kết nối → chỉ xử lý khi đang ở Chat Panel (user đã login)
            // Nếu ở Auth Panel, bỏ qua tránh ghi đè thông báo lỗi xac thực
            _client.OnDisconnected += reason =>
                Dispatcher.Invoke(() =>
                {
                    HideLoading();
                    BtnConnect.IsEnabled = true;
                    if (ChatPanel.Visibility == Visibility.Visible)
                    {
                        // User đang chat → báo mất kết nối và quảy về login
                        AddSystemMessage($"❌ Mất kết nối: {reason}");
                        ShowError($"Mất kết nối đến Server: {reason}");
                        SwitchToAuthPanel();
                    }
                    // Nếu đang ở Auth Panel → bỏ qua (lỗi xác thực đã hiện rồi)
                });

            // Đăng nhập / Đăng ký thành công
            _client.OnAuthSuccess += (userId, displayName) =>
                Dispatcher.Invoke(() => HandleAuthSuccess(displayName));

            // Đăng nhập / Đăng ký thất bại → ẩn loading, hiện lỗi, mở lại nút
            // Không gọi Disconnect() vì Server vẫn giữ TCP alive → user có thể thử lại ngay
            _client.OnAuthFailed += error =>
                Dispatcher.Invoke(() =>
                {
                    HideLoading();
                    ShowError(error);
                    BtnConnect.IsEnabled = true;
                });

            // Nhận tin nhắn mới (BroadcastMessage từ Server)
            _client.OnMessageReceived += message =>
                Dispatcher.Invoke(() => HandleNewMessage(message));

            // Nhận lịch sử tin nhắn (HistoryResponse)
            _client.OnHistoryReceived += message =>
                Dispatcher.Invoke(() => HandleHistoryResponse(message));

            // Nhận danh sách cuộc hội thoại (ConversationsResponse)
            _client.OnConversationsReceived += message =>
                Dispatcher.Invoke(() => HandleConversationsResponse(message));

            // Nhận danh sách người dùng online (OnlineUsersResponse)
            _client.OnOnlineUsersReceived += message =>
                Dispatcher.Invoke(() => HandleOnlineUsersResponse(message));

            // Có người dùng mới online
            _client.OnUserOnline += message =>
                Dispatcher.Invoke(() =>
                {
                    RefreshOnlineUsers();
                    AddSystemMessage($"👤 {message.SenderName} đã tham gia.");
                });

            // Có người dùng offline
            _client.OnUserOffline += message =>
                Dispatcher.Invoke(() =>
                {
                    RefreshOnlineUsers();
                    AddSystemMessage($"👤 {message.SenderName} đã rời đi.");
                });

            // Nhận lỗi từ Server - safety net: ẩn loading và mở nút nếu đang ở Auth Panel
            _client.OnError += error =>
                Dispatcher.Invoke(() =>
                {
                    HideLoading();
                    ShowError(error);
                    if (AuthPanel.Visibility == Visibility.Visible)
                        BtnConnect.IsEnabled = true;
                });
        }

        // ══════════════════════════════════════════════════════════════════════
        // XỬ LÝ AUTH PANEL (ĐĂNG NHẬP / ĐĂNG KÝ)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Chuyển sang tab Đăng Nhập.</summary>
        private void BtnTabLogin_Click(object sender, RoutedEventArgs e)
        {
            _isLoginMode = true;
            BtnTabLogin.Style = (Style)FindResource("PrimaryButton");
            BtnTabRegister.Style = (Style)FindResource("GhostButton");
            RegisterOnlyPanel.Visibility = Visibility.Collapsed;
            BtnConnect.Content = "Đăng Nhập";
            HideError();
        }

        /// <summary>Chuyển sang tab Đăng Ký.</summary>
        private void BtnTabRegister_Click(object sender, RoutedEventArgs e)
        {
            _isLoginMode = false;
            BtnTabLogin.Style = (Style)FindResource("GhostButton");
            BtnTabRegister.Style = (Style)FindResource("PrimaryButton");
            RegisterOnlyPanel.Visibility = Visibility.Visible;
            BtnConnect.Content = "Đăng Ký";
            HideError();
        }

        /// <summary>
        /// Xử lý click nút Đăng Nhập / Đăng Ký:
        ///   1. Kết nối TCP đến Server.
        ///   2. Gửi gói Login hoặc Register.
        /// </summary>
        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            HideError();

            string host = TxtHost.Text.Trim();
            string portStr = TxtPort.Text.Trim();
            string username = TxtUsername.Text.Trim();
            string password = TxtPassword.Password;
            string displayName = TxtDisplayName.Text.Trim();

            // Kiểm tra dữ liệu nhập
            if (string.IsNullOrWhiteSpace(host) ||
                !int.TryParse(portStr, out int port) || port <= 0)
            {
                ShowError("Địa chỉ Server hoặc Port không hợp lệ.");
                return;
            }

            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                ShowError("Tên đăng nhập và mật khẩu không được để trống.");
                return;
            }

            ShowLoading("Đang kết nối đến Server...");
            BtnConnect.IsEnabled = false;

            try
            {
                // Kết nối TCP đến ChatServer
                if (!_client.IsConnected)
                    await _client.ConnectAsync(host, port);

                // Gửi yêu cầu xác thực
                if (_isLoginMode)
                {
                    ShowLoading("Đang đăng nhập...");
                    await _client.LoginAsync(username, password);
                }
                else
                {
                    ShowLoading("Đang đăng ký...");
                    await _client.RegisterAsync(
                        username,
                        password,
                        string.IsNullOrWhiteSpace(displayName) ? username : displayName);
                }
            }
            catch (Exception ex)
            {
                // Lỗi kết nối TCP (không phải lỗi xác thực) → hiện ngay
                HideLoading();
                ShowError($"Không thể kết nối đến Server: {ex.Message}");
                BtnConnect.IsEnabled = true;
            }
            // Lưu ý: KHÔNG re-enable nút ở finally vì phản hồi xác thực đến
            // bất đồng bộ qua event OnAuthSuccess / OnAuthFailed.
            // Hai handler đó sẽ tự re-enable nút khi nhận được phản hồi từ Server.
        }

        /// <summary>
        /// Sau khi xác thực thành công: chuyển sang giao diện Chat và tải dữ liệu ban đầu.
        /// </summary>
        private async void HandleAuthSuccess(string displayName)
        {
            HideLoading();
            HideError();
            BtnConnect.IsEnabled = true; // Khôi phục nút khi thành công

            TxtCurrentUser.Text = displayName;
            SwitchToChatPanel();

            // Tải danh sách cuộc hội thoại và người dùng online từ Server
            await _client.GetConversationsAsync();
            await _client.GetOnlineUsersAsync();
        }

        // ══════════════════════════════════════════════════════════════════════
        // XỬ LÝ CHAT PANEL
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Xử lý chọn một cuộc hội thoại từ danh sách sidebar.</summary>
        private async void LstConversations_SelectionChanged(
            object sender, SelectionChangedEventArgs e)
        {
            if (LstConversations.SelectedItem is not ConversationItem selected)
                return;

            _currentConversationId = selected.ConversationId;
            _historyOffset = 0;
            _messages.Clear();

            TxtConversationTitle.Text = selected.Name;
            TxtConversationType.Text = selected.TypeLabel;
            SetChatInputEnabled(true);
            BtnLoadMore.Visibility = Visibility.Visible;

            // Tải lịch sử tin nhắn từ Server (lưu trong DB phía Server)
            await _client.GetHistoryAsync(selected.ConversationId);
        }

        /// <summary>Xử lý nhận tin nhắn mới (BroadcastMessage) từ Server.</summary>
        private void HandleNewMessage(NetworkMessage message)
        {
            // Chỉ hiển thị nếu thuộc cuộc hội thoại đang xem
            if (message.ConversationId != _currentConversationId)
            {
                // TODO: Cập nhật badge thông báo cho cuộc hội thoại khác
                return;
            }

            string msgType = "text";
            string? fileName = null;
            long? fileSize = null;
            byte[]? rawBytes = null;
            BitmapSource? imgSource = null;

            if (message.Payload.HasValue)
            {
                var p = message.Payload.Value;
                if (p.TryGetProperty("messageType", out var mtEl))
                    msgType = mtEl.GetString() ?? "text";
                if (p.TryGetProperty("fileName", out var fnEl))
                    fileName = fnEl.GetString();
                if (p.TryGetProperty("fileSize", out var fsEl) && fsEl.TryGetInt64(out long fsz))
                    fileSize = fsz;
                if (p.TryGetProperty("dataBase64", out var db64El) && !string.IsNullOrEmpty(db64El.GetString()))
                {
                    try
                    {
                        rawBytes = Convert.FromBase64String(db64El.GetString()!);
                        if (msgType == "image" && rawBytes != null)
                        {
                            imgSource = ChatMessage.LoadBitmapFromBytes(rawBytes);
                        }
                    }
                    catch { }
                }
            }

            _messages.Add(new ChatMessage
            {
                SenderId = message.SenderId,
                SenderName = message.SenderName ?? "Unknown",
                Content = message.Content ?? string.Empty,
                Timestamp = message.Timestamp,
                IsOutgoing = message.SenderId == _client.CurrentUserId,
                MessageType = msgType,
                FileName = fileName,
                FileSize = fileSize,
                RawFileData = rawBytes,
                ImageSource = imgSource,
            });

            ScrollToBottom();
        }

        /// <summary>
        /// Xử lý lịch sử tin nhắn nhận về từ Server (HistoryResponse).
        /// Chèn tin nhắn cũ lên đầu danh sách (offset > 0) hoặc thay thế toàn bộ.
        /// </summary>
        private void HandleHistoryResponse(NetworkMessage message)
        {
            if (!message.Payload.HasValue) return;

            var payload = message.Payload.Value;
            if (!payload.TryGetProperty("messages", out var messagesEl)) return;
            if (!payload.TryGetProperty("hasMore", out var hasMoreEl)) return;

            var newMessages = new List<ChatMessage>();
            foreach (var item in messagesEl.EnumerateArray())
            {
                string senderName = item.TryGetProperty("senderName", out var sn)
                    ? (sn.GetString() ?? "Unknown") : "Unknown";
                string? content = item.TryGetProperty("content", out var c)
                    ? c.GetString() : null;
                DateTime sentAt = item.TryGetProperty("sentAt", out var sa)
                    ? (sa.TryGetDateTime(out var dt) ? dt : DateTime.UtcNow) : DateTime.UtcNow;
                int senderId = item.TryGetProperty("senderId", out var sid)
                    ? (sid.TryGetInt32(out int id) ? id : 0) : 0;
                string msgType = item.TryGetProperty("messageType", out var mt)
                    ? (mt.GetString() ?? "text") : "text";

                string? fileName = null;
                long? fileSize = null;
                byte[]? rawBytes = null;
                BitmapSource? imgSource = null;

                if (item.TryGetProperty("attachment", out var attEl) && attEl.ValueKind != JsonValueKind.Null)
                {
                    if (attEl.TryGetProperty("fileName", out var fnEl))
                        fileName = fnEl.GetString();
                    if (attEl.TryGetProperty("fileSize", out var fsEl) && fsEl.TryGetInt64(out long fsz))
                        fileSize = fsz;
                    if (attEl.TryGetProperty("dataBase64", out var db64El) && !string.IsNullOrEmpty(db64El.GetString()))
                    {
                        try
                        {
                            rawBytes = Convert.FromBase64String(db64El.GetString()!);
                            if (msgType == "image" && rawBytes != null)
                            {
                                imgSource = ChatMessage.LoadBitmapFromBytes(rawBytes);
                            }
                        }
                        catch { }
                    }
                }

                newMessages.Add(new ChatMessage
                {
                    SenderId = senderId,
                    SenderName = senderName,
                    Content = content ?? string.Empty,
                    Timestamp = sentAt,
                    IsOutgoing = senderId == _client.CurrentUserId,
                    MessageType = msgType,
                    FileName = fileName,
                    FileSize = fileSize,
                    RawFileData = rawBytes,
                    ImageSource = imgSource,
                });
            }

            // Chèn tin nhắn cũ lên đầu (khi phân trang) hoặc thêm mới vào danh sách
            if (_historyOffset == 0)
            {
                _messages.Clear();
                foreach (var msg in newMessages) _messages.Add(msg);
                ScrollToBottom();
            }
            else
            {
                // Phân trang: chèn tin cũ lên đầu
                for (int i = newMessages.Count - 1; i >= 0; i--)
                    _messages.Insert(0, newMessages[i]);
            }

            BtnLoadMore.Visibility = hasMoreEl.GetBoolean()
                ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Xử lý danh sách cuộc hội thoại từ Server (ConversationsResponse).</summary>
        private void HandleConversationsResponse(NetworkMessage message)
        {
            if (!message.Payload.HasValue) return;
            var payload = message.Payload.Value;
            if (!payload.TryGetProperty("conversations", out var convEl)) return;

            _conversations.Clear();
            foreach (var item in convEl.EnumerateArray())
            {
                int id = item.TryGetProperty("conversationId", out var cid) &&
                         cid.TryGetInt32(out int val) ? val : 0;
                string? name = item.TryGetProperty("conversationName", out var cn)
                    ? cn.GetString() : null;
                string type = item.TryGetProperty("conversationType", out var ct)
                    ? (ct.GetString() ?? "direct") : "direct";
                int memberCount = item.TryGetProperty("memberCount", out var mc) &&
                                  mc.TryGetInt32(out int mval) ? mval : 0;

                _conversations.Add(new ConversationItem
                {
                    ConversationId = id,
                    Name = name ?? $"Hội thoại #{id}",
                    Type = type,
                    TypeLabel = type == "group"
                        ? $"👥 Nhóm • {memberCount} thành viên"
                        : "💬 Chat 1-1",
                });
            }
        }

        /// <summary>Xử lý danh sách người dùng online từ Server (OnlineUsersResponse).</summary>
        private void HandleOnlineUsersResponse(NetworkMessage message)
        {
            if (!message.Payload.HasValue) return;
            var payload = message.Payload.Value;
            if (!payload.TryGetProperty("users", out var usersEl)) return;

            _onlineUsers.Clear();
            foreach (var item in usersEl.EnumerateArray())
            {
                string? displayName = item.TryGetProperty("displayName", out var dn)
                    ? dn.GetString() : null;
                int userId = item.TryGetProperty("userId", out var uid) &&
                             uid.TryGetInt32(out int val) ? val : 0;

                _onlineUsers.Add(new OnlineUserItem
                {
                    UserId = userId,
                    DisplayName = displayName ?? "Unknown",
                });
            }

            TxtOnlineCount.Text = $"{_onlineUsers.Count} người online";
        }

        // ── Gửi tin nhắn ─────────────────────────────────────────────────────

        /// <summary>Xử lý gửi tin nhắn khi nhấn nút hoặc phím Enter.</summary>
        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            await SendMessageAsync();
        }

        private async void TxtMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift))
            {
                e.Handled = true;
                await SendMessageAsync();
            }
        }

        /// <summary>
        /// Gửi tin nhắn text đến cuộc hội thoại đang mở.
        /// Hiển thị trước trên giao diện (optimistic), Server sẽ broadcast lại cho các thành viên khác.
        /// </summary>
        private async Task SendMessageAsync()
        {
            if (_currentConversationId is null) return;

            string content = TxtMessage.Text.Trim();
            if (string.IsNullOrWhiteSpace(content)) return;

            TxtMessage.Clear();
            BtnSend.IsEnabled = false;

            try
            {
                await _client.SendMessageAsync(_currentConversationId.Value, content);
            }
            catch (Exception ex)
            {
                ShowError($"Lỗi gửi tin nhắn: {ex.Message}");
                TxtMessage.Text = content; // Khôi phục nội dung nếu lỗi
            }
            finally
            {
                BtnSend.IsEnabled = _currentConversationId.HasValue;
                TxtMessage.Focus();
            }
        }

        // ── Sidebar controls ──────────────────────────────────────────────────

        /// <summary>Chuyển sidebar sang tab Hội Thoại.</summary>
        private void BtnTabConversations_Click(object sender, RoutedEventArgs e)
        {
            BtnTabConversations.Style = (Style)FindResource("PrimaryButton");
            BtnTabOnline.Style = (Style)FindResource("GhostButton");
            ConversationsPanel.Visibility = Visibility.Visible;
            OnlineUsersPanel.Visibility = Visibility.Collapsed;
        }

        /// <summary>Chuyển sidebar sang tab Người Dùng Online.</summary>
        private async void BtnTabOnline_Click(object sender, RoutedEventArgs e)
        {
            BtnTabConversations.Style = (Style)FindResource("GhostButton");
            BtnTabOnline.Style = (Style)FindResource("PrimaryButton");
            ConversationsPanel.Visibility = Visibility.Collapsed;
            OnlineUsersPanel.Visibility = Visibility.Visible;

            await _client.GetOnlineUsersAsync();
        }

        /// <summary>Tạo cuộc hội thoại mới (hiện tại: direct chat với người dùng online).</summary>
        private async void BtnNewConversation_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Mở dialog chọn thành viên và tên nhóm
            // Hiện tại: tạo cuộc hội thoại test 1-1 với bản thân (demo)
            var dialog = new NewConversationDialog(_onlineUsers, _client.CurrentUserId);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true && dialog.SelectedUserId.HasValue)
            {
                await _client.CreateConversationAsync(
                    "direct",
                    null,
                    new[] { dialog.SelectedUserId.Value });

                // Làm mới danh sách hội thoại
                await _client.GetConversationsAsync();
            }
        }

        /// <summary>Làm mới danh sách hội thoại và trạng thái online.</summary>
        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await _client.GetConversationsAsync();
            await _client.GetOnlineUsersAsync();
        }

        /// <summary>Tải thêm tin nhắn cũ (phân trang) trong cuộc hội thoại hiện tại.</summary>
        private async void BtnLoadMore_Click(object sender, RoutedEventArgs e)
        {
            if (_currentConversationId is null) return;
            _historyOffset += 50;
            await _client.GetHistoryAsync(_currentConversationId.Value, _historyOffset);
        }

        /// <summary>Đăng xuất: ngắt kết nối và quay về màn hình Auth.</summary>
        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            _client.Disconnect();
            _messages.Clear();
            _conversations.Clear();
            _onlineUsers.Clear();
            _currentConversationId = null;
            _historyOffset = 0;
            SetChatInputEnabled(false);
            TxtPassword.Clear();
            SwitchToAuthPanel();
        }

        // ── Xử lý phím tắt ở auth panel ──────────────────────────────────────

        private async void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) await Task.Run(async () =>
            {
                await Dispatcher.InvokeAsync(() =>
                    BtnConnect_Click(sender, new RoutedEventArgs()));
            });
        }

        // ── Đóng ứng dụng ────────────────────────────────────────────────────

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _client.Disconnect();
            _client.Dispose();
        }

        // ══════════════════════════════════════════════════════════════════════
        // TIỆN ÍCH UI
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Chuyển sang giao diện Chat sau khi đăng nhập thành công.</summary>
        private void SwitchToChatPanel()
        {
            AuthPanel.Visibility = Visibility.Collapsed;
            ChatPanel.Visibility = Visibility.Visible;
        }

        /// <summary>Quay về giao diện Auth (khi đăng xuất hoặc mất kết nối).</summary>
        private void SwitchToAuthPanel()
        {
            ChatPanel.Visibility = Visibility.Collapsed;
            AuthPanel.Visibility = Visibility.Visible;
        }

        /// <summary>Hiển thị overlay Loading với thông báo.</summary>
        private void ShowLoading(string message = "Đang xử lý...")
        {
            TxtLoadingMsg.Text = message;
            LoadingOverlay.Visibility = Visibility.Visible;
        }

        /// <summary>Ẩn overlay Loading.</summary>
        private void HideLoading()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        /// <summary>Hiển thị thông báo lỗi trên Auth Panel.</summary>
        private void ShowError(string message)
        {
            TxtErrorMsg.Text = message;
            ErrorBorder.Visibility = Visibility.Visible;
        }

        /// <summary>Ẩn thông báo lỗi trên Auth Panel.</summary>
        private void HideError()
        {
            ErrorBorder.Visibility = Visibility.Collapsed;
        }

        /// <summary>Thêm tin nhắn hệ thống vào cuộc hội thoại (không lưu DB).</summary>
        private void AddSystemMessage(string text)
        {
            _messages.Add(new ChatMessage
            {
                Content = text,
                IsSystemMessage = true,
                Timestamp = DateTime.UtcNow,
            });
            ScrollToBottom();
        }

        /// <summary>Cuộn danh sách tin nhắn xuống tin nhắn mới nhất.</summary>
        private void ScrollToBottom()
        {
            MessagesScroll.ScrollToBottom();
        }

        /// <summary>Làm mới danh sách người dùng online từ Server.</summary>
        private async void RefreshOnlineUsers()
        {
            await _client.GetOnlineUsersAsync();
        }

        // ══════════════════════════════════════════════════════════════════════
        // EMOJI, HÌNH ẢNH VÀ TẬP TIN ĐÍNH KÈM
        // ══════════════════════════════════════════════════════════════════════

        private void SetChatInputEnabled(bool isEnabled)
        {
            TxtMessage.IsEnabled = isEnabled;
            BtnSend.IsEnabled = isEnabled;
            BtnEmoji.IsEnabled = isEnabled;
            BtnSendImage.IsEnabled = isEnabled;
            BtnSendFile.IsEnabled = isEnabled;
        }

        private static readonly Dictionary<string, (string Title, string[] Emojis)> _emojiCategories = new()
        {
            ["smileys"] = ("Mặt cười & Cảm xúc", new[]
            {
                "😀", "😃", "😄", "😁", "😆", "😅", "🤣", "😂", "🙂", "🙃",
                "😉", "😊", "😇", "🥰", "😍", "🤩", "😘", "😗", "😚", "😋",
                "😛", "😜", "🤪", "😝", "🤑", "🤗", "🤭", "🤫", "🤔", "🤐",
                "🤨", "😐", "😑", "😶", "😏", "😒", "🙄", "😬", "😮‍💨", "🤥",
                "😌", "😔", "😪", "🤤", "😴", "😷", "🤒", "🤕", "🤢", "🤮",
                "🤧", "🥵", "🥶", "🥴", "😵", "🤯", "🤠", "🥳", "🥸", "😎",
                "🤓", "🧐", "😕", "😟", "🙁", "😮", "😯", "😲", "😳", "🥺",
                "😦", "😧", "😨", "😰", "😥", "😢", "😭", "😱", "😖", "😣",
                "😞", "😓", "😩", "😫", "🥱", "😤", "😡", "😠", "🤬", "😈",
                "👿", "💀", "☠", "💩", "🤡", "👹", "👺", "👻", "👽", "🤖"
            }),
            ["hearts"] = ("Tình cảm & Trái tim", new[]
            {
                "💖", "❤️", "🧡", "💛", "💚", "💙", "💜", "🖤", "🤍", "🤎",
                "💔", "❣️", "💕", "💞", "💓", "💗", "💘", "💝", "💟", "💌",
                "💋", "🫂", "😻", "😽", "💑", "👩‍❤️‍👨", "💏", "💐", "🌹", "🌸",
                "🌺", "🌷", "🌻", "🥀", "🌼", "🌿", "🍀", "✨", "🌟", "💫"
            }),
            ["gestures"] = ("Cử chỉ & Bàn tay", new[]
            {
                "👍", "👎", "👊", "✊", "🤛", "🤜", "👏", "🙌", "👐", "🤲",
                "🤝", "🙏", "✍", "💅", "🤳", "💪", "🦾", "🦵", "🦶", "👂",
                "👃", "👁", "👀", "👅", "👄", "👈", "👉", "👆", "👇", "☝",
                "🖐", "✋", "🤚", "🖖", "👋", "🤙", "🤏", "✌", "🤞", "🤟"
            }),
            ["food"] = ("Đồ ăn & Thức uống", new[]
            {
                "🍔", "🍟", "🍕", "🌭", "🥪", "🌮", "🌯", "🥗", "🥘", "🍝",
                "🍜", "🍲", "🍛", "🍣", "🍱", "🥟", "🍤", "🍙", "🍚", "🍘",
                "🍦", "🍧", "🍨", "🍩", "🍪", "🎂", "🍰", "🧁", "🥧", "🍫",
                "🍬", "🍭", "🍮", "🍯", "☕", "🍵", "🧃", "🥤", "🧋", "🍺",
                "🍻", "🥂", "🍷", "🥃", "🍸", "🍹", "🍾", "🍿", "🥑", "🍓"
            }),
            ["fun"] = ("Tiệc tùng & Hoạt động", new[]
            {
                "🎉", "🎊", "🎈", "🎁", "🎀", "🎆", "🎇", "🧨", "✨", "🌟",
                "🎵", "🎶", "🎸", "🎹", "🥁", "🎺", "🎻", "🎲", "🎯", "🎳",
                "🎮", "🕹", "🎰", "🏆", "🥇", "🥈", "🥉", "🏅", "⚽", "🏀",
                "🏈", "⚾", "🎾", "🏐", "🎱", "🏓", "🏸", "🥊", "🥋", "🛹"
            }),
            ["objects"] = ("Đồ vật & Biểu tượng", new[]
            {
                "🚀", "🛸", "🚁", "✈", "🚗", "🏎", "🏍", "🚲", "🚂", "🚢",
                "🏝", "🏖", "🌋", "🏰", "💻", "🖥", "📱", "⌚", "💡", "🔦",
                "🕯", "💰", "💵", "💎", "🔑", "🔒", "🔮", "🪄", "🧪", "🧬",
                "🔭", "🌈", "⚡", "🔥", "❄", "⭐", "☀️", "🌙", "☁️", "🪐"
            }),
        };

        private void InitEmojiPicker()
        {
            LoadEmojiCategory("smileys");
        }

        private void EmojiCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string categoryKey)
            {
                LoadEmojiCategory(categoryKey);
            }
        }

        private static string GetEmojiCodePoint(string emoji)
        {
            var codePoints = new List<string>();
            for (int i = 0; i < emoji.Length;)
            {
                int cp = char.ConvertToUtf32(emoji, i);
                if (cp != 0xfe0f)
                {
                    codePoints.Add(cp.ToString("x"));
                }
                i += char.IsSurrogatePair(emoji, i) ? 2 : 1;
            }
            return string.Join("-", codePoints);
        }

        private static readonly Dictionary<string, BitmapImage> _emojiImageCache = new();

        private static BitmapImage? GetEmojiBitmap(string codePoint)
        {
            if (_emojiImageCache.TryGetValue(codePoint, out var cached))
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
                        _emojiImageCache[codePoint] = bi;
                        return bi;
                    }
                    catch { }
                }
            }

            return null;
        }

        private void LoadEmojiCategory(string categoryKey)
        {
            if (!_emojiCategories.TryGetValue(categoryKey, out var catData))
                return;

            TxtEmojiCategoryTitle.Text = catData.Title;
            EmojiContainer.Children.Clear();

            var itemStyle = TryFindResource("EmojiItemButton") as Style;

            foreach (var emoji in catData.Emojis)
            {
                string cp = GetEmojiCodePoint(emoji);
                var bmp = GetEmojiBitmap(cp);

                object btnContent;
                if (bmp != null)
                {
                    btnContent = new Image
                    {
                        Source = bmp,
                        Width = 26,
                        Height = 26,
                        Stretch = System.Windows.Media.Stretch.Uniform,
                    };
                }
                else
                {
                    btnContent = new TextBlock
                    {
                        Text = emoji,
                        FontSize = 20,
                        FontFamily = new System.Windows.Media.FontFamily("Segoe UI Emoji, Segoe UI"),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                }

                var btn = new Button
                {
                    Content = btnContent,
                    ToolTip = emoji,
                };

                if (itemStyle != null)
                {
                    btn.Style = itemStyle;
                }
                else
                {
                    btn.Width = 38;
                    btn.Height = 38;
                    btn.Margin = new Thickness(2);
                    btn.Cursor = Cursors.Hand;
                }

                btn.Click += (_, _) =>
                {
                    int caretIndex = TxtMessage.CaretIndex;
                    TxtMessage.Text = TxtMessage.Text.Insert(caretIndex, emoji);
                    TxtMessage.CaretIndex = caretIndex + emoji.Length;
                    EmojiPopup.IsOpen = false;
                    TxtMessage.Focus();
                };

                EmojiContainer.Children.Add(btn);
            }
        }

        private void BtnEmoji_Click(object sender, RoutedEventArgs e)
        {
            EmojiPopup.IsOpen = !EmojiPopup.IsOpen;
        }

        private async void BtnSendImage_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentConversationId.HasValue) return;

            var ofd = new OpenFileDialog
            {
                Title = "Chọn hình ảnh để gửi",
                Filter = "Hình ảnh (*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|Tất cả tập tin (*.*)|*.*",
                Multiselect = false,
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    var fi = new FileInfo(ofd.FileName);
                    if (fi.Length > 10 * 1024 * 1024)
                    {
                        MessageBox.Show("Kích thước hình ảnh vượt quá giới hạn 10 MB.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    await _client.SendFileAsync(_currentConversationId.Value, ofd.FileName, "image");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi gửi ảnh: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnSendFile_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentConversationId.HasValue) return;

            var ofd = new OpenFileDialog
            {
                Title = "Chọn tập tin để gửi",
                Filter = "Tất cả tập tin (*.*)|*.*",
                Multiselect = false,
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    var fi = new FileInfo(ofd.FileName);
                    if (fi.Length > 10 * 1024 * 1024)
                    {
                        MessageBox.Show("Kích thước tập tin vượt quá giới hạn 10 MB.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    await _client.SendFileAsync(_currentConversationId.Value, ofd.FileName, "file");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi gửi tập tin: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnDownloadFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ChatMessage msg)
            {
                if (msg.RawFileData == null || msg.RawFileData.Length == 0)
                {
                    MessageBox.Show("Không có dữ liệu tập tin để tải về.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var sfd = new SaveFileDialog
                {
                    Title = "Lưu tập tin",
                    FileName = msg.FileName ?? "downloaded_file",
                    Filter = "Tất cả tập tin (*.*)|*.*",
                };

                if (sfd.ShowDialog() == true)
                {
                    try
                    {
                        File.WriteAllBytes(sfd.FileName, msg.RawFileData);
                        MessageBox.Show("Tải tập tin thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Lỗi khi lưu tập tin: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void Image_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ChatMessage msg && msg.RawFileData != null)
            {
                var result = MessageBox.Show("Bạn có muốn lưu hình ảnh này về máy không?", "Hình ảnh", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    var sfd = new SaveFileDialog
                    {
                        Title = "Lưu hình ảnh",
                        FileName = msg.FileName ?? "image.png",
                        Filter = "Hình ảnh (*.png;*.jpg)|*.png;*.jpg|Tất cả tập tin (*.*)|*.*",
                    };

                    if (sfd.ShowDialog() == true)
                    {
                        try
                        {
                            File.WriteAllBytes(sfd.FileName, msg.RawFileData);
                            MessageBox.Show("Lưu hình ảnh thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Lỗi khi lưu ảnh: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // VIEW MODELS (dữ liệu hiển thị UI, không lưu DB)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Item hiển thị cuộc hội thoại trong sidebar.</summary>
    public class ConversationItem
    {
        public int ConversationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "direct";
        public string TypeLabel { get; set; } = string.Empty;
    }

    /// <summary>Item hiển thị người dùng online trong sidebar.</summary>
    public class OnlineUserItem
    {
        public int UserId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }
}