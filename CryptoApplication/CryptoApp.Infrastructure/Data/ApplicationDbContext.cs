using CryptoApp.Application.Interfaces;
using CryptoApp.Domain.Models;
using CryptoApp.Domain.Models.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CryptoApp.Infrastructure.Data
{    
    public class ApplicationDbContext : IdentityDbContext<CryptoApplicationUser>, IApplicationDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }        
        public DbSet<Portfolio> Portfolios { get; set; } = null!;
        public DbSet<Cryptocurrency> Cryptocurrencies { get; set; } = null!;
        public DbSet<Holding> Holdings { get; set; } = null!;
        public DbSet<Transaction> Transactions { get; set; } = null!;
     
        public new DbSet<CryptoApplicationUser> Users { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);            
            modelBuilder.Entity<CryptoApplicationUser>(entity =>
            {
                entity.HasOne(u => u.Portfolio)
                    .WithOne(p => p.User)
                    .HasForeignKey<Portfolio>(p => p.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            
            modelBuilder.Entity<Portfolio>(entity =>
            {
                entity.HasMany(p => p.Holdings)
                    .WithOne(h => h.Portfolio)
                    .HasForeignKey(h => h.PortfolioId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(p => p.Transactions)
                    .WithOne(t => t.Portfolio)
                    .HasForeignKey(t => t.PortfolioId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            
            modelBuilder.Entity<Cryptocurrency>()
                .Property(c => c.CurrentPrice)
                .HasPrecision(18, 8);

            modelBuilder.Entity<Transaction>()
                .Property(t => t.Amount)
                .HasPrecision(18, 8);

            modelBuilder.Entity<Transaction>()
                .Property(t => t.PriceAtTime)
                .HasPrecision(18, 8);

            modelBuilder.Entity<Holding>()
                .Property(h => h.Quantity)
                .HasPrecision(18, 8);
        }
    }
}
