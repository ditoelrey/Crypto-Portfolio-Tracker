using CryptoApp.Application.Interfaces;
using CryptoApp.Domain.Models;
using CryptoApp.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace CryptoApp.Application.Services;

public class CryptocurrencyService : ICryptocurrencyService
{    
    private readonly IRepository<Cryptocurrency> _repository;
    private readonly ICryptoFetchService _fetchService;
    private readonly ICryptoPriceCacheService _cacheService;
    private readonly ILogger<CryptocurrencyService> _logger;

    public CryptocurrencyService(
        IRepository<Cryptocurrency> repository,
        ICryptoFetchService fetchService,
        ICryptoPriceCacheService cacheService,
        ILogger<CryptocurrencyService> logger)
    {
        _repository = repository;
        _fetchService = fetchService;
        _cacheService = cacheService;
        _logger = logger;
    }    

    public async Task<Cryptocurrency?> GetCryptocurrencyAsync(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            throw new ArgumentException("Id cannot be null or empty", nameof(id));
        }
        
        return await _repository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<Cryptocurrency>> GetAllCryptocurrenciesAsync()
    {
        try
        {
            
            return await _repository.GetAllAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching cryptocurrency list");
            return new List<Cryptocurrency>();
        }
    }    

    public async Task<decimal> GetCurrentPriceAsync(string coinGeckoId)
    {
        try
        {
            if (_cacheService.TryGetPrice(coinGeckoId, out var cachedPrice))
            {
                if (!_cacheService.ShouldRefreshPrice(coinGeckoId))
                {
                    return cachedPrice;
                }
            }
            
            await _cacheService.RefreshPriceAsync(coinGeckoId);
            return _cacheService.TryGetPrice(coinGeckoId, out var price) ? price : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current price for coin {CoinGeckoId}", coinGeckoId);
            return _cacheService.TryGetPrice(coinGeckoId, out var fallbackPrice) ? fallbackPrice : 0;
        }
    }

    public async Task UpdatePricesAsync()
    {
        try
        {
            var cryptos = await GetAllCryptocurrenciesAsync();
            if (!cryptos.Any())
            {
                _logger.LogWarning("No cryptocurrencies found in database");
                return;
            }

            var coinIds = cryptos.Select(c => c.CoinGeckoId).ToList();
            _logger.LogInformation("Updating prices for coins: {CoinIds}", string.Join(", ", coinIds));

            var success = await _cacheService.RefreshAllPricesAsync(coinIds);
            if (!success)
            {
                _logger.LogWarning("Failed to refresh prices from API, using cached values");
            }

            foreach (var crypto in cryptos)
            {
                try
                {
                    if (_cacheService.TryGetPrice(crypto.CoinGeckoId, out var price))
                    {
                        crypto.CurrentPrice = price;
                        _logger.LogInformation("Updating {Symbol} ({CoinId}) price to: ${Price}", 
                            crypto.Symbol, crypto.CoinGeckoId, crypto.CurrentPrice);
                        await _repository.UpdateAsync(crypto);
                    }
                    else
                    {
                        _logger.LogWarning("No price found for {Symbol} ({CoinId})", 
                            crypto.Symbol, crypto.CoinGeckoId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating price for {Symbol}", crypto.Symbol);
                    continue;
                }
            }
            
            await _repository.SaveChangesAsync();
            _logger.LogInformation("Price update completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during price update");
            throw;
        }
    }

    public async Task AddCryptocurrencyAsync(Cryptocurrency cryptocurrency)
    {
        if (cryptocurrency == null)
            throw new ArgumentNullException(nameof(cryptocurrency));

        var existing = (await _repository.GetAllAsync())
            .FirstOrDefault(c => c.Symbol.Equals(cryptocurrency.Symbol, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
            throw new InvalidOperationException($"A cryptocurrency with symbol {cryptocurrency.Symbol} already exists.");

        await _repository.AddAsync(cryptocurrency);
        await _repository.SaveChangesAsync();
    }

    public async Task DeleteCryptocurrencyAsync(String id)
    {
        var cryptocurrency = await _repository.GetByIdAsync(id);
        if (cryptocurrency == null)
            throw new InvalidOperationException($"Cryptocurrency with ID {id} not found");

        await _repository.DeleteAsync(cryptocurrency);
        await _repository.SaveChangesAsync();
    }
}
