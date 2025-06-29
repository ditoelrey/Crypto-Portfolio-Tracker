using CryptoApp.Domain.Models;

namespace CryptoApp.Domain.Repositories;

public interface IPortfolioRepository : IRepository<Portfolio>
{
    Task<Portfolio?> GetPortfolioWithDetailsAsync(string portfolioId);
    Task<Portfolio?> GetPortfolioWithHoldingsAsync(string portfolioId);
    Task<Portfolio?> GetPortfolioWithTransactionsAsync(string portfolioId);
}
