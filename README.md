# Crypto Portfolio Tracker

 Full‑stack ASP.NET Core prototype to track cryptocurrency portfolios, compute portfolio valuations from live prices, and record buy/sell transactions per user.

## Features
- Per-user portfolios and transaction ledger (buy/sell).
- Live price ingestion with in-memory caching and background synchronization.
- Resilient external API integration (retry/backoff, 429 handling).
- Persistent storage with EF Core and SQL Server; automated DB migrations supported.
- Accurate financial handling via configured decimal precision and rounding.
- Basic authentication using ASP.NET Identity and environment-aware logging.

## Technologies
C# | ASP.NET Core | Entity Framework Core | SQL Server | ASP.NET Identity | HttpClient + Polly | IMemoryCache | BackgroundService | ILogger

## Quick start (Windows)
1. Clone:
   git clone <repo-url>
2. Set connection string and API settings (appsettings.Development.json or environment variables):
   - ConnectionStrings:DefaultConnection
   - CoinGecko:BaseUrl (or other provider)
3. Restore and build:
   dotnet restore
   dotnet build
4. Apply migrations (optional if startup auto-migrates):
   cd CryptoApplication/CryptoApp.Web
   dotnet ef database update
5. Run:
   dotnet run --project CryptoApplication/CryptoApp.Web

## Configuration
Provide required values via appsettings.* or environment variables:
- ConnectionStrings:DefaultConnection — SQL Server connection
- CoinGecko:BaseUrl — external price API base URL
- Background update interval — configurable in settings (seconds/minutes)


## Behavior notes
- On startup the app can preload price cache for faster reads and reduce API calls.
- Background service periodically refreshes prices and updates cache/DB.
- External API failures fall back to cached values; retry policies minimize transient errors.

## Extending
- Add analytics, alerts or simulated trading.
- Add unit/integration tests for service logic and DB operations.
- Harden concurrency with explicit DB transactions where needed.

## Contact
See repository for code structure and implementation details. Use issues/PRs for questions or contributions.
