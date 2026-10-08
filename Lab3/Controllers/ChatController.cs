using Microsoft.AspNetCore.Mvc;

namespace Lab3.Controllers
{
    public class ChatController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
