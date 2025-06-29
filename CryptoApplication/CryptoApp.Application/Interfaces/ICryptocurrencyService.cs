using CryptoApp.Domain.Models;

namespace CryptoApp.Application.Interfaces;

public interface ICryptocurrencyService
{    Task<Cryptocurrency?> GetCryptocurrencyAsync(string id);
    Task<IEnumerable<Cryptocurrency>> GetAllCryptocurrenciesAsync();
    Task<decimal> GetCurrentPriceAsync(string symbol);
    Task UpdatePricesAsync();
    Task AddCryptocurrencyAsync(Cryptocurrency cryptocurrency);
    Task DeleteCryptocurrencyAsync(string id);
}
