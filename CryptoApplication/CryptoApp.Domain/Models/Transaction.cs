namespace CryptoApp.Domain.Models;

public enum TransactionType
{
    Buy,
    Sell
}

public class Transaction : BaseEntity
{    public string PortfolioId { get; set; } = string.Empty;
    public string CryptocurrencyId { get; set; } = string.Empty;
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public decimal PriceAtTime { get; set; }
    public DateTime Date { get; set; }
    
    public Portfolio Portfolio { get; set; } = null!;
    public Cryptocurrency Cryptocurrency { get; set; } = null!;
}
