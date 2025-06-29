using CryptoApp.Application.Interfaces;
using CryptoApp.Application.Services;
using CryptoApp.Domain.Models;
using CryptoApp.Domain.Models.Identity;
using CryptoApp.Domain.Repositories;
using CryptoApp.Infrastructure.Data;
using CryptoApp.Infrastructure.Repositories;
using CryptoApp.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Polly;
using Polly.Extensions.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Define retry policy for HTTP client
static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, retryAttempt => 
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}

// Add services to the container
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Logging configuration
if (builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
}
else
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();
    builder.Logging.SetMinimumLevel(LogLevel.Information);
}

// Database configuration with retry policy
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    });
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

builder.Services.AddScoped<IApplicationDbContext>(provider =>
    provider.GetRequiredService<ApplicationDbContext>());

// Identity configuration
builder.Services.AddIdentity<CryptoApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

// Cookie configuration
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.Cookie.Name = "CryptoAppCookie";
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(5);
    options.SlidingExpiration = true;
});

// Register Generic Repository
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// Register Specific Repositories
builder.Services.AddScoped<IPortfolioRepository, PortfolioRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

// Add memory cache
builder.Services.AddMemoryCache();

// Register CryptoPriceCacheService as Singleton
builder.Services.AddSingleton<ICryptoPriceCacheService, CryptoPriceCacheService>();

// Register other services
builder.Services.AddScoped<ICryptocurrencyService, CryptocurrencyService>();
builder.Services.AddScoped<IPortfolioService, PortfolioService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();

// Register HttpClient for CryptoFetchService with retry policy and configuration
builder.Services.AddHttpClient<ICryptoFetchService, CryptoFetchService>(client =>
{
    client.BaseAddress = new Uri("https://api.coingecko.com/api/v3/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.Add("User-Agent", "CryptoApp");
    client.Timeout = TimeSpan.FromSeconds(30); // Increased timeout for coin list
})
.AddPolicyHandler(HttpPolicyExtensions
    .HandleTransientHttpError()
    .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
    .WaitAndRetryAsync(3, retryAttempt => 
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

// Register background services
builder.Services.AddHostedService<CryptoPriceUpdateService>();

// Register other infrastructure services
builder.Services.AddMemoryCache();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Ensure database exists and migrations are applied
if (!app.Environment.IsDevelopment())
{
    try
    {
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<ApplicationDbContext>();
            var logger = services.GetRequiredService<ILogger<Program>>();
            
            logger.LogInformation("Attempting to migrate database...");
            context.Database.Migrate();
            logger.LogInformation("Database migration completed successfully.");

            // Initialize price cache
            var cryptoService = services.GetRequiredService<ICryptocurrencyService>();
            var cacheService = services.GetRequiredService<ICryptoPriceCacheService>();
            var cryptoRepo = services.GetRequiredService<IRepository<Cryptocurrency>>();
            
            logger.LogInformation("Initializing price cache...");
            var cryptocurrencies = await cryptoRepo.GetAllAsync();
            foreach (var crypto in cryptocurrencies)
            {
                try
                {
                    var price = await cryptoService.GetCurrentPriceAsync(crypto.CoinGeckoId);
                    if (price > 0)
                    {
                        cacheService.UpdatePrice(crypto.CoinGeckoId, price, DateTime.UtcNow);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error fetching initial price for {CoinId}", crypto.CoinGeckoId);
                }
            }
            logger.LogInformation("Price cache initialization completed.");
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "An error occurred while initializing the application.");
    }
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
    
app.MapRazorPages();

app.Run();
