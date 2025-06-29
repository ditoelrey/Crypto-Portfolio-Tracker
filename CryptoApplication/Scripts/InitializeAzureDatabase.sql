-- First, ensure we're using the correct database
USE [CryptoApp.Web_db]
GO

-- Enable snapshot isolation for better concurrency
ALTER DATABASE [CryptoApp.Web_db] SET ALLOW_SNAPSHOT_ISOLATION ON
GO
ALTER DATABASE [CryptoApp.Web_db] SET READ_COMMITTED_SNAPSHOT ON
GO

-- Create EF Core Migrations table if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[__EFMigrationsHistory]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    )
END
GO

-- Note: The actual schema creation will be handled by EF Core migrations
-- This script is just to prepare the database for migrations
