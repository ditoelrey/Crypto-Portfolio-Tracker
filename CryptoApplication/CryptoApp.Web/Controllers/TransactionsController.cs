using Microsoft.AspNetCore.Mvc;
using CryptoApp.Application.Interfaces;
using CryptoApp.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace CryptoApp.Web.Controllers
{
    [Authorize]
    public class TransactionsController : Controller
    {
        private readonly IPortfolioService _portfolioService;
        private readonly ITransactionService _transactionService;
        private readonly ICryptocurrencyService _cryptoService;
        private readonly ILogger<TransactionsController> _logger;

        private string? CurrentUserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        public TransactionsController(
            IPortfolioService portfolioService,
            ITransactionService transactionService,
            ICryptocurrencyService cryptoService,
            ILogger<TransactionsController> logger)
        {
            _portfolioService = portfolioService;
            _transactionService = transactionService;
            _cryptoService = cryptoService;
            _logger = logger;
        }

        private async Task<bool> VerifyPortfolioAccess(string portfolioId)
        {
            var portfolio = await _portfolioService.GetPortfolioAsync(portfolioId);
            if (portfolio == null)
            {
                _logger.LogWarning("Portfolio not found with ID: {PortfolioId}", portfolioId);
                return false;
            }

            var userId = CurrentUserId;
            if (userId != portfolio.UserId)
            {
                _logger.LogWarning("Unauthorized access attempt to portfolio {PortfolioId} by user {UserId}", 
                    portfolioId, userId);
                return false;
            }

            return true;
        }

        // GET: /Transactions/Portfolio/5
        public async Task<IActionResult> Portfolio(string id)
        {
            if (!await VerifyPortfolioAccess(id))
            {
                return NotFound();
            }

            var transactions = await _transactionService.GetPortfolioTransactionsAsync(id);
            ViewBag.PortfolioId = id;
            return View(transactions);
        }

        // GET: /Transactions/Add/5
        public async Task<IActionResult> Add(string portfolioId)
        {
            if (!await VerifyPortfolioAccess(portfolioId))
            {
                return NotFound();
            }

            ViewBag.PortfolioId = portfolioId;
            ViewBag.Cryptocurrencies = await _cryptoService.GetAllCryptocurrenciesAsync();
            return View(new Transaction 
            { 
                PortfolioId = portfolioId, 
                Date = DateTime.UtcNow 
            });
        }

        // POST: /Transactions/Add
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(Transaction transaction)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.PortfolioId = transaction.PortfolioId;
                ViewBag.Cryptocurrencies = await _cryptoService.GetAllCryptocurrenciesAsync();
                return View(transaction);
            }

            if (!await VerifyPortfolioAccess(transaction.PortfolioId))
            {
                return NotFound();
            }

            try
            {
                // Get current price if not provided
                if (transaction.PriceAtTime == 0)
                {
                    var crypto = await _cryptoService.GetCryptocurrencyAsync(transaction.CryptocurrencyId);
                    if (crypto != null)
                    {
                        transaction.PriceAtTime = await _cryptoService.GetCurrentPriceAsync(crypto.CoinGeckoId);
                        _logger.LogInformation(
                            "Using current price {Price} for cryptocurrency {CryptoId}", 
                            transaction.PriceAtTime, 
                            transaction.CryptocurrencyId);
                    }
                }

                if (transaction.Date == default)
                {
                    transaction.Date = DateTime.UtcNow;
                }

                await _transactionService.CreateTransactionAsync(
                    transaction.PortfolioId,
                    transaction.CryptocurrencyId,
                    transaction.Type,
                    transaction.Amount,
                    transaction.PriceAtTime
                );

                _logger.LogInformation(
                    "Added {Type} transaction for {Amount} {CryptoId} at ${Price} to portfolio {PortfolioId}", 
                    transaction.Type, 
                    transaction.Amount, 
                    transaction.CryptocurrencyId,
                    transaction.PriceAtTime,
                    transaction.PortfolioId);

                return RedirectToAction("Details", "Portfolio", new { id = transaction.PortfolioId });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid transaction attempt: {Message}", ex.Message);
                ModelState.AddModelError("", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding transaction to portfolio {PortfolioId}", transaction.PortfolioId);
                ModelState.AddModelError("", "Failed to add transaction. Please try again.");
            }

            ViewBag.PortfolioId = transaction.PortfolioId;
            ViewBag.Cryptocurrencies = await _cryptoService.GetAllCryptocurrenciesAsync();
            return View(transaction);
        }
    }
}
