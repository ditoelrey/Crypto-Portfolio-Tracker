using CryptoApp.Domain.Models;

namespace CryptoApp.Application.Interfaces;

public interface ICryptoPriceCacheService
{
    bool TryGetPrice(string coinId, out decimal price);
    void UpdatePrice(string coinId, decimal price, DateTime timestamp);
    bool ShouldRefreshPrice(string coinId);
    Task<bool> RefreshPriceAsync(string coinId);
    Task<bool> RefreshAllPricesAsync(IEnumerable<string> coinIds);
    DateTime? GetLastUpdateTime(string coinId);
    void Clear();
}
