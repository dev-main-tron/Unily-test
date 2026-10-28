-- Schema for the OPTIONAL SQL Server storage (Persistence:Provider = SqlServer).
-- Database-first: create this schema, then run scripts/scaffold-db.ps1 to (re)generate UnilyDbContext + Models.
-- The challenge itself runs on the in-memory cache and does not need this database.

IF DB_ID(N'Unily') IS NULL
    CREATE DATABASE [Unily];
GO
USE [Unily];
GO

IF OBJECT_ID(N'dbo.Reservations', N'U') IS NOT NULL DROP TABLE dbo.Reservations;
IF OBJECT_ID(N'dbo.Products', N'U') IS NOT NULL DROP TABLE dbo.Products;
GO

CREATE TABLE dbo.Products
(
    Id    uniqueidentifier NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
    Name  nvarchar(128)    NOT NULL,
    Stock int              NOT NULL CONSTRAINT CK_Products_Stock_NonNegative CHECK (Stock >= 0)
);
GO

CREATE TABLE dbo.Reservations
(
    Id        uniqueidentifier NOT NULL CONSTRAINT PK_Reservations PRIMARY KEY,
    ProductId uniqueidentifier NOT NULL CONSTRAINT FK_Reservations_Products REFERENCES dbo.Products (Id),
    Quantity  int              NOT NULL CONSTRAINT CK_Reservations_Quantity CHECK (Quantity >= 1),
    CreatedAt datetime2        NOT NULL,  -- UTC
    ExpiresAt datetime2        NOT NULL,  -- UTC, CreatedAt + TTL
    Status    varchar(16)      NOT NULL CONSTRAINT CK_Reservations_Status
                                CHECK (Status IN ('Active', 'Confirmed', 'Released', 'Expired'))
);
GO

CREATE INDEX IX_Reservations_ProductId_Status ON dbo.Reservations (ProductId, Status);
GO
