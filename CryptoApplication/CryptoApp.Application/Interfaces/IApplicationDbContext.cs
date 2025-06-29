using CryptoApp.Domain.Models;
using CryptoApp.Domain.Models.Identity;
using Microsoft.EntityFrameworkCore;

namespace CryptoApp.Application.Interfaces
{
    public interface IApplicationDbContext
    {
        DbSet<Portfolio> Portfolios { get; }
        DbSet<Cryptocurrency> Cryptocurrencies { get; }
        DbSet<Holding> Holdings { get; }
        DbSet<Transaction> Transactions { get; }
        DbSet<CryptoApplicationUser> Users { get; }
        
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
