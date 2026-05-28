using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SomaShare.Services;

namespace SomaShare.Controllers
{
    [Authorize]
    public class TransactionsController : Controller
    {
        private readonly ITransactionService _transactionService;
        public TransactionsController(ITransactionService transactionService) => _transactionService = transactionService;

        [HttpPost]
        public async Task<IActionResult> Complete(int transactionId)
        {
            await _transactionService.CompleteTransactionAsync(transactionId);
            return RedirectToAction("Index", "Dashboard");
        }
    }
}