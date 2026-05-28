using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SomaShare.Services;
using SomaShare.Models;
using SomaShare.Models.ViewModels;
using System.Security.Claims;

namespace SomaShare.Controllers
{
    public class TextbooksController : Controller
    {
        private readonly ITextbookService _textbookService;
        private readonly IOfferService _offerService;
        private readonly IWishlistService _wishlistService;

        public TextbooksController(ITextbookService textbookService, IOfferService offerService, IWishlistService wishlistService)
        {
            _textbookService = textbookService;
            _offerService = offerService;
            _wishlistService = wishlistService;
        }

        public async Task<IActionResult> Index(string? searchTerm, string? campus, string? condition,
            decimal? minPrice, decimal? maxPrice, string? sortBy, int page = 1)
        {
            int pageSize = 9;
            var textbooks = await _textbookService.SearchAsync(searchTerm, campus, condition, minPrice, maxPrice, sortBy, page, pageSize);
            // Pass wishlist info to view
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var wishlistedIds = (await _wishlistService.GetUserWishlistAsync(userId))
                                    .Select(b => b.TextbookId)
                                    .ToHashSet();
                ViewBag.WishlistedIds = wishlistedIds;
            }
            else
            {
                ViewBag.WishlistedIds = new HashSet<int>();
            }
            ViewBag.SearchTerm = searchTerm;
            ViewBag.Campus = campus;
            ViewBag.Condition = condition;
            // Populate dropdowns
            ViewBag.Campuses = new SelectList(new[] { "Centurion", "Waterfall", "Bellville", "Musgrave" });
            ViewBag.Conditions = new SelectList(new[] { "1", "2", "3" });
            return View(textbooks);
        }

        [Authorize(Roles = "Seller,Admin")]
        public IActionResult Create() => View(new TextbookCreateViewModel());

        [HttpPost, Authorize(Roles = "Seller,Admin")]
        public async Task<IActionResult> Create(TextbookCreateViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var textbook = new Textbook
            {
                SellerId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                Title = model.Title,
                Author = model.Author,
                ISBN = model.ISBN,
                Condition = model.Condition,
                ListingPrice = model.ListingPrice,
                Campus = model.Campus,
                Category = model.Category,
                // ImageUrl handling simplified; use a file upload service in reality
                ImageUrl = model.Image != null ? "/images/" + model.Image.FileName : null
            };
            await _textbookService.CreateAsync(textbook);
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Details(int id)
        {
            var textbook = await _textbookService.GetByIdAsync(id);
            if (textbook == null) return NotFound();
            ViewBag.Offers = await _offerService.GetOffersForListingAsync(id);
            // Check wishlist
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId != null)
                ViewBag.IsWishlisted = await _wishlistService.IsInWishlistAsync(userId, id);
            return View(textbook);
        }

        [Authorize, HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            await _textbookService.DeleteAsync(id);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Seller,Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var textbook = await _textbookService.GetByIdAsync(id);
            if (textbook == null) return NotFound();

            // Ensure only the owner or Admin can edit
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (textbook.SellerId != userId && !User.IsInRole("Admin"))
                return Forbid();

            return View(textbook);
        }

        [HttpPost]
        [Authorize(Roles = "Seller,Admin")]
        public async Task<IActionResult> Edit(int id, [Bind("TextbookId,Title,Author,ISBN,Condition,ListingPrice,Campus,Category,ImageUrl")] Textbook model)
        {
            if (id != model.TextbookId) return NotFound();

            var textbook = await _textbookService.GetByIdAsync(id);
            if (textbook == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (textbook.SellerId != userId && !User.IsInRole("Admin"))
                return Forbid();

            //remove validation errors for properties not present in form
            ModelState.Remove("SellerId");
            ModelState.Remove("Seller");
            ModelState.Remove("Offers");
            ModelState.Remove("WishlistedBy");

            if (ModelState.IsValid)
            {
                // Update fields manually (to avoid over‑posting sensitive data)
                textbook.Title = model.Title;
                textbook.Author = model.Author;
                textbook.ISBN = model.ISBN;
                textbook.Condition = model.Condition;
                textbook.ListingPrice = model.ListingPrice;
                textbook.Campus = model.Campus;
                textbook.Category = model.Category;
                textbook.ImageUrl = model.ImageUrl;

                await _textbookService.UpdateAsync(textbook);
                return RedirectToAction("Details", new { id = textbook.TextbookId });
            }
            return View(model);
        }
    }
}