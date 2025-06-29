using CryptoApp.Application.Interfaces;
using CryptoApp.Domain.Models;
using CryptoApp.Domain.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace CryptoApp.Application.Services;

public class UserService : IUserService
{
    private readonly UserManager<CryptoApplicationUser> _userManager;
    private readonly SignInManager<CryptoApplicationUser> _signInManager;
    private readonly IPortfolioService _portfolioService;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<UserService> _logger;

    public UserService(
        UserManager<CryptoApplicationUser> userManager,
        SignInManager<CryptoApplicationUser> signInManager,
        IPortfolioService portfolioService,
        IApplicationDbContext context,
        ILogger<UserService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _portfolioService = portfolioService;
        _context = context;
        _logger = logger;
    }

    public async Task<CryptoApplicationUser?> GetUserAsync(string id)
    {
        return await _userManager.FindByIdAsync(id);
    }

    public async Task<CryptoApplicationUser?> GetUserByEmailAsync(string email)
    {
        return await _userManager.FindByEmailAsync(email);
    }

    public async Task<(IdentityResult Result, CryptoApplicationUser? User)> CreateUserAsync(string username, string email, string password, string firstName, string lastName)
    {
        var user = new CryptoApplicationUser
        {
            UserName = username,
            Email = email,
            FirstName = firstName,
            LastName = lastName
        };

        var result = await _userManager.CreateAsync(user, password);
        
        if (result.Succeeded)
        {
            
            try
            {
                var portfolio = await _portfolioService.CreatePortfolioAsync($"{lastName}'s Portfolio", user.Id);
                user.Portfolio = portfolio;
                await _userManager.UpdateAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating portfolio for user {UserId}", user.Id);
                
                await _userManager.DeleteAsync(user);
                return (IdentityResult.Failed(new IdentityError { Description = "Failed to create user portfolio" }), null);
            }
        }
        
        return (result, result.Succeeded ? user : null);
    }

    public async Task<IdentityResult> UpdateUserAsync(CryptoApplicationUser user)
    {
        return await _userManager.UpdateAsync(user);
    }

    public async Task<IdentityResult> DeleteUserAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return IdentityResult.Failed(new IdentityError { Description = "User not found" });
        }
        return await _userManager.DeleteAsync(user);
    }

    public async Task<bool> ValidateCredentialsAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return false;
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, password, false);
        return result.Succeeded;
    }        public async Task<CryptoApplicationUser?> GetUserWithPortfolioAsync(string id)
    {
        var user = await _context.Users
            .Include(u => u.Portfolio)
                .ThenInclude(p => p.Holdings)
                    .ThenInclude(h => h.Cryptocurrency)
            .Include(u => u.Portfolio)
                .ThenInclude(p => p.Transactions)
            .AsNoTracking()  
            .FirstOrDefaultAsync(u => u.Id == id);

        
        if (user != null && user.Portfolio == null)
        {
            try
            {
                _logger.LogInformation("Creating default portfolio for user {UserId}", user.Id);
                var portfolio = await _portfolioService.CreatePortfolioAsync($"{user.LastName}'s Portfolio", user.Id);
                user.Portfolio = portfolio;
                await _userManager.UpdateAsync(user);
                
                
                return await _context.Users
                    .Include(u => u.Portfolio)
                        .ThenInclude(p => p.Holdings)
                            .ThenInclude(h => h.Cryptocurrency)
                    .Include(u => u.Portfolio)
                        .ThenInclude(p => p.Transactions)
                    .FirstOrDefaultAsync(u => u.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create portfolio for user {UserId}", user.Id);
                return user; 
            }
        }

        return user;
    }
}
