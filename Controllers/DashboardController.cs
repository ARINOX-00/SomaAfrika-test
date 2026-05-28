using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SomaShare.Services;
using SomaShare.Models.ViewModels;
using System.Security.Claims;

namespace SomaShare.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ITextbookService _textbookService;
        private readonly IOfferService _offerService;
        private readonly ITransactionService _transactionService;
        private readonly IReviewService _reviewService;
        private readonly IWishlistService _wishlistService;

        public DashboardController(ITextbookService textbookService, IOfferService offerService,
            ITransactionService transactionService, IReviewService reviewService, IWishlistService wishlistService)
        {
            _textbookService = textbookService;
            _offerService = offerService;
            _transactionService = transactionService;
            _reviewService = reviewService;
            _wishlistService = wishlistService;
        }

        public async Task<IActionResult> Index(int? rating = null, string sort = "newest", int page = 1)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            const int pageSize = 5;  // Show 5 reviews per page

            // Get paginated reviews
            var paginatedReviews = await _reviewService.GetUserReviewsPaginatedAsync(userId, rating, sort, page, pageSize);

            // Also need the full list of transactions, offers, etc. (those may still be fetched fully, or also paginated later)
            var vm = new DashboardViewModel
            {
                MyListings = await _textbookService.GetUserListingsAsync(userId),
                MyOffers = await _offerService.GetUserOffersAsync(userId),
                MyTransactions = await _transactionService.GetUserTransactionsAsync(userId),
                MyReviews = paginatedReviews.Items,                 // only one page of reviews
                MyWishlist = await _wishlistService.GetUserWishlistAsync(userId),
                ReviewedTransactionIds = await _reviewService.GetReviewedTransactionIdsAsync(userId)
            };

            // Pass pagination metadata to the view via ViewBag (or add properties to ViewModel)
            ViewBag.CurrentPage = paginatedReviews.PageIndex;
            ViewBag.TotalPages = paginatedReviews.TotalPages;
            ViewBag.HasPreviousPage = paginatedReviews.HasPreviousPage;
            ViewBag.HasNextPage = paginatedReviews.HasNextPage;
            ViewBag.CurrentRating = rating;
            ViewBag.CurrentSort = sort;
            ViewBag.PageSize = pageSize;

            // Also pass rating distribution (for the bar chart)
            var distribution = await _reviewService.GetRatingDistributionAsync(userId);
            ViewBag.RatingDistribution = distribution;
            ViewBag.TotalReviews = distribution.Values.Sum();

            return View(vm);
        }
    }
}