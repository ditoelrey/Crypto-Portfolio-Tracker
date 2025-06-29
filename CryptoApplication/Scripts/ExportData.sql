IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO


SELECT 
    'INSERT INTO [Cryptocurrencies] ([Id], [Name], [Symbol], [CoinGeckoId], [CurrentPrice], [CreatedAt], [UpdatedAt]) VALUES (' +
    QUOTENAME(Id, '''') + ', ' +
    QUOTENAME(Name, '''') + ', ' +
    QUOTENAME(Symbol, '''') + ', ' +
    QUOTENAME(CoinGeckoId, '''') + ', ' +
    CAST(CurrentPrice as varchar) + ', ' +
    QUOTENAME(CONVERT(varchar, CreatedAt, 121), '''') + ', ' +
    CASE WHEN UpdatedAt IS NULL THEN 'NULL' ELSE QUOTENAME(CONVERT(varchar, UpdatedAt, 121), '''') END + ');'
FROM [Cryptocurrencies];

SELECT 
    'INSERT INTO [AspNetUsers] ([Id], [UserName], [Email], [FirstName], [LastName], [Address], [NormalizedUserName], [NormalizedEmail], [EmailConfirmed], [PasswordHash], [SecurityStamp], [ConcurrencyStamp], [PhoneNumber], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnd], [LockoutEnabled], [AccessFailedCount]) VALUES (' +
    QUOTENAME(Id, '''') + ', ' +
    QUOTENAME(UserName, '''') + ', ' +
    QUOTENAME(Email, '''') + ', ' +
    QUOTENAME(FirstName, '''') + ', ' +
    QUOTENAME(LastName, '''') + ', ' +
    QUOTENAME(Address, '''') + ', ' +
    QUOTENAME(NormalizedUserName, '''') + ', ' +
    QUOTENAME(NormalizedEmail, '''') + ', ' +
    CAST(EmailConfirmed as varchar) + ', ' +
    QUOTENAME(PasswordHash, '''') + ', ' +
    QUOTENAME(SecurityStamp, '''') + ', ' +
    QUOTENAME(ConcurrencyStamp, '''') + ', ' +
    CASE WHEN PhoneNumber IS NULL THEN 'NULL' ELSE QUOTENAME(PhoneNumber, '''') END + ', ' +
    CAST(PhoneNumberConfirmed as varchar) + ', ' +
    CAST(TwoFactorEnabled as varchar) + ', ' +
    CASE WHEN LockoutEnd IS NULL THEN 'NULL' ELSE QUOTENAME(CONVERT(varchar, LockoutEnd, 121), '''') END + ', ' +
    CAST(LockoutEnabled as varchar) + ', ' +
    CAST(AccessFailedCount as varchar) + ');'
FROM [AspNetUsers];

SELECT 
    'INSERT INTO [Portfolios] ([Id], [Name], [UserId], [CreatedAt], [UpdatedAt]) VALUES (' +
    QUOTENAME(Id, '''') + ', ' +
    QUOTENAME(Name, '''') + ', ' +
    QUOTENAME(UserId, '''') + ', ' +
    QUOTENAME(CONVERT(varchar, CreatedAt, 121), '''') + ', ' +
    CASE WHEN UpdatedAt IS NULL THEN 'NULL' ELSE QUOTENAME(CONVERT(varchar, UpdatedAt, 121), '''') END + ');'
FROM [Portfolios];

SELECT 
    'INSERT INTO [Holdings] ([Id], [PortfolioId], [CryptocurrencyId], [Quantity], [PurchasePrice], [PurchaseDate], [CreatedAt], [UpdatedAt]) VALUES (' +
    QUOTENAME(Id, '''') + ', ' +
    QUOTENAME(PortfolioId, '''') + ', ' +
    QUOTENAME(CryptocurrencyId, '''') + ', ' +
    CAST(Quantity as varchar) + ', ' +
    CAST(PurchasePrice as varchar) + ', ' +
    QUOTENAME(CONVERT(varchar, PurchaseDate, 121), '''') + ', ' +
    QUOTENAME(CONVERT(varchar, CreatedAt, 121), '''') + ', ' +
    CASE WHEN UpdatedAt IS NULL THEN 'NULL' ELSE QUOTENAME(CONVERT(varchar, UpdatedAt, 121), '''') END + ');'
FROM [Holdings];

SELECT 
    'INSERT INTO [Transactions] ([Id], [PortfolioId], [CryptocurrencyId], [Type], [Amount], [PriceAtTime], [Date], [CreatedAt], [UpdatedAt]) VALUES (' +
    QUOTENAME(Id, '''') + ', ' +
    QUOTENAME(PortfolioId, '''') + ', ' +
    QUOTENAME(CryptocurrencyId, '''') + ', ' +
    CAST([Type] as varchar) + ', ' +
    CAST(Amount as varchar) + ', ' +
    CAST(PriceAtTime as varchar) + ', ' +
    QUOTENAME(CONVERT(varchar, Date, 121), '''') + ', ' +
    QUOTENAME(CONVERT(varchar, CreatedAt, 121), '''') + ', ' +
    CASE WHEN UpdatedAt IS NULL THEN 'NULL' ELSE QUOTENAME(CONVERT(varchar, UpdatedAt, 121), '''') END + ');'
FROM [Transactions];
