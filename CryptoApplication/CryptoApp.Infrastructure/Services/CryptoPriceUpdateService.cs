using CryptoApp.Application.Interfaces;
using CryptoApp.Domain.Models;
using CryptoApp.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CryptoApp.Infrastructure.Services;

public class CryptoPriceUpdateService : BackgroundService
{
    private readonly ILogger<CryptoPriceUpdateService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _updateInterval = TimeSpan.FromMinutes(10);
    private bool _initialLoadComplete;

    public CryptoPriceUpdateService(
        IServiceProvider serviceProvider,
        ILogger<CryptoPriceUpdateService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _initialLoadComplete = false;
    }    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_initialLoadComplete)
        {
            _logger.LogInformation("Performing initial price load");
            await UpdateAllPrices();
            _initialLoadComplete = true;
            _logger.LogInformation("Initial price load completed");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting scheduled price update cycle");
                await UpdateAllPrices();
                _logger.LogInformation("Price update cycle completed");
                await Task.Delay(_updateInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating cryptocurrency prices");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }    private async Task UpdateAllPrices()
    {
        using var scope = _serviceProvider.CreateScope();
        var cacheService = scope.ServiceProvider.GetRequiredService<ICryptoPriceCacheService>();
        var cryptoRepo = scope.ServiceProvider.GetRequiredService<IRepository<Cryptocurrency>>();

        try
        {
            var cryptocurrencies = await cryptoRepo.GetAllAsync();
            if (cryptocurrencies?.Any() != true) return;

            var coinIds = cryptocurrencies.Select(c => c.CoinGeckoId).ToList();
            var batchSize = 50; 
            
            for (var i = 0; i < coinIds.Count; i += batchSize)
            {
                var batch = coinIds.Skip(i).Take(batchSize);
                try
                {
                    var success = await cacheService.RefreshAllPricesAsync(batch);
                    if (!success)
                    {
                        _logger.LogWarning("Some prices in batch starting at index {Index} failed to update", i);
                    }
                    
                   
                    if (i + batchSize < coinIds.Count)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating price batch starting at index {Index}", i);
                }
            }
            
            _logger.LogInformation("Completed price update cycle for {Count} cryptocurrencies", coinIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk price update");
        }
    }
}
