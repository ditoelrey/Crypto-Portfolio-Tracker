using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using CryptoApp.Application.Interfaces;
using CryptoApp.Domain.Models;
using CryptoApp.Web.Models;
using Microsoft.AspNetCore.Authorization;

namespace CryptoApp.Web.Controllers;

public class CryptoController : Controller
{
    private readonly ICryptocurrencyService _cryptoService;
    private readonly IPortfolioService _portfolioService;
    private readonly ICryptoFetchService _cryptoFetchService;
    private readonly IUserService _userService;
    private readonly ILogger<CryptoController> _logger;

    private string? CurrentUserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    public CryptoController(
        ICryptocurrencyService cryptoService, 
        IPortfolioService portfolioService,
        ICryptoFetchService cryptoFetchService,
        IUserService userService,
        ILogger<CryptoController> logger)
    {
        _cryptoService = cryptoService;
        _portfolioService = portfolioService;
        _cryptoFetchService = cryptoFetchService;
        _userService = userService;
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

    // GET: /Crypto
    public async Task<IActionResult> Index()
    {
        try 
        {
            var userId = CurrentUserId;
            if (userId == null)
            {
                return Challenge();
            }

            var user = await _userService.GetUserWithPortfolioAsync(userId);
            if (user?.Portfolio?.Id != null)
            {
                ViewData["PortfolioId"] = user.Portfolio.Id;
            }

            var cryptocurrencies = await _cryptoService.GetAllCryptocurrenciesAsync();
            
           
            try 
            {
                await _cryptoService.UpdatePricesAsync();
               
                cryptocurrencies = await _cryptoService.GetAllCryptocurrenciesAsync();
            }
            catch (Exception ex)
            {
                
                _logger.LogError(ex, "Error updating prices");
            }

            return View(cryptocurrencies);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Index");
            return View(Enumerable.Empty<Cryptocurrency>());
        }
    }

    // GET: /Crypto/Create
    public async Task<IActionResult> Create()
    {
        try
        {
            var supportedCoins = await _cryptoFetchService.GetSupportedCoinsAsync();
            var existingCoins = await _cryptoService.GetAllCryptocurrenciesAsync();
            var existingCoinIds = existingCoins.Select(c => c.CoinGeckoId).ToHashSet();
            
           
            var availableCoins = supportedCoins
                .Where(c => !existingCoinIds.Contains(c.Id))
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem 
                { 
                    Value = c.Id,
                    Text = $"{c.Name} ({c.Symbol.ToUpper()})"
                });            ViewBag.SupportedCoins = availableCoins.ToList();
            return View(new CreateCryptoViewModel 
            { 
                Name = string.Empty,
                Symbol = string.Empty,
                CoinGeckoId = string.Empty
            });
        }        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading available cryptocurrencies in Create GET action");
            
            if (ex is HttpRequestException httpEx)
            {
                ModelState.AddModelError("", $"Error connecting to CoinGecko API: {httpEx.Message}");
            }
            else
            {
                ModelState.AddModelError("", "Error loading available cryptocurrencies. Please try again later.");
            }

            return View(new CreateCryptoViewModel 
            { 
                Name = string.Empty,
                Symbol = string.Empty,
                CoinGeckoId = string.Empty
            });
        }
    }

    // POST: /Crypto/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCryptoViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var supportedCoins = await _cryptoFetchService.GetSupportedCoinsAsync();
            ViewBag.SupportedCoins = supportedCoins
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem 
                { 
                    Value = c.Id,
                    Text = $"{c.Name} ({c.Symbol.ToUpper()})"
                });
            return View(model);
        }

        try
        {
            var cryptocurrency = new Cryptocurrency
            {
                Id = Guid.NewGuid().ToString(),
                Name = model.Name,
                Symbol = model.Symbol,
                CoinGeckoId = model.CoinGeckoId,
                CreatedAt = DateTime.UtcNow
            };

            
            await _cryptoService.AddCryptocurrencyAsync(cryptocurrency);

           
            try
            {
                cryptocurrency.CurrentPrice = await _cryptoService.GetCurrentPriceAsync(cryptocurrency.CoinGeckoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting initial price for {Symbol} ({CoinGeckoId})", 
                    cryptocurrency.Symbol, cryptocurrency.CoinGeckoId);
                
            }

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding cryptocurrency: {Message}", ex.Message);
            ModelState.AddModelError("", "Error adding cryptocurrency. Please try again later.");
            
            var supportedCoins = await _cryptoFetchService.GetSupportedCoinsAsync();
            ViewBag.SupportedCoins = supportedCoins
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem 
                { 
                    Value = c.Id,
                    Text = $"{c.Name} ({c.Symbol.ToUpper()})"
                });
            return View(model);
        }
    }

    // GET: /Crypto/Details/5
    public async Task<IActionResult> Details(string id)
    {
        var userId = CurrentUserId;
        if (userId == null)
        {
            return Challenge();
        }

        var user = await _userService.GetUserWithPortfolioAsync(userId);
        if (user?.Portfolio?.Id != null)
        {
            ViewData["PortfolioId"] = user.Portfolio.Id;
        }

        var crypto = await _cryptoService.GetCryptocurrencyAsync(id);
        if (crypto == null)
        {
            return NotFound();
        }

        try
        {            crypto.CurrentPrice = await _cryptoService.GetCurrentPriceAsync(crypto.CoinGeckoId);
            return View(crypto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current price: {Message}", ex.Message);
            return View(crypto);
        }
    }

    // GET: /Crypto/Buy/5?portfolioId=1
    public async Task<IActionResult> Buy(string id, string portfolioId)
    {
        if (!await VerifyPortfolioAccess(portfolioId))
        {
            return NotFound();
        }

        var crypto = await _cryptoService.GetCryptocurrencyAsync(id);
        if (crypto == null)
        {
            _logger.LogWarning("Cryptocurrency not found with ID: {CryptoId}", id);
            return NotFound();
        }        ViewBag.PortfolioId = portfolioId;
        try
        {
            ViewBag.CurrentPrice = await _cryptoService.GetCurrentPriceAsync(crypto.CoinGeckoId);
            return View(crypto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current price for {Symbol} ({CoinGeckoId})", crypto.Symbol, crypto.CoinGeckoId);
            ViewBag.CurrentPrice = 0;
            ModelState.AddModelError("", "Unable to fetch current price. Please try again.");
            return View(crypto);
        }
    }

    // POST: /Crypto/Buy/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Buy(string id, string portfolioId, decimal quantity, decimal price)
    {
        if (!await VerifyPortfolioAccess(portfolioId))
        {
            return NotFound();
        }

        var crypto = await _cryptoService.GetCryptocurrencyAsync(id);
        if (crypto == null)
        {
            _logger.LogWarning("Cryptocurrency not found with ID: {CryptoId}", id);
            return NotFound();
        }

        if (quantity <= 0)
        {
            ModelState.AddModelError("quantity", "Quantity must be greater than 0");
        }

        if (price <= 0)
        {
            ModelState.AddModelError("price", "Price must be greater than 0");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.PortfolioId = portfolioId;
            ViewBag.CurrentPrice = await _cryptoService.GetCurrentPriceAsync(crypto.CoinGeckoId);
            return View(crypto);
        }

        try
        {
            var transaction = new Transaction
            {
                PortfolioId = portfolioId,
                CryptocurrencyId = id,
                Type = TransactionType.Buy,
                Amount = quantity,
                PriceAtTime = price,
                Date = DateTime.UtcNow
            };

            await _portfolioService.AddTransactionAsync(transaction);

            _logger.LogInformation(
                "User {UserId} bought {Amount} {Symbol} at ${Price} for portfolio {PortfolioId}",
                CurrentUserId,
                quantity,
                crypto.Symbol,
                price,
                portfolioId);

           
            ViewData["PortfolioId"] = portfolioId;
            return RedirectToAction("Index");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid buy transaction attempt");
            ModelState.AddModelError("", ex.Message);
            ViewBag.PortfolioId = portfolioId;
            ViewBag.CurrentPrice = await _cryptoService.GetCurrentPriceAsync(crypto.Symbol);
            return View(crypto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing buy transaction");
            ModelState.AddModelError("", "An error occurred while processing your transaction. Please try again.");
            ViewBag.PortfolioId = portfolioId;
            ViewBag.CurrentPrice = await _cryptoService.GetCurrentPriceAsync(crypto.Symbol);
            return View(crypto);
        }
    }

    // GET: /Crypto/Sell/5?portfolioId=1
    public async Task<IActionResult> Sell(string id, string portfolioId)
    {
        if (!await VerifyPortfolioAccess(portfolioId))
        {
            return NotFound();
        }

        var crypto = await _cryptoService.GetCryptocurrencyAsync(id);
        if (crypto == null)
        {
            _logger.LogWarning("Cryptocurrency not found with ID: {CryptoId}", id);
            return NotFound();
        }

        var portfolio = await _portfolioService.GetPortfolioDetailsAsync(portfolioId);
        var holding = portfolio?.Holdings.FirstOrDefault(h => h.CryptocurrencyId == id);
        if (holding == null)
        {
            _logger.LogWarning("No holding found for crypto {CryptoId} in portfolio {PortfolioId}", id, portfolioId);
            return RedirectToAction("Details", "Portfolio", new { id = portfolioId });
        }

        ViewBag.PortfolioId = portfolioId;
        ViewBag.CurrentHolding = holding.Quantity;

        try
        {
            ViewBag.CurrentPrice = await _cryptoService.GetCurrentPriceAsync(crypto.Symbol);
            return View(crypto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current price for {Symbol}", crypto.Symbol);
            ViewBag.CurrentPrice = 0;
            ModelState.AddModelError("", "Unable to fetch current price. Please try again.");
            return View(crypto);
        }
    }    // POST: /Crypto/Sell/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sell(string id, string portfolioId, decimal quantity)
    {
        if (!await VerifyPortfolioAccess(portfolioId))
        {
            return NotFound();
        }

        var crypto = await _cryptoService.GetCryptocurrencyAsync(id);
        if (crypto == null)
        {
            _logger.LogWarning("Cryptocurrency not found with ID: {CryptoId}", id);
            return NotFound();
        }

        var portfolio = await _portfolioService.GetPortfolioDetailsAsync(portfolioId);
        var holding = portfolio?.Holdings.FirstOrDefault(h => h.CryptocurrencyId == id);
        if (holding == null)
        {
            _logger.LogWarning("No holding found for crypto {CryptoId} in portfolio {PortfolioId}", id, portfolioId);
            ModelState.AddModelError("", "You don't have any holdings of this cryptocurrency to sell.");
            ViewBag.PortfolioId = portfolioId;
            ViewBag.CurrentHolding = 0;
            return View(crypto);
        }

        
        if (Math.Floor(quantity) != quantity)
        {
            ModelState.AddModelError("quantity", "You can only sell whole coins (no decimals)");
        }

        if (quantity <= 0)
        {
            ModelState.AddModelError("quantity", "Quantity must be greater than 0");
        }

        if (quantity > holding.Quantity)
        {
            ModelState.AddModelError("quantity", $"You can only sell up to {Math.Floor(holding.Quantity)} coins");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.PortfolioId = portfolioId;
            ViewBag.CurrentHolding = Math.Floor(holding.Quantity);
            return View(crypto);
        }

        try
        {
            
            var currentPrice = await _cryptoService.GetCurrentPriceAsync(crypto.CoinGeckoId);
            
            var transaction = new Transaction
            {
                PortfolioId = portfolioId,
                CryptocurrencyId = id,
                Type = TransactionType.Sell,
                Amount = quantity,
                PriceAtTime = currentPrice,
                Date = DateTime.UtcNow
            };            await _portfolioService.AddTransactionAsync(transaction);

            _logger.LogInformation(
                "User {UserId} sold {Amount} {Symbol} from portfolio {PortfolioId}",
                CurrentUserId,
                quantity,
                crypto.Symbol,
                portfolioId);

            return RedirectToAction("Details", "Portfolio", new { id = portfolioId });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid sell transaction attempt");
            ModelState.AddModelError("", ex.Message);
            ViewBag.PortfolioId = portfolioId;
            ViewBag.CurrentHolding = Math.Floor(holding.Quantity);
            return View(crypto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing sell transaction");
            ModelState.AddModelError("", "An error occurred while processing your transaction. Please try again.");
            ViewBag.PortfolioId = portfolioId;
            ViewBag.CurrentHolding = Math.Floor(holding.Quantity);
            return View(portfolio);
        }
    }    // GET: /Crypto/GetPrice/5
    [HttpGet]
    public async Task<JsonResult> GetPrice(string id)
    {
        try
        {
            var crypto = await _cryptoService.GetCryptocurrencyAsync(id);
            if (crypto == null)
                return Json(new { error = "Cryptocurrency not found", success = false });
            
            var price = await _cryptoService.GetCurrentPriceAsync(crypto.CoinGeckoId);
            return Json(new { price, success = true });
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message, success = false });
        }
    }

    // GET: /Crypto/Refresh
    [HttpGet]
    public async Task<IActionResult> Refresh()
    {
        try
        {
            await _cryptoService.UpdatePricesAsync();
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error refreshing prices: {ex.Message}");
            return RedirectToAction(nameof(Index));
        }
    }

    // GET: /Crypto/Delete/5
    public async Task<IActionResult> Delete(string id)
    {
        var cryptocurrency = await _cryptoService.GetCryptocurrencyAsync(id);
        if (cryptocurrency == null)
        {
            return NotFound();
        }

        return View(cryptocurrency);
    }

    // POST: /Crypto/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        try
        {
            await _cryptoService.DeleteCryptocurrencyAsync(id);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting cryptocurrency {id}");
            return RedirectToAction(nameof(Index));
        }
    }
}
