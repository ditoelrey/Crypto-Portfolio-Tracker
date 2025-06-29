using CryptoApp.Domain.Models;

namespace CryptoApp.Application.Interfaces;

public interface IPortfolioService
{
    Task<Portfolio> CreatePortfolioAsync(string name, string userId);
    Task<IEnumerable<Portfolio>> GetPortfoliosAsync();
    Task<Portfolio?> GetPortfolioAsync(string portfolioId);
    Task<decimal> CalculatePortfolioValueAsync(string portfolioId);
    Task UpdatePortfolioAsync(Portfolio portfolio);
    Task DeletePortfolioAsync(string portfolioId);
    Task AddHoldingAsync(Holding holding);
    Task UpdateHoldingAsync(Holding holding);
    Task AddTransactionAsync(Transaction transaction);
    Task<IEnumerable<Transaction>> GetPortfolioTransactionsAsync(string portfolioId);
    Task<Portfolio?> GetPortfolioDetailsAsync(string portfolioId);
}
