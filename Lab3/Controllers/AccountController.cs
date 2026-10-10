using System.Security.Claims;
using Lab3.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace Lab3.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserService _userService;

        public AccountController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Chat");
            }

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.LoginError = "Vui lòng nhập tên đăng nhập và mật khẩu.";
                return View();
            }

            var user = _userService.ValidateUser(username, password);
            if (user == null)
            {
                ViewBag.LoginError = "Tên đăng nhập hoặc mật khẩu không chính xác.";
                return View();
            }

            await SignInUserAsync(user);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Chat");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string username, string password, string confirmPassword, string displayName, string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.ActiveTab = "register";

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.RegisterError = "Tên đăng nhập và mật khẩu không được để trống.";
                return View("Login");
            }

            if (password != confirmPassword)
            {
                ViewBag.RegisterError = "Mật khẩu xác nhận không khớp.";
                return View("Login");
            }

            if (!_userService.Register(username, password, displayName, out var errorMessage))
            {
                ViewBag.RegisterError = errorMessage;
                return View("Login");
            }

            var user = _userService.GetUser(username);
            if (user != null)
            {
                await SignInUserAsync(user);
            }

            return RedirectToAction("Index", "Chat");
        }

        [HttpGet]
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        private async Task SignInUserAsync(Models.UserAccount user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Username),
                new Claim(ClaimTypes.Name, user.DisplayName),
                new Claim("Username", user.Username),
                new Claim("DisplayName", user.DisplayName)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);
        }
    }
}
