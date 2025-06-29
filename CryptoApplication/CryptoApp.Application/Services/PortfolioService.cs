using CryptoApp.Application.Interfaces;
using CryptoApp.Domain.Models;
using CryptoApp.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace CryptoApp.Application.Services;

public class PortfolioService : IPortfolioService
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly ICryptocurrencyService _cryptoService;
    private readonly ICryptoPriceCacheService _cacheService;
    private readonly ILogger<PortfolioService> _logger;

    public PortfolioService(
        IPortfolioRepository portfolioRepository, 
        ICryptocurrencyService cryptoService,
        ICryptoPriceCacheService cacheService,
        ILogger<PortfolioService> logger)
    {
        _portfolioRepository = portfolioRepository;
        _cryptoService = cryptoService;
        _cacheService = cacheService;
        _logger = logger;
    }    public async Task<Portfolio> CreatePortfolioAsync(string name, string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException("User ID is required when creating a portfolio", nameof(userId));
        }

        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Portfolio name is required", nameof(name));
        }

        var portfolio = new Portfolio
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            UserId = userId,
            Holdings = new List<Holding>(),
            Transactions = new List<Transaction>()
        };

        await _portfolioRepository.AddAsync(portfolio);
        await _portfolioRepository.SaveChangesAsync();
        _logger.LogInformation("Created new portfolio {PortfolioId} for user {UserId}", portfolio.Id, userId);
        return portfolio;
    }

    public async Task<IEnumerable<Portfolio>> GetPortfoliosAsync()
    {
        return await _portfolioRepository.GetAllAsync();
    }

    public async Task<Portfolio?> GetPortfolioAsync(string portfolioId)
    {
        if (string.IsNullOrEmpty(portfolioId))
        {
            throw new ArgumentException("Portfolio ID is required", nameof(portfolioId));
        }

        return await _portfolioRepository.GetByIdAsync(portfolioId);
    }    public async Task<decimal> CalculatePortfolioValueAsync(string portfolioId)
    {
        if (string.IsNullOrEmpty(portfolioId))
        {
            throw new ArgumentException("Portfolio ID is required", nameof(portfolioId));
        }

        var portfolio = await _portfolioRepository.GetPortfolioWithDetailsAsync(portfolioId);
        if (portfolio == null)
        {
            _logger.LogWarning("Portfolio {PortfolioId} not found", portfolioId);
            return 0;
        }

        
        var cryptocurrencies = portfolio.Holdings
            .Where(h => h.Cryptocurrency != null)
            .Select(h => h.Cryptocurrency)
            .Distinct()
            .ToList();

        if (!cryptocurrencies.Any())
        {
            return 0;
        }

        decimal totalValue = 0;
        var prices = new Dictionary<string, decimal>();

      
        var coinIds = cryptocurrencies.Select(c => c.CoinGeckoId).ToList();
        try
        {
            
            bool needsFetch = false;
            foreach (var coinId in coinIds)
            {
                if (_cacheService.TryGetPrice(coinId, out var price))
                {
                    prices[coinId] = price;
                }
                else
                {
                    needsFetch = true;
                    break;
                }
            }

           
            if (needsFetch)
            {
                await _cacheService.RefreshAllPricesAsync(coinIds);
                foreach (var coinId in coinIds)
                {
                    if (_cacheService.TryGetPrice(coinId, out var price))
                    {
                        prices[coinId] = price;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching cryptocurrency prices");
        }

        
        foreach (var holding in portfolio.Holdings)
        {
            if (holding.Cryptocurrency == null)
            {
                _logger.LogWarning("Holding in portfolio {PortfolioId} has no associated cryptocurrency", portfolioId);
                continue;
            }

            if (prices.TryGetValue(holding.Cryptocurrency.CoinGeckoId, out var currentPrice))
            {
                holding.Cryptocurrency.CurrentPrice = currentPrice;
                var holdingValue = holding.Quantity * currentPrice;
                totalValue += holdingValue;
                _logger.LogTrace("Calculated value for {Symbol}: {Quantity} * ${Price} = ${Value}", 
                    holding.Cryptocurrency.Symbol, holding.Quantity, currentPrice, holdingValue);
            }
            else if (holding.Cryptocurrency.CurrentPrice > 0)
            {
                var holdingValue = holding.Quantity * holding.Cryptocurrency.CurrentPrice;
                totalValue += holdingValue;
                _logger.LogWarning("Used last known price for {Symbol}: ${Price}", 
                    holding.Cryptocurrency.Symbol, holding.Cryptocurrency.CurrentPrice);
            }
            else
            {
                _logger.LogError("No price available for {Symbol}", holding.Cryptocurrency.Symbol);
            }
        }

        return Math.Round(totalValue, 2);
    }

    public async Task UpdatePortfolioAsync(Portfolio portfolio)
    {
        if (portfolio == null)
        {
            throw new ArgumentNullException(nameof(portfolio));
        }

        await _portfolioRepository.UpdateAsync(portfolio);
        await _portfolioRepository.SaveChangesAsync();
        _logger.LogInformation("Updated portfolio {PortfolioId}", portfolio.Id);
    }

    public async Task DeletePortfolioAsync(string portfolioId)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(portfolioId);
        if (portfolio != null)
        {
            await _portfolioRepository.DeleteAsync(portfolio);
            await _portfolioRepository.SaveChangesAsync();
        }
    }

    public async Task AddHoldingAsync(Holding holding)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(holding.PortfolioId);
        if (portfolio != null)
        {
            portfolio.Holdings.Add(holding);
            await _portfolioRepository.SaveChangesAsync();
        }
    }

    public async Task UpdateHoldingAsync(Holding holding)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(holding.PortfolioId);
        if (portfolio != null)
        {
            var existingHolding = portfolio.Holdings.FirstOrDefault(h => h.Id == holding.Id);
            if (existingHolding != null)
            {
                existingHolding.Quantity = holding.Quantity;
                await _portfolioRepository.SaveChangesAsync();
            }
        }
    }    public async Task AddTransactionAsync(Transaction transaction)
    {
        var portfolio = await _portfolioRepository.GetPortfolioWithDetailsAsync(transaction.PortfolioId);
        if (portfolio == null)
            throw new InvalidOperationException("Portfolio not found");

        
        var holding = portfolio.Holdings.FirstOrDefault(h => h.CryptocurrencyId == transaction.CryptocurrencyId);

        if (transaction.Type == TransactionType.Buy)
        {
            if (holding == null)
            {
                holding = new Holding
                {
                    Id = Guid.NewGuid().ToString(),
                    PortfolioId = portfolio.Id,
                    CryptocurrencyId = transaction.CryptocurrencyId,
                    Quantity = transaction.Amount,
                    PurchasePrice = transaction.PriceAtTime,
                    PurchaseDate = transaction.Date
                };
                portfolio.Holdings.Add(holding);
            }
            else
            {
                
                var totalQuantity = holding.Quantity + transaction.Amount;
                var totalValue = (holding.Quantity * holding.PurchasePrice) + (transaction.Amount * transaction.PriceAtTime);
                
                holding.Quantity = totalQuantity;
                holding.PurchasePrice = totalValue / totalQuantity;
                holding.PurchaseDate = transaction.Date; 
            }
        }
        else 
        {
            if (holding == null)
                throw new InvalidOperationException("Cannot sell cryptocurrency that is not in portfolio");
            
            if (holding.Quantity < transaction.Amount)
                throw new InvalidOperationException($"Insufficient balance for sell transaction. Available: {holding.Quantity}, Attempted to sell: {transaction.Amount}");

            holding.Quantity -= transaction.Amount;
            
        
            if (holding.Quantity <= 0)
            {
                portfolio.Holdings.Remove(holding);
            }
        }

        
        transaction.Id = Guid.NewGuid().ToString();
        transaction.Date = DateTime.UtcNow;
        portfolio.Transactions.Add(transaction);

        
        await _portfolioRepository.SaveChangesAsync();
    }

    public async Task<IEnumerable<Transaction>> GetPortfolioTransactionsAsync(string portfolioId)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(portfolioId);
        return portfolio?.Transactions ?? new List<Transaction>();
    }

    public async Task<Portfolio?> GetPortfolioDetailsAsync(string portfolioId)
    {
        return await _portfolioRepository.GetPortfolioWithDetailsAsync(portfolioId);
    }
}
