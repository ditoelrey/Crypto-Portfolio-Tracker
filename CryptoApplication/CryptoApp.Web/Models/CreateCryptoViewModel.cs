using CryptoApp.Domain.Models;

namespace CryptoApp.Web.Models;

public class CreateCryptoViewModel
{
    public required string Name { get; set; }
    public required string Symbol { get; set; }
    public required string CoinGeckoId { get; set; }
}
