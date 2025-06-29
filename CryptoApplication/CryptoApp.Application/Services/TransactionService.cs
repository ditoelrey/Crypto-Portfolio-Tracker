using CryptoApp.Application.Interfaces;
using CryptoApp.Domain.Models;
using CryptoApp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using System;

namespace CryptoApp.Application.Services;

public class TransactionService : ITransactionService
{
    private readonly IRepository<Transaction> _transactionRepository;
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly ICryptoPriceCacheService _priceCache;

    public TransactionService(
        IRepository<Transaction> transactionRepository,
        IPortfolioRepository portfolioRepository,
        ICryptoPriceCacheService priceCache)
    {
        _transactionRepository = transactionRepository;
        _portfolioRepository = portfolioRepository;
        _priceCache = priceCache;
    }

    public async Task<Transaction> CreateTransactionAsync(string portfolioId, string cryptoId, TransactionType type, decimal amount, decimal price)
    {
       
        var portfolio = await _portfolioRepository.GetPortfolioWithDetailsAsync(portfolioId);
        if (portfolio == null)
            throw new ArgumentException("Portfolio not found", nameof(portfolioId));

        var holding = portfolio.Holdings.FirstOrDefault(h => h.CryptocurrencyId == cryptoId);
        
        
        if (type == TransactionType.Sell)
        {
            if (holding == null || holding.Quantity < amount)
                throw new InvalidOperationException($"Insufficient balance for sell transaction. Available: {holding?.Quantity ?? 0}, Attempted to sell: {amount}");
        }

      
        var transaction = new Transaction
        {
            Id = Guid.NewGuid().ToString(),
            PortfolioId = portfolioId,
            CryptocurrencyId = cryptoId,
            Type = type,
            Amount = amount,
            PriceAtTime = price,
            Date = DateTime.UtcNow
        };

        
        if (type == TransactionType.Buy)
        {
            if (holding == null)
            {
                holding = new Holding
                {
                    Id = Guid.NewGuid().ToString(),
                    PortfolioId = portfolioId,
                    CryptocurrencyId = cryptoId,
                    Quantity = amount,
                    PurchasePrice = price,
                    PurchaseDate = transaction.Date
                };
                portfolio.Holdings.Add(holding);
            }
            else
            {
                
                var totalValue = (holding.Quantity * holding.PurchasePrice) + (amount * price);
                var totalQuantity = holding.Quantity + amount;
                holding.Quantity = totalQuantity;
                holding.PurchasePrice = totalValue / totalQuantity;
                holding.PurchaseDate = transaction.Date;
            }
        }
        else 
        {            if (holding == null)
            {
                throw new InvalidOperationException("Cannot sell cryptocurrency that is not in portfolio");
            }

            holding.Quantity -= amount;
            if (holding.Quantity == 0)
            {
                portfolio.Holdings.Remove(holding);
            }
        }

        
        portfolio.Transactions.Add(transaction);
        await _portfolioRepository.SaveChangesAsync();

        return transaction;
    }    public async Task<IEnumerable<Transaction>> GetPortfolioTransactionsAsync(string portfolioId)
    {
        var portfolio = await _portfolioRepository.GetPortfolioWithDetailsAsync(portfolioId);
        return portfolio?.Transactions.OrderByDescending(t => t.Date) ?? Enumerable.Empty<Transaction>();
    }public async Task<Transaction?> GetTransactionAsync(string transactionId)
    {
        return await _transactionRepository.GetByIdAsync(transactionId);
    }

    public async Task UpdateTransactionAsync(Transaction transaction)
    {
        var oldTransaction = await _transactionRepository.GetByIdAsync(transaction.Id);
        if (oldTransaction == null)
            throw new ArgumentException("Transaction not found", nameof(transaction));

        await UpdateHoldingsFromTransactionAsync(oldTransaction, true);

        await _transactionRepository.UpdateAsync(transaction);
        await UpdateHoldingsFromTransactionAsync(transaction);
        await _transactionRepository.SaveChangesAsync();
    }    public async Task DeleteTransactionAsync(string transactionId)
    {
        var transaction = await _transactionRepository.GetByIdAsync(transactionId);
        if (transaction == null)
            throw new ArgumentException("Transaction not found", nameof(transactionId));

        await _transactionRepository.DeleteAsync(transaction);
        await _transactionRepository.SaveChangesAsync();
    }

    private async Task UpdateHoldingsFromTransactionAsync(Transaction transaction, bool reverse = false)
    {
        var portfolio = await _portfolioRepository.GetPortfolioWithDetailsAsync(transaction.PortfolioId);
        if (portfolio == null)
            throw new ArgumentException("Portfolio not found", nameof(transaction));

        var holding = portfolio.Holdings.FirstOrDefault(h => h.CryptocurrencyId == transaction.CryptocurrencyId);
        var amount = reverse ? -transaction.Amount : transaction.Amount;

        if (transaction.Type == TransactionType.Buy)
        {
            if (holding == null)
            {
                if (!reverse)
                {
                    holding = new Holding
                    {
                        Id = Guid.NewGuid().ToString(),
                        PortfolioId = transaction.PortfolioId,
                        CryptocurrencyId = transaction.CryptocurrencyId,
                        Quantity = transaction.Amount,
                        PurchasePrice = transaction.PriceAtTime,
                        PurchaseDate = transaction.Date
                    };
                    portfolio.Holdings.Add(holding);
                }
            }
            else
            {
                holding.Quantity += amount;

                if (!reverse && amount > 0)
                {
                    var totalValue = (holding.Quantity - amount) * holding.PurchasePrice + amount * transaction.PriceAtTime;
                    holding.PurchasePrice = totalValue / holding.Quantity;
                    holding.PurchaseDate = transaction.Date;
                }
            }
        }
        else 
        {
            if (holding == null)
                throw new InvalidOperationException("Cannot sell cryptocurrency that is not in portfolio");

            if (!reverse && holding.Quantity < amount)
                throw new InvalidOperationException($"Insufficient balance for sell transaction. Available: {holding.Quantity}, Attempted to sell: {transaction.Amount}");

            holding.Quantity -= amount;

            if (holding.Quantity <= 0)
            {
                portfolio.Holdings.Remove(holding);
            }
        }

        await _portfolioRepository.SaveChangesAsync();
    }
}
