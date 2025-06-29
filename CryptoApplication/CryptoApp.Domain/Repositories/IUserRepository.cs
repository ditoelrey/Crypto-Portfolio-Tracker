using CryptoApp.Domain.Models;
using CryptoApp.Domain.Models.Identity;

namespace CryptoApp.Domain.Repositories;

public interface IUserRepository : IRepository<CryptoApplicationUser>
{
    Task<CryptoApplicationUser?> GetByEmailAsync(string email);
    Task<CryptoApplicationUser?> GetByUsernameAsync(string username);
    Task<IEnumerable<Portfolio>> GetUserPortfoliosAsync(string userId);
}
