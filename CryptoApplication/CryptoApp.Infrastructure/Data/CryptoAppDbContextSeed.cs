using CryptoApp.Domain.Models;
using CryptoApp.Domain.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CryptoApp.Infrastructure.Data;

public static class CryptoAppDbContextSeed
{
    public static async Task SeedDatabaseAsync(
        ApplicationDbContext context,
        UserManager<CryptoApplicationUser> userManager,
        ILogger logger)
    {
        try
        {
            // Check if we need to apply migrations
            if (context.Database.GetPendingMigrations().Any())
            {
                logger.LogInformation("Applying pending migrations...");
                await context.Database.MigrateAsync();
                logger.LogInformation("Migrations applied successfully.");
            }

            // Seed cryptocurrencies if none exist
            if (!await context.Set<Cryptocurrency>().AnyAsync())
            {
                logger.LogInformation("Seeding cryptocurrency data...");
                var cryptocurrencies = new List<Cryptocurrency>
                {
                    new() { 
                        Id = Guid.NewGuid().ToString(),
                        Name = "Bitcoin",
                        Symbol = "BTC",
                        CoinGeckoId = "bitcoin",
                        CurrentPrice = 0
                    },
                    new() {
                        Id = Guid.NewGuid().ToString(),
                        Name = "Ethereum",
                        Symbol = "ETH",
                        CoinGeckoId = "ethereum",
                        CurrentPrice = 0
                    },
                    new() {
                        Id = Guid.NewGuid().ToString(),
                        Name = "Binance Coin",
                        Symbol = "BNB",
                        CoinGeckoId = "binancecoin",
                        CurrentPrice = 0
                    },
                    new() {
                        Id = Guid.NewGuid().ToString(),
                        Name = "Cardano",
                        Symbol = "ADA",
                        CoinGeckoId = "cardano",
                        CurrentPrice = 0
                    },
                    new() {
                        Id = Guid.NewGuid().ToString(),
                        Name = "Solana",
                        Symbol = "SOL",
                        CoinGeckoId = "solana",
                        CurrentPrice = 0
                    }
                };

                await context.Set<Cryptocurrency>().AddRangeAsync(cryptocurrencies);
                await context.SaveChangesAsync();
                logger.LogInformation("Cryptocurrency seed data applied successfully.");
            }

            // Export existing data if needed
            var existingData = new
            {
                Cryptocurrencies = await context.Set<Cryptocurrency>().ToListAsync(),
                Users = await context.Users
                    .Include(u => u.Portfolio)
                        .ThenInclude(p => p.Holdings)
                    .Include(u => u.Portfolio)
                        .ThenInclude(p => p.Transactions)
                    .ToListAsync()
            };

            logger.LogInformation("Database is seeded and ready.");
            logger.LogInformation("Current database status: {0} cryptocurrencies, {1} users", 
                existingData.Cryptocurrencies.Count, 
                existingData.Users.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }
}
