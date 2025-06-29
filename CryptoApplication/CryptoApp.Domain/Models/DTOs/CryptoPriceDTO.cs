namespace CryptoApp.Domain.Models.DTOs;

public class CryptoPriceDTO
{
    public string Id { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
}
