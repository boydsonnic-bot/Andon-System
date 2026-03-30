using Microsoft.AspNetCore.Mvc;

namespace AndonDashBroad.Controllers
{
    public class SpcController : Controller
    {
        public IActionResult Index()
        {
            // Trả về file giao diện Views/Spc/Index.cshtml
            return View();
        }
    }
}