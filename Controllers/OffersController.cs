using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SomaShare.Data;
using SomaShare.Models;
using SomaShare.Services;
using System.Security.Claims;
namespace SomaShare.Controllers
{
    [Authorize]
    public class OffersController : Controller
    {
        private readonly IOfferService _offerService;
        private readonly ApplicationDbContext _context;

        public OffersController(IOfferService offerService, ApplicationDbContext context)
        {
            _offerService = offerService;
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Create(int textbookId, decimal amount)
        {
            try
            {
                var buyerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                await _offerService.CreateOfferAsync(buyerId, textbookId, amount);

                // Fetch the textbook to get the seller's ID and title
                var textbook = await _context.Textbooks.FindAsync(textbookId);
                if (textbook != null)
                {
                    // Create a notification for the seller
                    var notification = new Notification
                    {
                        UserId = textbook.SellerId,
                        Message = $"A new offer of R{amount:F2} was made on your listing '{textbook.Title}'.",
                        RelatedEntityType = "Textbook",
                        RelatedEntityId = textbookId
                    };
                    _context.Notifications.Add(notification);
                    await _context.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = "Your offer has been submitted successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction("Details", "Textbooks", new { id = textbookId });
        }

        [HttpPost]
        public async Task<IActionResult> Accept(int offerId)
        {
            try
            {
                // Get offer details before accepting (to know buyer and textbook)
                var offer = await _context.Offers
                    .Include(o => o.Buyer)
                    .Include(o => o.Textbook)
                    .FirstOrDefaultAsync(o => o.OfferId == offerId);
                if (offer == null) throw new Exception("Offer not found.");

                await _offerService.AcceptOfferAsync(offerId);

                // Create notification for the buyer
                var buyerNotification = new Notification
                {
                    UserId = offer.BuyerId,
                    Message = $"Your offer of R{offer.OfferAmount:F2} on '{offer.Textbook.Title}' was accepted!",
                    RelatedEntityType = "Textbook",
                    RelatedEntityId = offer.TextbookId
                };
                _context.Notifications.Add(buyerNotification);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Offer accepted! Transaction has been created.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction("Index", "Dashboard");
        }

        [HttpPost]
        public async Task<IActionResult> Reject(int offerId)
        {
            await _offerService.RejectOfferAsync(offerId);
            return RedirectToAction("Index", "Dashboard");
        }
    }
}