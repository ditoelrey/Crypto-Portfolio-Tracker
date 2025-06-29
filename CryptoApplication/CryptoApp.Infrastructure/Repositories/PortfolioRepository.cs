using CryptoApp.Domain.Models;
using CryptoApp.Domain.Repositories;
using CryptoApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CryptoApp.Infrastructure.Repositories;

public class PortfolioRepository : Repository<Portfolio>, IPortfolioRepository
{
    private readonly ApplicationDbContext _dbContext;

    public PortfolioRepository(ApplicationDbContext context) : base(context)
    {
        _dbContext = context;
    }

    public async Task<Portfolio?> GetPortfolioWithDetailsAsync(string portfolioId)
    {
        return await _dbContext.Set<Portfolio>()
            .Include(p => p.Holdings)
                .ThenInclude(h => h.Cryptocurrency)
            .Include(p => p.Transactions)
                .ThenInclude(t => t.Cryptocurrency)
            .FirstOrDefaultAsync(p => p.Id == portfolioId);
    }

    public async Task<Portfolio?> GetPortfolioWithHoldingsAsync(string portfolioId)
    {
        return await _dbContext.Set<Portfolio>()
            .Include(p => p.Holdings)
                .ThenInclude(h => h.Cryptocurrency)
            .FirstOrDefaultAsync(p => p.Id == portfolioId);
    }

    public async Task<Portfolio?> GetPortfolioWithTransactionsAsync(string portfolioId)
    {
        return await _dbContext.Set<Portfolio>()
            .Include(p => p.Transactions)
                .ThenInclude(t => t.Cryptocurrency)
            .FirstOrDefaultAsync(p => p.Id == portfolioId);
    }
}
