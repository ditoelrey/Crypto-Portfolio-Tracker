namespace CryptoApp.Domain.Models;

public class Cryptocurrency : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string CoinGeckoId { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public ICollection<Holding> Holdings { get; set; } = new List<Holding>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
