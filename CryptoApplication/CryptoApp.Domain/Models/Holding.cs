namespace CryptoApp.Domain.Models;

public class Holding : BaseEntity
{    public string PortfolioId { get; set; } = string.Empty;
    public string CryptocurrencyId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal PurchasePrice { get; set; }
    public DateTime PurchaseDate { get; set; }
    
    public Portfolio Portfolio { get; set; } = null!;
    public Cryptocurrency Cryptocurrency { get; set; } = null!;

     public decimal CurrentValue => Quantity * (Cryptocurrency?.CurrentPrice ?? 0);
}