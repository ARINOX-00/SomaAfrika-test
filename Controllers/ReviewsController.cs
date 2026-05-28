using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SomaShare.Models;
using SomaShare.Services;
using System.Security.Claims;
using SomaShare.Data;
using SomaAfrika_SS2_ENTITY404.Models;
using Microsoft.EntityFrameworkCore;


namespace SomaShare.Controllers
{
    [Authorize]
    public class ReviewsController : Controller
    {
        private readonly IReviewService _reviewService;
        private readonly ApplicationDbContext _context;
        public ReviewsController(IReviewService reviewService, ApplicationDbContext context)   // add parameter
        {
            _reviewService = reviewService;
            _context = context;
        }

        public class ReportRequest
        {
            public int ReviewId { get; set; }
            public string Reason { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Create(int transactionId, int rating, string comment)
        {
            var review = new Review
            {
                TransactionId = transactionId,
                ReviewerId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                RatingValue = rating,
                CommentText = comment,
                DateReviewed = DateTime.UtcNow
            };
            try
            {
                await _reviewService.CreateReviewAsync(review);
                TempData["SuccessMessage"] = "Thank you for your review!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction("Index", "Dashboard");
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review == null) return NotFound();
            if (review.ReviewerId != User.FindFirstValue(ClaimTypes.NameIdentifier))
                return Forbid();
            if (!review.CanEditOrDelete)
            {
                TempData["ErrorMessage"] = "The 24-hour edit window has expired. ";
                return RedirectToAction("Index", "Dashboard");
            }

            return View(review);
        

            
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Edit(int id, int rating, string comment)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review == null) return NotFound();
            if (review.ReviewerId != User.FindFirstValue(ClaimTypes.NameIdentifier))
                return Forbid();
            if (!review.CanEditOrDelete) return BadRequest("Edit window has expired.");
            review.RatingValue = rating;
            review.CommentText = comment;
            review.LastModifiedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            //recalculate trust score for reviewee
            await UpdateTrustScore(review.RevieweeId);

            TempData["SuccessMessage"] = "Your review has been updated.";
            return RedirectToAction("Index", "Dashboard");
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review == null) return NotFound();
            if (review.ReviewerId != User.FindFirstValue(ClaimTypes.NameIdentifier))
                return Forbid();
            if (!review.CanEditOrDelete) return BadRequest("Delete window has expired.");

            review.IsDeleted = true;
            await _context.SaveChangesAsync();
            await UpdateTrustScore(review.RevieweeId);

            TempData["SuccessMessage"] = "Review deleted.";
            return RedirectToAction("Index", "Dashboard");
        }

        private async Task UpdateTrustScore(string userId)
        {
            var reviewee = await _context.Users.FindAsync(userId);
            if (reviewee != null)
            {
                var avg = await _context.Reviews
                    .Where(r => r.RevieweeId == userId && !r.IsDeleted)
                    .AverageAsync(r => (decimal?)r.RatingValue) ?? 0;
                reviewee.TrustScore = Math.Round(avg, 2);
                await _context.SaveChangesAsync();
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Report([FromBody] ReportRequest request)
        {
            //validate reason not null or whitespace
            if (request == null || string.IsNullOrWhiteSpace(request.Reason))
                return BadRequest("Please provide a reason for the report.");

                var report = new Report
            {
                ReviewId = request.ReviewId,
                ReporterUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                Reason = request.Reason
            };
            _context.Reports.Add(report);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}