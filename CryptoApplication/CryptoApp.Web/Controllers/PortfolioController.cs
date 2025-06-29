using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using CryptoApp.Application.Interfaces;
using CryptoApp.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using CryptoApp.Domain.Models.Identity;

namespace CryptoApp.Web.Controllers;

[Authorize]
public class PortfolioController : Controller
{
    private readonly IPortfolioService _portfolioService;
    private readonly ICryptocurrencyService _cryptoService;
    private readonly ITransactionService _transactionService;
    private readonly ILogger<PortfolioController> _logger;
    private readonly IUserService _userService;
    private readonly ICryptoPriceCacheService _priceCache;

    public PortfolioController(
        IPortfolioService portfolioService,
        ICryptocurrencyService cryptoService,
        ITransactionService transactionService,
        ILogger<PortfolioController> logger,
        IUserService userService,
        ICryptoPriceCacheService priceCache)
    {
        _portfolioService = portfolioService;
        _cryptoService = cryptoService;
        _transactionService = transactionService;
        _logger = logger;
        _userService = userService;
        _priceCache = priceCache;
    }

    private string? CurrentUserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    // GET: /Portfolio
    public async Task<IActionResult> Index()
    {
        var userId = CurrentUserId;
        if (userId == null)
        {
            _logger.LogWarning("No CurrentUserId found");
            return Challenge();
        }

        var user = await _userService.GetUserWithPortfolioAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User not found with ID: {UserId}", userId);
            return NotFound();
        }

        if (user.Portfolio == null)
        {
            _logger.LogWarning("User {UserId} has no portfolio", userId);
            return NotFound();
        }

        return RedirectToAction(nameof(Details), new { id = user.Portfolio.Id });
    }

    // GET: /Portfolio/Details/5
    public async Task<IActionResult> Details(string id)
    {
        var userId = CurrentUserId;
        if (userId == null)
        {
            _logger.LogWarning("No CurrentUserId found");
            return Challenge();
        }

        var portfolio = await _portfolioService.GetPortfolioDetailsAsync(id);
        if (portfolio == null)
        {
            _logger.LogWarning("Portfolio not found with ID: {PortfolioId}", id);
            return NotFound();
        }

        // Check if portfolio belongs to the current user
        if (portfolio.UserId != userId)
        {
            _logger.LogWarning("Unauthorized access attempt to portfolio {PortfolioId} by user {UserId}", id, userId);
            return NotFound(); // Using NotFound instead of Unauthorized for security
        }

        // Get all unique cryptocurrencies in the portfolio
        var cryptocurrencies = portfolio.Holdings
            .Where(h => h.Cryptocurrency != null)
            .Select(h => h.Cryptocurrency)
            .Distinct()
            .ToList();

        // Pre-fetch all needed prices in one batch
        var coinIds = cryptocurrencies.Select(c => c.CoinGeckoId).ToList();
        try
        {
            // Try to get all prices from cache first
            bool needsFetch = false;
            foreach (var coinId in coinIds)
            {
                if (_priceCache.TryGetPrice(coinId, out var price))
                {
                    var crypto = cryptocurrencies.First(c => c.CoinGeckoId == coinId);
                    crypto.CurrentPrice = price;
                }
                else
                {
                    needsFetch = true;
                    break;
                }
            }

            // If any price is missing, refresh all at once
            if (needsFetch)
            {
                await _priceCache.RefreshAllPricesAsync(coinIds);
                foreach (var crypto in cryptocurrencies)
                {
                    if (_priceCache.TryGetPrice(crypto.CoinGeckoId, out var price))
                    {
                        crypto.CurrentPrice = price;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching cryptocurrency prices");
        }

        // Calculate total value using cached prices
        decimal totalValue = portfolio.Holdings
            .Where(h => h.Cryptocurrency != null)
            .Sum(h => h.Quantity * (h.Cryptocurrency.CurrentPrice > 0 ? h.Cryptocurrency.CurrentPrice : 0));

        ViewData["TotalValue"] = totalValue;
        return View(portfolio);
    }

    // GET: /Portfolio/CalculateValue/5
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> CalculateValue(string id)
    {
        try
        {
            var value = await _portfolioService.CalculatePortfolioValueAsync(id);
            return Json(new { value });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating portfolio value");
            return Json(new { error = "Error calculating portfolio value" });
        }
    }    // GET: /Portfolio/AddHolding/5
    public async Task<IActionResult> AddHolding(string id, int page = 1)
    {
        var portfolio = await _portfolioService.GetPortfolioAsync(id);
        if (portfolio == null)
        {
            return NotFound();
        }

        try
        {
            const int PageSize = 100;
            var allCryptos = (await _cryptoService.GetAllCryptocurrenciesAsync()).ToList();
            
            // Calculate pagination
            var totalPages = (int)Math.Ceiling(allCryptos.Count / (double)PageSize);
            page = Math.Max(1, Math.Min(page, totalPages));
            
            var pagedCryptos = allCryptos
                .OrderBy(c => c.Symbol)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            ViewBag.PortfolioId = id;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.Cryptocurrencies = pagedCryptos;
            ViewBag.TotalCount = allCryptos.Count;

            return View(new Holding { PortfolioId = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading cryptocurrency list for portfolio {PortfolioId}", id);
            TempData["Error"] = "There was an error loading the cryptocurrency list. Please try again.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    // GET: /Portfolio/SearchCryptos
    [HttpGet]
    public async Task<IActionResult> SearchCryptos(string term)
    {
        try
        {
            var cryptos = await _cryptoService.GetAllCryptocurrenciesAsync();
            var matches = cryptos
                .Where(c => c.Symbol.Contains(term, StringComparison.OrdinalIgnoreCase) || 
                           c.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                .Take(10)
                .Select(c => new { id = c.Id, text = $"{c.Symbol} - {c.Name}" })
                .ToList();

            return Json(matches);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching cryptocurrencies with term: {Term}", term);
            return Json(new List<object>());
        }
    }

    // POST: /Portfolio/AddHolding
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddHolding(Holding holding)
    {
        if (ModelState.IsValid)
        {
            await _portfolioService.AddHoldingAsync(holding);
            return RedirectToAction(nameof(Details), new { id = holding.PortfolioId });
        }

        ViewBag.PortfolioId = holding.PortfolioId;
        ViewBag.Cryptocurrencies = await _cryptoService.GetAllCryptocurrenciesAsync();
        return View(holding);
    }

    // GET: /Portfolio/AddTransaction/5
    public async Task<IActionResult> AddTransaction(string id)
    {
        var portfolio = await _portfolioService.GetPortfolioAsync(id);
        if (portfolio == null)
        {
            return NotFound();
        }

        ViewBag.PortfolioId = id;
        ViewBag.Cryptocurrencies = await _cryptoService.GetAllCryptocurrenciesAsync();
        return View(new Transaction { PortfolioId = id });
    }

    // POST: /Portfolio/AddTransaction
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTransaction(Transaction transaction)
    {
        if (ModelState.IsValid)
        {
            await _portfolioService.AddTransactionAsync(transaction);
            return RedirectToAction(nameof(Details), new { id = transaction.PortfolioId });
        }

        ViewBag.PortfolioId = transaction.PortfolioId;
        ViewBag.Cryptocurrencies = await _cryptoService.GetAllCryptocurrenciesAsync();
        return View(transaction);
    }

    // GET: /Portfolio/Transactions/5
    public async Task<IActionResult> Transactions(string id)
    {
        var userId = CurrentUserId;
        if (userId == null)
        {
            _logger.LogWarning("No CurrentUserId found");
            return Challenge();
        }

        var portfolio = await _portfolioService.GetPortfolioDetailsAsync(id);
        if (portfolio == null)
        {
            _logger.LogWarning("Portfolio not found with ID: {PortfolioId}", id);
            return NotFound();
        }

        // Check if portfolio belongs to the current user
        if (portfolio.UserId != userId)
        {
            _logger.LogWarning("Unauthorized access attempt to portfolio {PortfolioId} by user {UserId}", id, userId);
            return NotFound(); // Using NotFound instead of Unauthorized for security
        }

        // Get transactions with related data
        var transactions = portfolio.Transactions
            .OrderByDescending(t => t.Date)
            .ToList();

        foreach (var transaction in transactions)
        {
            if (transaction.Cryptocurrency != null)
            {
                try
                {
                    // Update current prices for display
                    transaction.Cryptocurrency.CurrentPrice = await _cryptoService.GetCurrentPriceAsync(transaction.Cryptocurrency.CoinGeckoId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting current price for {Symbol}", transaction.Cryptocurrency.Symbol);
                }
            }
        }

        return View(transactions);
    }

    // POST: /Portfolio/DeleteTransaction/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTransaction(string id)
    {
        try
        {
            var transaction = await _transactionService.GetTransactionAsync(id);
            if (transaction == null)
            {
                _logger.LogWarning("Transaction not found with ID: {TransactionId}", id);
                return NotFound();
            }

            var userId = CurrentUserId;
            var portfolio = await _portfolioService.GetPortfolioDetailsAsync(transaction.PortfolioId);
            if (portfolio == null || userId != portfolio.UserId)
            {
                _logger.LogWarning("Unauthorized deletion attempt for transaction {TransactionId} by user {UserId}", id, userId);
                return NotFound();
            }

            await _transactionService.DeleteTransactionAsync(id);
            _logger.LogInformation("Transaction {TransactionId} deleted by user {UserId}", id, userId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting transaction {TransactionId}", id);
            return StatusCode(500);
        }
    }

    // POST: /Portfolio/SellCrypto
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SellCrypto(string portfolioId, string cryptoId, decimal amount)
    {
        if (string.IsNullOrEmpty(portfolioId) || string.IsNullOrEmpty(cryptoId) || amount <= 0)
        {
            return BadRequest("Invalid sale parameters");
        }

        try
        {
            // Get current price from cache first
            decimal currentPrice;
            if (!_priceCache.TryGetPrice(cryptoId, out currentPrice))
            {
                // If not in cache, get it and cache it
                currentPrice = await _cryptoService.GetCurrentPriceAsync(cryptoId);
                _priceCache.UpdatePrice(cryptoId, currentPrice, DateTime.UtcNow);
            }

            if (currentPrice <= 0)
            {
                _logger.LogError("Invalid price (0 or negative) for crypto {CryptoId}", cryptoId);
                return BadRequest("Unable to get current price for the cryptocurrency");
            }

            // Create the sell transaction
            await _transactionService.CreateTransactionAsync(
                portfolioId,
                cryptoId,
                TransactionType.Sell,
                amount,
                currentPrice
            );

            // Redirect back to portfolio details
            return RedirectToAction(nameof(Details), new { id = portfolioId });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid sale attempt: {Message}", ex.Message);
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id = portfolioId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing sale for portfolio {PortfolioId}, crypto {CryptoId}, amount {Amount}", 
                portfolioId, cryptoId, amount);
            TempData["Error"] = "An error occurred while processing the sale";
            return RedirectToAction(nameof(Details), new { id = portfolioId });
        }
    }
}
