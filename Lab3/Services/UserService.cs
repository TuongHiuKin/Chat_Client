using System.Collections.Concurrent;
using System.Text.Json;
using Lab3.Models;

namespace Lab3.Services
{
    public interface IUserService
    {
        UserAccount? ValidateUser(string username, string password);
        bool Register(string username, string password, string displayName, out string errorMessage);
        UserAccount? GetUser(string username);
        IEnumerable<UserAccount> GetAllUsers();
    }

    public class UserService : IUserService
    {
        private readonly string _dataFilePath;
        private readonly ConcurrentDictionary<string, UserAccount> _users = new(StringComparer.OrdinalIgnoreCase);

        public UserService(IWebHostEnvironment env)
        {
            var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }
            _dataFilePath = Path.Combine(dataDir, "users.json");

            LoadUsers();
        }

        private void LoadUsers()
        {
            if (File.Exists(_dataFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_dataFilePath);
                    var list = JsonSerializer.Deserialize<List<UserAccount>>(json);
                    if (list != null)
                    {
                        foreach (var u in list)
                        {
                            _users[u.Username] = u;
                        }
                    }
                }
                catch
                {
                    // Fallback to default users if file is corrupted
                }
            }

            // Seed default users if empty (như trong Lab 2)
            if (_users.IsEmpty)
            {
                var defaults = new List<UserAccount>
                {
                    new UserAccount { Username = "user1", Password = "123", DisplayName = "Khang (User 1)" },
                    new UserAccount { Username = "user2", Password = "123", DisplayName = "Vy (User 2)" },
                    new UserAccount { Username = "admin", Password = "123", DisplayName = "Quản trị viên" }
                };

                foreach (var u in defaults)
                {
                    _users[u.Username] = u;
                }
                SaveUsers();
            }
        }

        private void SaveUsers()
        {
            try
            {
                var list = _users.Values.ToList();
                var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_dataFilePath, json);
            }
            catch
            {
                // Ignore save errors
            }
        }

        public UserAccount? ValidateUser(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return null;

            if (_users.TryGetValue(username.Trim(), out var user))
            {
                if (user.Password == password)
                {
                    return user;
                }
            }

            return null;
        }

        public bool Register(string username, string password, string displayName, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(username))
            {
                errorMessage = "Tên đăng nhập không được để trống.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                errorMessage = "Mật khẩu không được để trống.";
                return false;
            }

            var trimmedUser = username.Trim();
            if (_users.ContainsKey(trimmedUser))
            {
                errorMessage = $"Tên đăng nhập '{trimmedUser}' đã tồn tại.";
                return false;
            }

            var finalDisplayName = string.IsNullOrWhiteSpace(displayName) ? trimmedUser : displayName.Trim();
            var newUser = new UserAccount
            {
                Username = trimmedUser,
                Password = password,
                DisplayName = finalDisplayName,
                CreatedAt = DateTime.Now
            };

            if (_users.TryAdd(trimmedUser, newUser))
            {
                SaveUsers();
                return true;
            }

            errorMessage = "Đăng ký không thành công. Vui lòng thử lại.";
            return false;
        }

        public UserAccount? GetUser(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return null;
            _users.TryGetValue(username.Trim(), out var user);
            return user;
        }

        public IEnumerable<UserAccount> GetAllUsers()
        {
            return _users.Values;
        }
    }
}
