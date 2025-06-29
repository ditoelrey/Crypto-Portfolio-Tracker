using CryptoApp.Application.Interfaces;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace CryptoApp.Infrastructure.Services;

public class CryptoPriceCacheService : ICryptoPriceCacheService
{
    private readonly ICryptoFetchService _fetchService;
    private readonly ILogger<CryptoPriceCacheService> _logger;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(10);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private class CacheEntry
    {
        public decimal Price { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public CryptoPriceCacheService(
        ICryptoFetchService fetchService,
        ILogger<CryptoPriceCacheService> logger)
    {
        _fetchService = fetchService;
        _logger = logger;
        _cache = new ConcurrentDictionary<string, CacheEntry>();
    }

    public bool TryGetPrice(string coinId, out decimal price)
    {
        if (_cache.TryGetValue(coinId, out var entry))
        {
            price = entry.Price;
            return true;
        }
        price = 0;
        return false;
    }

    public bool ShouldRefreshPrice(string coinId)
    {
        if (!_cache.TryGetValue(coinId, out var entry))
        {
            return true;
        }

        var age = DateTime.UtcNow - entry.LastUpdated;
        return age >= _cacheExpiration;
    }

    public DateTime? GetLastUpdateTime(string coinId)
    {
        if (_cache.TryGetValue(coinId, out var entry))
        {
            return entry.LastUpdated;
        }
        return null;
    }

    public void Clear()
    {
        _cache.Clear();
    }

    public void UpdatePrice(string coinId, decimal price, DateTime timestamp)
    {
        _cache.AddOrUpdate(
            coinId,
            new CacheEntry { Price = price, LastUpdated = timestamp },
            (_, _) => new CacheEntry { Price = price, LastUpdated = timestamp }
        );
    }

    public async Task<bool> RefreshPriceAsync(string coinId)
    {
       
        if (!ShouldRefreshPrice(coinId) && TryGetPrice(coinId, out _))
        {
            return true;
        }

        try
        {
            await _refreshLock.WaitAsync();
            try
            {
                var price = await _fetchService.GetPriceAsync(coinId);
                if (price > 0)
                {
                    UpdatePrice(coinId, price, DateTime.UtcNow);
                    _logger.LogInformation("Refreshed price for {CoinId}: ${Price}", coinId, price);
                    return true;
                }
                return false;
            }
            finally
            {
                _refreshLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing price for {CoinId}", coinId);
            return false;
        }
    }

    public async Task<bool> RefreshAllPricesAsync(IEnumerable<string> coinIds)
    {
        try
        {
            await _refreshLock.WaitAsync();
            try
            {
                
                var staleCoins = coinIds.Where(id => ShouldRefreshPrice(id)).ToList();
                
                if (!staleCoins.Any())
                {
                    return true; 
                }

                var prices = await _fetchService.GetCryptoPricesAsync(staleCoins);
                var now = DateTime.UtcNow;
                var updated = false;

                foreach (var price in prices)
                {
                    if (price.CurrentPrice > 0)
                    {
                        UpdatePrice(price.Id, price.CurrentPrice, now);
                        updated = true;
                    }
                }

                if (updated)
                {
                    _logger.LogInformation("Refreshed prices for {Count} coins", prices.Count());
                }
                return updated;
            }
            finally
            {
                _refreshLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk price refresh");
            return false;
        }
    }
}
