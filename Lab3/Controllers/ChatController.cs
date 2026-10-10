using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lab3.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly IWebHostEnvironment _env;

        public ChatController(IWebHostEnvironment env)
        {
            _env = env;
        }

        public IActionResult Index()
        {
            var displayName = User.FindFirst("DisplayName")?.Value ?? User.Identity?.Name ?? "User";
            var username = User.FindFirst("Username")?.Value ?? User.Identity?.Name ?? "User";

            ViewBag.CurrentUser = displayName;
            ViewBag.Username = username;

            return View();
        }

        /// <summary>
        /// API tải lên tệp tin và hình ảnh (hỗ trợ tối đa 500 MB như Lab 2)
        /// </summary>
        [HttpPost("api/chat/upload")]
        [RequestSizeLimit(524_288_000)]
        public async Task<IActionResult> UploadFile(IFormFile? file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { success = false, message = "Vui lòng chọn một tập tin." });
                }

                if (file.Length > 524_288_000)
                {
                    return BadRequest(new { success = false, message = "Tập tin vượt quá giới hạn cho phép (500 MB)." });
                }

                var webRoot = _env.WebRootPath;
                if (string.IsNullOrEmpty(webRoot))
                {
                    webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                }
                var uploadsFolder = Path.Combine(webRoot, "uploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var originalName = Path.GetFileName(file.FileName);
                var extension = Path.GetExtension(originalName).ToLowerInvariant();
                var safeId = Guid.NewGuid().ToString("N").Substring(0, 8);
                var uniqueFileName = $"{DateTime.Now:yyyyMMddHHmmssfff}_{safeId}_{originalName}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var isImage = extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp";
                var fileUrl = $"/uploads/{uniqueFileName}";

                return Ok(new
                {
                    success = true,
                    fileName = originalName,
                    fileSize = file.Length,
                    fileUrl = fileUrl,
                    isImage = isImage
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}
