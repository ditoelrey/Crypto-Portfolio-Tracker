using Microsoft.AspNetCore.Mvc;
using CryptoApp.Application.Interfaces;
using CryptoApp.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace CryptoApp.Web.Controllers
{
    [Authorize]
    public class HoldingsController : Controller
    {
        private readonly IPortfolioService _portfolioService;
        private readonly ICryptocurrencyService _cryptoService;
        private readonly ILogger<HoldingsController> _logger;

        private string? CurrentUserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        public HoldingsController(
            IPortfolioService portfolioService,
            ICryptocurrencyService cryptoService,
            ILogger<HoldingsController> logger)
        {
            _portfolioService = portfolioService;
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

        // GET: /Holdings/Portfolio/5
        public async Task<IActionResult> Portfolio(string id)
        {
            var portfolio = await _portfolioService.GetPortfolioDetailsAsync(id);
            if (portfolio == null)
            {
                _logger.LogWarning("Portfolio not found with ID: {PortfolioId}", id);
                return NotFound();
            }

            if (portfolio.UserId != CurrentUserId)
            {
                _logger.LogWarning("Unauthorized access attempt to portfolio {PortfolioId}", id);
                return NotFound();
            }

            // Update current prices and values
            foreach (var holding in portfolio.Holdings)
            {
                try
                {
                    holding.Cryptocurrency.CurrentPrice = await _cryptoService.GetCurrentPriceAsync(holding.Cryptocurrency.CoinGeckoId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting current price for {Symbol}", holding.Cryptocurrency.Symbol);
                    holding.Cryptocurrency.CurrentPrice = 0;
                }
            }

            ViewBag.PortfolioId = id;
            return View(portfolio.Holdings);
        }

        // GET: /Holdings/Add/5
        public async Task<IActionResult> Add(string portfolioId)
        {
            if (!await VerifyPortfolioAccess(portfolioId))
            {
                return NotFound();
            }

            ViewBag.PortfolioId = portfolioId;
            ViewBag.Cryptocurrencies = await _cryptoService.GetAllCryptocurrenciesAsync();
            return View(new Holding { PortfolioId = portfolioId });
        }

        // POST: /Holdings/Add
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(Holding holding)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.PortfolioId = holding.PortfolioId;
                ViewBag.Cryptocurrencies = await _cryptoService.GetAllCryptocurrenciesAsync();
                return View(holding);
            }

            if (!await VerifyPortfolioAccess(holding.PortfolioId))
            {
                return NotFound();
            }

            try
            {
                // Get current price for the cryptocurrency
                var crypto = await _cryptoService.GetCryptocurrencyAsync(holding.CryptocurrencyId);
                if (crypto != null)
                {
                    holding.PurchasePrice = await _cryptoService.GetCurrentPriceAsync(crypto.CoinGeckoId);
                    _logger.LogInformation(
                        "Using current price {Price} for cryptocurrency {CryptoId}", 
                        holding.PurchasePrice, 
                        holding.CryptocurrencyId);
                }
                holding.PurchaseDate = DateTime.UtcNow;

                await _portfolioService.AddHoldingAsync(holding);
                
                _logger.LogInformation(
                    "Added holding {HoldingId} of {Amount} {CryptoId} at ${Price} to portfolio {PortfolioId}", 
                    holding.Id,
                    holding.Quantity,
                    holding.CryptocurrencyId,
                    holding.PurchasePrice,
                    holding.PortfolioId);

                return RedirectToAction("Details", "Portfolio", new { id = holding.PortfolioId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding holding to portfolio {PortfolioId}", holding.PortfolioId);
                ModelState.AddModelError("", "Failed to add holding. Please try again.");
                ViewBag.PortfolioId = holding.PortfolioId;
                ViewBag.Cryptocurrencies = await _cryptoService.GetAllCryptocurrenciesAsync();
                return View(holding);
            }
        }

        // GET: /Holdings/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            var portfolio = await _portfolioService.GetPortfolioDetailsAsync(id);
            if (portfolio == null)
            {
                return NotFound();
            }

            if (portfolio.UserId != CurrentUserId)
            {
                _logger.LogWarning("Unauthorized access attempt to portfolio {PortfolioId}", id);
                return NotFound();
            }

            var holding = portfolio.Holdings.FirstOrDefault(h => h.Id == id);
            if (holding == null)
            {
                _logger.LogWarning("Holding {HoldingId} not found in portfolio {PortfolioId}", id, portfolio.Id);
                return NotFound();
            }

            ViewBag.PortfolioId = portfolio.Id;
            ViewBag.Cryptocurrencies = await _cryptoService.GetAllCryptocurrenciesAsync();
            return View(holding);
        }

        // POST: /Holdings/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, Holding holding)
        {
            if (id != holding.Id)
            {
                return NotFound();
            }

            if (!await VerifyPortfolioAccess(holding.PortfolioId))
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    await _portfolioService.UpdateHoldingAsync(holding);
                    _logger.LogInformation(
                        "Updated holding {HoldingId} quantity to {Amount} in portfolio {PortfolioId}", 
                        id, 
                        holding.Quantity,
                        holding.PortfolioId);

                    return RedirectToAction("Details", "Portfolio", new { id = holding.PortfolioId });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating holding {HoldingId}", id);
                    ModelState.AddModelError("", "Failed to update holding. Please try again.");
                }
            }

            ViewBag.PortfolioId = holding.PortfolioId;
            ViewBag.Cryptocurrencies = await _cryptoService.GetAllCryptocurrenciesAsync();
            return View(holding);
        }
    }
}
