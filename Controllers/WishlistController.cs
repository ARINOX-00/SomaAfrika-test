using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SomaShare.Services;
using System.Security.Claims;

namespace SomaShare.Controllers
{
    [Authorize]
    public class WishlistController : Controller
    {
        private readonly IWishlistService _wishlistService;

        public WishlistController(IWishlistService wishlistService)
        {
            _wishlistService = wishlistService;
        }

        // Toggle: adds or removes
        [HttpPost]
        public async Task<IActionResult> Toggle(int textbookId, string? returnUrl = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            bool isInWishlist = await _wishlistService.IsInWishlistAsync(userId, textbookId);
            if (isInWishlist)
                await _wishlistService.RemoveFromWishlistAsync(userId, textbookId);
            else
                await _wishlistService.AddToWishlistAsync(userId, textbookId);

            if (!string.IsNullOrEmpty(returnUrl))
                return LocalRedirect(returnUrl);

            // Default fallback
            return RedirectToAction("Details", "Textbooks", new { id = textbookId });
        }

        // View wishlist
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var wishlist = await _wishlistService.GetUserWishlistAsync(userId);
            return View(wishlist);
        }
    }
}