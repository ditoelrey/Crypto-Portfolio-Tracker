using CryptoApp.Domain.Models;
using CryptoApp.Domain.Repositories;

namespace CryptoApp.Application.Common.Interfaces;

public interface IUnitOfWork
{
    IRepository<T> Repository<T>() where T : class;
    IUserRepository Users { get; }
    IPortfolioRepository Portfolios { get; }
    Task SaveChangesAsync();
}
