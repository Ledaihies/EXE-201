using System;
using EXE.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EXE.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260620000000_AddCodSettlementFlow")]
    public partial class AddCodSettlementFlow : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Orders', 'PaymentStatus') IS NULL
BEGIN
    ALTER TABLE [Orders] ADD [PaymentStatus] nvarchar(50) NULL;
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID('CODCollections', 'U') IS NULL
BEGIN
    CREATE TABLE [CODCollections] (
        [CODCollectionId] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [Amount] decimal(10, 2) NOT NULL,
        [Status] nvarchar(50) NOT NULL CONSTRAINT [DF_CODCollections_Status] DEFAULT N'PendingCollection',
        [CollectedAt] datetime NULL,
        [RemittedToAdminAt] datetime NULL,
        [ConfirmedByAdminId] int NULL,
        [Note] nvarchar(500) NULL,
        [CreatedAt] datetime NOT NULL CONSTRAINT [DF_CODCollections_CreatedAt] DEFAULT ((getdate())),
        CONSTRAINT [PK_CODCollections] PRIMARY KEY ([CODCollectionId]),
        CONSTRAINT [FK_CODCollections_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([OrderId]) ON DELETE CASCADE,
        CONSTRAINT [FK_CODCollections_Users_ConfirmedByAdminId] FOREIGN KEY ([ConfirmedByAdminId]) REFERENCES [Users] ([UserId])
    );
END

IF OBJECT_ID('OrderSettlements', 'U') IS NULL
BEGIN
    CREATE TABLE [OrderSettlements] (
        [OrderSettlementId] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [SellerId] int NOT NULL,
        [GrossAmount] decimal(10, 2) NOT NULL,
        [PlatformFee] decimal(10, 2) NOT NULL,
        [ShippingFee] decimal(10, 2) NOT NULL,
        [CodFee] decimal(10, 2) NOT NULL,
        [SellerReceivable] decimal(10, 2) NOT NULL,
        [AdminRevenue] decimal(10, 2) NOT NULL,
        [PaymentMethod] nvarchar(50) NOT NULL CONSTRAINT [DF_OrderSettlements_PaymentMethod] DEFAULT N'COD',
        [SettlementStatus] nvarchar(50) NOT NULL CONSTRAINT [DF_OrderSettlements_SettlementStatus] DEFAULT N'NotReady',
        [CreatedAt] datetime NOT NULL CONSTRAINT [DF_OrderSettlements_CreatedAt] DEFAULT ((getdate())),
        [SettledAt] datetime NULL,
        CONSTRAINT [PK_OrderSettlements] PRIMARY KEY ([OrderSettlementId]),
        CONSTRAINT [FK_OrderSettlements_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([OrderId]) ON DELETE CASCADE,
        CONSTRAINT [FK_OrderSettlements_Users_SellerId] FOREIGN KEY ([SellerId]) REFERENCES [Users] ([UserId])
    );
END

IF OBJECT_ID('SellerPayouts', 'U') IS NULL
BEGIN
    CREATE TABLE [SellerPayouts] (
        [SellerPayoutId] int NOT NULL IDENTITY,
        [SellerId] int NOT NULL,
        [TotalAmount] decimal(10, 2) NOT NULL,
        [Status] nvarchar(50) NOT NULL CONSTRAINT [DF_SellerPayouts_Status] DEFAULT N'Pending',
        [PayoutMethod] nvarchar(100) NULL,
        [PaidAt] datetime NULL,
        [CreatedByAdminId] int NULL,
        [Note] nvarchar(500) NULL,
        [CreatedAt] datetime NOT NULL CONSTRAINT [DF_SellerPayouts_CreatedAt] DEFAULT ((getdate())),
        CONSTRAINT [PK_SellerPayouts] PRIMARY KEY ([SellerPayoutId]),
        CONSTRAINT [FK_SellerPayouts_Users_CreatedByAdminId] FOREIGN KEY ([CreatedByAdminId]) REFERENCES [Users] ([UserId]),
        CONSTRAINT [FK_SellerPayouts_Users_SellerId] FOREIGN KEY ([SellerId]) REFERENCES [Users] ([UserId])
    );
END

IF OBJECT_ID('SellerPayoutItems', 'U') IS NULL
BEGIN
    CREATE TABLE [SellerPayoutItems] (
        [SellerPayoutItemId] int NOT NULL IDENTITY,
        [SellerPayoutId] int NOT NULL,
        [OrderSettlementId] int NOT NULL,
        [OrderId] int NOT NULL,
        [Amount] decimal(10, 2) NOT NULL,
        CONSTRAINT [PK_SellerPayoutItems] PRIMARY KEY ([SellerPayoutItemId]),
        CONSTRAINT [FK_SellerPayoutItems_OrderSettlements_OrderSettlementId] FOREIGN KEY ([OrderSettlementId]) REFERENCES [OrderSettlements] ([OrderSettlementId]),
        CONSTRAINT [FK_SellerPayoutItems_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([OrderId]),
        CONSTRAINT [FK_SellerPayoutItems_SellerPayouts_SellerPayoutId] FOREIGN KEY ([SellerPayoutId]) REFERENCES [SellerPayouts] ([SellerPayoutId]) ON DELETE CASCADE
    );
END

IF OBJECT_ID('CODCollections', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CODCollections_ConfirmedByAdminId' AND object_id = OBJECT_ID('CODCollections'))
    CREATE INDEX [IX_CODCollections_ConfirmedByAdminId] ON [CODCollections] ([ConfirmedByAdminId]);
IF OBJECT_ID('CODCollections', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CODCollections_OrderId' AND object_id = OBJECT_ID('CODCollections'))
    CREATE UNIQUE INDEX [IX_CODCollections_OrderId] ON [CODCollections] ([OrderId]);
IF OBJECT_ID('OrderSettlements', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrderSettlements_OrderId_SellerId' AND object_id = OBJECT_ID('OrderSettlements'))
    CREATE UNIQUE INDEX [IX_OrderSettlements_OrderId_SellerId] ON [OrderSettlements] ([OrderId], [SellerId]);
IF OBJECT_ID('OrderSettlements', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrderSettlements_SellerId' AND object_id = OBJECT_ID('OrderSettlements'))
    CREATE INDEX [IX_OrderSettlements_SellerId] ON [OrderSettlements] ([SellerId]);
IF OBJECT_ID('SellerPayoutItems', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SellerPayoutItems_OrderId' AND object_id = OBJECT_ID('SellerPayoutItems'))
    CREATE INDEX [IX_SellerPayoutItems_OrderId] ON [SellerPayoutItems] ([OrderId]);
IF OBJECT_ID('SellerPayoutItems', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SellerPayoutItems_OrderSettlementId' AND object_id = OBJECT_ID('SellerPayoutItems'))
    CREATE UNIQUE INDEX [IX_SellerPayoutItems_OrderSettlementId] ON [SellerPayoutItems] ([OrderSettlementId]);
IF OBJECT_ID('SellerPayoutItems', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SellerPayoutItems_SellerPayoutId' AND object_id = OBJECT_ID('SellerPayoutItems'))
    CREATE INDEX [IX_SellerPayoutItems_SellerPayoutId] ON [SellerPayoutItems] ([SellerPayoutId]);
IF OBJECT_ID('SellerPayouts', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SellerPayouts_CreatedByAdminId' AND object_id = OBJECT_ID('SellerPayouts'))
    CREATE INDEX [IX_SellerPayouts_CreatedByAdminId] ON [SellerPayouts] ([CreatedByAdminId]);
IF OBJECT_ID('SellerPayouts', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SellerPayouts_SellerId' AND object_id = OBJECT_ID('SellerPayouts'))
    CREATE INDEX [IX_SellerPayouts_SellerId] ON [SellerPayouts] ([SellerId]);

IF NOT EXISTS (SELECT 1 FROM PaymentMethods WHERE MethodName = 'COD')
BEGIN
    INSERT INTO PaymentMethods (MethodName) VALUES ('COD');
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('SellerPayoutItems', 'U') IS NOT NULL DROP TABLE [SellerPayoutItems];
IF OBJECT_ID('CODCollections', 'U') IS NOT NULL DROP TABLE [CODCollections];
IF OBJECT_ID('OrderSettlements', 'U') IS NOT NULL DROP TABLE [OrderSettlements];
IF OBJECT_ID('SellerPayouts', 'U') IS NOT NULL DROP TABLE [SellerPayouts];
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('Orders', 'PaymentStatus') IS NOT NULL
BEGIN
    ALTER TABLE [Orders] DROP COLUMN [PaymentStatus];
END
");

            migrationBuilder.Sql("DELETE FROM PaymentMethods WHERE MethodName = 'COD';");
        }
    }
}
