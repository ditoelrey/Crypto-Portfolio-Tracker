using CryptoApp.Domain.Models.DTOs;

namespace CryptoApp.Application.Interfaces;

public interface ICryptoFetchService 
{
    Task<IEnumerable<CryptoPriceDTO>> GetCryptoPricesAsync(IEnumerable<string> symbols);
    Task<decimal> GetPriceAsync(string symbol);
    Task<IEnumerable<CoinGeckoCoinDTO>> GetSupportedCoinsAsync();
}
