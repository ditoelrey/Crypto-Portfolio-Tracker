using CryptoApp.Domain.Models;

namespace CryptoApp.Application.Interfaces;

public interface ITransactionService
{    Task<Transaction> CreateTransactionAsync(string portfolioId, string cryptoId, TransactionType type, decimal amount, decimal price);
    Task<IEnumerable<Transaction>> GetPortfolioTransactionsAsync(string portfolioId);
    Task<Transaction?> GetTransactionAsync(string transactionId);
    Task UpdateTransactionAsync(Transaction transaction);
    Task DeleteTransactionAsync(string transactionId);
}
