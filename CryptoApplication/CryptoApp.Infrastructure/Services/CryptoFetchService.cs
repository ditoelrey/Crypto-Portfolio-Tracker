using CryptoApp.Application.Interfaces;
using CryptoApp.Domain.Models.DTOs;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CryptoApp.Infrastructure.Services;

public class CryptoFetchService : ICryptoFetchService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CryptoFetchService> _logger;
    private const int MaxRetries = 3;
    private const int InitialRetryDelayMs = 1000;
    private const int MaxCoinsPerRequest = 50;
    private static readonly SemaphoreSlim _requestThrottler = new(1, 1);

    public CryptoFetchService(HttpClient httpClient, ILogger<CryptoFetchService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        _httpClient.BaseAddress = new Uri("https://api.coingecko.com/api/v3/");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    private async Task<(bool Success, string Content)> ExecuteWithRetryAsync(Func<Task<HttpResponseMessage>> apiCall)
    {
        int retryCount = 0;
        int delayMs = InitialRetryDelayMs;

        while (retryCount <= MaxRetries)
        {
            try
            {
                var response = await apiCall();
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return (true, content);
                }

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    retryCount++;
                    if (retryCount > MaxRetries)
                    {
                        _logger.LogWarning($"Max retries ({MaxRetries}) reached after TooManyRequests");
                        return (false, content);
                    }

                    await Task.Delay(delayMs);
                    delayMs *= 2;
                    continue;
                }

                _logger.LogError($"API error: {response.StatusCode}");
                return (false, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during API call (attempt {retryCount + 1}/{MaxRetries})");
                retryCount++;
                if (retryCount > MaxRetries)
                {
                    return (false, ex.Message);
                }
                await Task.Delay(delayMs);
                delayMs *= 2;
            }
        }

        return (false, "Max retries exceeded");
    }

    private async Task<(bool Success, string Content)> ExecuteRequestWithThrottlingAsync(Func<Task<HttpResponseMessage>> apiCall)
    {
        await _requestThrottler.WaitAsync();
        try
        {
            return await ExecuteWithRetryAsync(apiCall);
        }
        finally
        {
            await Task.Delay(200);  
            _requestThrottler.Release();
        }
    }

    public async Task<decimal> GetPriceAsync(string coinId)
    {
        var prices = await GetCryptoPricesAsync(new[] { coinId });
        return prices.FirstOrDefault()?.CurrentPrice ?? 0;
    }

    public async Task<IEnumerable<CryptoPriceDTO>> GetCryptoPricesAsync(IEnumerable<string> coinIds)
    {
        var idList = coinIds.ToList();
        if (!idList.Any())
        {
            return Enumerable.Empty<CryptoPriceDTO>();
        }

        var allResults = new List<CryptoPriceDTO>();

        
        for (int i = 0; i < idList.Count; i += MaxCoinsPerRequest)
        {
            var batchIds = idList.Skip(i).Take(MaxCoinsPerRequest);
            var idString = string.Join(",", batchIds);

            var (success, content) = await ExecuteRequestWithThrottlingAsync(() =>
                _httpClient.GetAsync($"simple/price?ids={idString}&vs_currencies=usd"));

            if (!success)
            {
                _logger.LogWarning($"Failed to fetch prices for batch starting at index {i}");
                foreach (var coinId in batchIds)
                {
                    allResults.Add(new CryptoPriceDTO { Id = coinId, CurrentPrice = 0 });
                }
                continue;
            }

            try
            {
                var prices = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, decimal>>>(content);
                if (prices != null)
                {
                    foreach (var coinId in batchIds)
                    {
                        if (prices.TryGetValue(coinId, out var currencyPrices) &&
                            currencyPrices.TryGetValue("usd", out var price))
                        {
                            allResults.Add(new CryptoPriceDTO { Id = coinId, CurrentPrice = price });
                        }
                        else
                        {
                            _logger.LogWarning($"No price data found for {coinId}");
                            allResults.Add(new CryptoPriceDTO { Id = coinId, CurrentPrice = 0 });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing price data for batch starting at index {i}");
                foreach (var coinId in batchIds)
                {
                    allResults.Add(new CryptoPriceDTO { Id = coinId, CurrentPrice = 0 });
                }
            }

           
            if (i + MaxCoinsPerRequest < idList.Count)
            {
                await Task.Delay(500);
            }
        }

        return allResults;
    }    public async Task<IEnumerable<CoinGeckoCoinDTO>> GetSupportedCoinsAsync()
    {
        var (success, content) = await ExecuteRequestWithThrottlingAsync(() =>
            _httpClient.GetAsync("coins/list"));

        if (!success)
        {
            _logger.LogError("Failed to fetch supported coins list");
            return Enumerable.Empty<CoinGeckoCoinDTO>();
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            _logger.LogInformation("Raw API response: {Content}", content);
            var coins = JsonSerializer.Deserialize<List<CoinGeckoCoinDTO>>(content, options);
            
            if (coins == null || !coins.Any())
            {
                _logger.LogWarning("No coins were deserialized from the API response");
                return Enumerable.Empty<CoinGeckoCoinDTO>();
            }

            _logger.LogInformation("Successfully fetched {Count} coins", coins.Count);
            return coins;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deserializing supported coins list. Content: {Content}", content);
            return Enumerable.Empty<CoinGeckoCoinDTO>();
        }
    }
}
