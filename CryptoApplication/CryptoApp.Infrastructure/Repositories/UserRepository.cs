using CryptoApp.Domain.Models;
using CryptoApp.Domain.Models.Identity;
using CryptoApp.Domain.Repositories;
using CryptoApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CryptoApp.Infrastructure.Repositories;

public class UserRepository : Repository<CryptoApplicationUser>, IUserRepository
{
    private readonly ApplicationDbContext _identityContext;

    public UserRepository(ApplicationDbContext context) : base(context)
    {
        _identityContext = context;
    }

    public async Task<CryptoApplicationUser?> GetByEmailAsync(string email)
    {
        return await _identityContext.Set<CryptoApplicationUser>()
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<CryptoApplicationUser?> GetByUsernameAsync(string username)
    {
        return await _identityContext.Set<CryptoApplicationUser>()
            .FirstOrDefaultAsync(u => u.UserName == username);
    }        public async Task<IEnumerable<Portfolio>> GetUserPortfoliosAsync(string userId)
        {
            var portfolio = await _identityContext.Set<Portfolio>()
                .Include(p => p.Holdings)
                    .ThenInclude(h => h.Cryptocurrency)
                .Include(p => p.Transactions)
                .Include(p => p.User)
                .Where(p => p.UserId == userId)
                .ToListAsync();

            return portfolio;
        }
}
