using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace SomaShare.Controllers
{
    public class HomeController : Controller
    {
        private readonly IStringLocalizer<HomeController> _localizer;
        public HomeController(IStringLocalizer<HomeController> localizer) { _localizer = localizer; }

        public IActionResult Index() { ViewData["Welcome"] = _localizer["Welcome"]; return View(); }
        public IActionResult Privacy() => View();

        [HttpPost]
        public IActionResult SetLanguage(string culture, string returnUrl)
        {
            Response.Cookies.Append("Culture", culture);
            return LocalRedirect(returnUrl ?? "/");
        }
    }
}