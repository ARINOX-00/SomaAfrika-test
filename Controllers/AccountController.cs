using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SomaAfrika_SS2_ENTITY404.Services;
using SomaShare.Data;                    // for ApplicationDbContext
using SomaShare.Models;
using SomaShare.Models.ViewModels;
using SomaShare.Services;               // for IEmailSender
using System.Security.Claims;

namespace SomaShare.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IOtpEmailSender _otpEmailSender;
        private readonly IPasswordResetEmailSender _resetEmailSender;
        private readonly ApplicationDbContext _context;
        //private string userId;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IOtpEmailSender otpEmailSender,
            IPasswordResetEmailSender resetEmailSender,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _otpEmailSender = otpEmailSender;
            _resetEmailSender = resetEmailSender;
            _context = context;
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                InstitutionEmail = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                EmailConfirmed = false
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
                return View(model);
            }

            // Assign role based on user's selection
            if (model.SelectedRole == "Seller")
            {
                await _userManager.AddToRoleAsync(user, "Seller");
                await _userManager.AddToRoleAsync(user, "Buyer"); // seller can also buy
            }
            else
            {
                await _userManager.AddToRoleAsync(user, "Buyer");
            }

            // Generate 6‑digit OTP
            var otp = new Random().Next(100000, 999999).ToString();
            var verification = new EmailVerification
            {
                UserId = user.Id,
                OtpCode = otp,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15)
            };
            _context.EmailVerifications.Add(verification);
            await _context.SaveChangesAsync();

            // Try to send the OTP email
            try
            {
                await _otpEmailSender.SendOtpAsync(user.Email, otp
                    //"SomaShare – Verify your account",
                    //$"Your verification code is: <strong>{otp}</strong><br/>This code expires in 15 minutes.");
                );
            }
            catch (Exception ex)
            {
                // Email send failed – roll back the user creation
                // Remove the verification record
                _context.EmailVerifications.Remove(verification);
                await _context.SaveChangesAsync();

                // Delete the user (this will also remove role assignments)
                await _userManager.DeleteAsync(user);

                // Log the real error for debugging
                // (the logger should already be injected into the controller)
                // _logger.LogError(ex, "Failed to send verification email for {Email}", user.Email);

                ModelState.AddModelError("", "We could not send the verification email. Please try again later.");
                return View(model);
            }

            // Success – email sent
            return RedirectToAction("VerifyOtp", new { userId = user.Id, email = user.Email});
        }
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (TempData["SuccessMessage"] != null)
                ViewBag.SuccessMessage = TempData["SuccessMessage"].ToString();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid) return View(model);

            var result = await _signInManager.PasswordSignInAsync(
                model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                // Redirect to Dashboard unless a returnUrl is specified
                return LocalRedirect(returnUrl ?? Url.Action("Index", "Dashboard"));
            }

            ModelState.AddModelError("", "Invalid login attempt.");
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        public IActionResult AccessDenied() => View();

        [Authorize]
        public async Task<IActionResult> Manage()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Login", "Account");

            var roles = await _userManager.GetRolesAsync(user) ?? new List<string>();
            ViewBag.IsSeller = roles.Contains("Seller");
            ViewBag.Roles = roles;

            return View(user);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> ToggleSeller()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            if (roles.Contains("Seller"))
                await _userManager.RemoveFromRoleAsync(user, "Seller");
            else
                await _userManager.AddToRoleAsync(user, "Seller");

            // Safety: ensure Buyer role remains
            if (!roles.Contains("Buyer") && !roles.Contains("Seller"))
                await _userManager.AddToRoleAsync(user, "Buyer");

            // Immediately update cookie claims
            await _signInManager.RefreshSignInAsync(user);

            return RedirectToAction("Manage");
        }

        [HttpGet]
        public IActionResult VerifyOtp(string userId, string email)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(email))
                return RedirectToAction("Register");

            ViewBag.Email = email;
            var model = new VerifyOtpViewModel { UserId = userId, Email = email };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> VerifyOtp(VerifyOtpViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var verification = await _context.EmailVerifications
                .FirstOrDefaultAsync(v => v.UserId == model.UserId && !v.IsUsed && v.ExpiresAt > DateTime.UtcNow);

            if (verification == null || verification.OtpCode != model.OtpCode)
            {
                ModelState.AddModelError("", "Invalid or expired OTP.");
                return View(model);
            }

            verification.IsUsed = true;
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user != null)
            {
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
            }
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Account verified! You can now log in.";
            return RedirectToAction("Login");
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> DeleteAccount()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");
            return View(user);
        }

        [Authorize]
        [HttpPost]
        [ActionName("DeleteAccount")]
        public async Task<IActionResult> DeleteAccountConfirmed()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            // Manually delete all user‑related records to respect foreign key restrictions
            // Load the user with all navigation properties included
            var userWithRelated = await _context.Users
                .Include(u => u.Textbooks)
                .Include(u => u.Offers)
                .Include(u => u.WantedAds)
                .Include(u => u.ReviewsGiven)
                .FirstOrDefaultAsync(u => u.Id == user.Id);

            if (userWithRelated != null)
            {
                // Remove related entities
                _context.Textbooks.RemoveRange(userWithRelated.Textbooks);
                _context.Offers.RemoveRange(userWithRelated.Offers);
                _context.WantedAds.RemoveRange(userWithRelated.WantedAds);
                _context.Reviews.RemoveRange(userWithRelated.ReviewsGiven);

                // Also remove any wishlist entries (UserWishlist) and transactions where user is a party
                var wishlistItems = _context.UserWishlists.Where(w => w.UserId == user.Id);
                _context.UserWishlists.RemoveRange(wishlistItems);

                var transactionsAsBuyer = _context.Transactions
                    .Where(t => t.Offer.BuyerId == user.Id);
                var transactionsAsSeller = _context.Transactions
                    .Where(t => t.Offer.Textbook.SellerId == user.Id);
                _context.Transactions.RemoveRange(transactionsAsBuyer);
                _context.Transactions.RemoveRange(transactionsAsSeller);

                // Finally remove the user itself
                _context.Users.Remove(userWithRelated);
                await _context.SaveChangesAsync();
            }

            // Sign out and redirect to home
            await _signInManager.SignOutAsync();
            TempData["Message"] = "Your account has been permanently deleted.";
            return RedirectToAction("Index", "Home");
        }
        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                //var callbackUrl = Url.Action(
                //    action: "ResetPassword",
                //    controller:"Account",
                //    values: new { userId = user.Id, token = token },
                //    protocol: Request.Scheme,
                //    host: Request.Host.Value);
                var devPublicUrl = "https://freestyle-pesticide-unpadded.ngrok-free.dev/"; // your ngrok URL
                var callbackUrl = $"{devPublicUrl}/Account/ResetPassword?userId={user.Id}&token={token}";

                try
                {
                    await _resetEmailSender.SendResetLinkAsync(user.Email, callbackUrl);
                        //"SomaShare – Reset Your Password",
                        //$"Your reset link: <a href='{callbackUrl}'>Reset Password</a>");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Email error: {ex.Message}");
                    return View(model);
                }
                // Show success even if user not found (security best practice)
            }

            return RedirectToAction("ForgotPasswordConfirmation");
        }

        [HttpGet]
        public IActionResult ForgotPasswordConfirmation() => View();

        [HttpGet]
        public IActionResult ResetPassword(string userId, string token)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Index", "Home");

            var model = new ResetPasswordViewModel { UserId = userId, Token = token };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
                return RedirectToAction("ResetPasswordConfirmation");

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);
            if (result.Succeeded)
                return RedirectToAction("ResetPasswordConfirmation");

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        [HttpGet]
        public IActionResult ResetPasswordConfirmation() => View();

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            var model = new EditProfileViewModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                StudentIdNumber = user.StudentIdNumber
            };
            return View(model);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> EditProfile(EditProfileViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.StudentIdNumber = model.StudentIdNumber;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                TempData["StatusMessage"] = "Profile updated successfully.";
                return RedirectToAction("Manage");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);
            return View(model);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new { n.Id, n.Message, n.CreatedAt, n.RelatedEntityType, n.RelatedEntityId })
                .ToListAsync();
            return Ok(notifications);
        }

        public class MarkAsReadRequest
        {
            public int Id { get; set; }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> MarkAsRead([FromBody] MarkAsReadRequest request)
        {
            try
            {
                var notification = await _context.Notifications.FindAsync(request.Id);
                if (notification != null && notification.UserId == User.FindFirstValue(ClaimTypes.NameIdentifier))
                {
                    notification.IsRead = true;
                    await _context.SaveChangesAsync();
                    return Ok(new { success = true });
                }
                return BadRequest(new { error = "Notification not found or unauthorized" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var unreadNotifcations = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();
            foreach( var notification in unreadNotifcations)
            {
                notification.IsRead = true;
            }
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }
    }
}