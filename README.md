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
