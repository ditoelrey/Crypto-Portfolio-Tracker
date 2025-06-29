using CryptoApp.Domain.Models.Identity;
using Microsoft.AspNetCore.Identity;

namespace CryptoApp.Application.Interfaces;

public interface IUserService
{
    Task<CryptoApplicationUser?> GetUserAsync(string id);
    Task<CryptoApplicationUser?> GetUserByEmailAsync(string email);
    Task<(IdentityResult Result, CryptoApplicationUser? User)> CreateUserAsync(string username, string email, string password, string firstName, string lastName);
    Task<IdentityResult> UpdateUserAsync(CryptoApplicationUser user);
    Task<IdentityResult> DeleteUserAsync(string id);
    Task<bool> ValidateCredentialsAsync(string email, string password);
    Task<CryptoApplicationUser?> GetUserWithPortfolioAsync(string id);
}
