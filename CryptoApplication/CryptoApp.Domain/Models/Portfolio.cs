using CryptoApp.Domain.Models.Identity;

namespace CryptoApp.Domain.Models;

public class Portfolio : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public virtual CryptoApplicationUser User { get; set; } = null!;
    public virtual ICollection<Holding> Holdings { get; set; } = new List<Holding>();
    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
