IF OBJECT_ID('dbo.Wallets', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Wallets](
        [WalletId] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [UserId] [int] NOT NULL,
        [Balance] [decimal](18,2) NOT NULL CONSTRAINT [DF_Wallets_Balance] DEFAULT (0),
        [CreatedDate] [datetime] NOT NULL CONSTRAINT [DF_Wallets_CreatedDate] DEFAULT (getdate()),
        [UpdatedDate] [datetime] NOT NULL CONSTRAINT [DF_Wallets_UpdatedDate] DEFAULT (getdate()),
        CONSTRAINT [UQ_Wallets_UserId] UNIQUE ([UserId]),
        CONSTRAINT [FK_Wallets_Users] FOREIGN KEY([UserId]) REFERENCES [dbo].[Users]([UserId])
    );
END
GO

IF OBJECT_ID('dbo.WalletTransactions', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WalletTransactions](
        [WalletTransactionId] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [WalletId] [int] NOT NULL,
        [OrderId] [int] NULL,
        [Type] [nvarchar](30) NOT NULL,
        [Amount] [decimal](18,2) NOT NULL,
        [BalanceAfter] [decimal](18,2) NOT NULL,
        [Description] [nvarchar](300) NULL,
        [CreatedDate] [datetime] NOT NULL CONSTRAINT [DF_WalletTransactions_CreatedDate] DEFAULT (getdate()),
        CONSTRAINT [FK_WalletTransactions_Wallets] FOREIGN KEY([WalletId]) REFERENCES [dbo].[Wallets]([WalletId]),
        CONSTRAINT [FK_WalletTransactions_Orders] FOREIGN KEY([OrderId]) REFERENCES [dbo].[Orders]([OrderId])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[PaymentMethods] WHERE [MethodName] = N'Wallet')
BEGIN
    INSERT INTO [dbo].[PaymentMethods] ([MethodName]) VALUES (N'Wallet');
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[PaymentMethods] WHERE [MethodName] = N'Refund')
BEGIN
    INSERT INTO [dbo].[PaymentMethods] ([MethodName]) VALUES (N'Refund');
END
GO

IF OBJECT_ID('dbo.WalletTopUpRequests', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WalletTopUpRequests](
        [RequestId] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [UserId] [int] NOT NULL,
        [Amount] [decimal](18,2) NOT NULL,
        [Status] [nvarchar](30) NOT NULL CONSTRAINT [DF_WalletTopUpRequests_Status] DEFAULT (N'Pending'),
        [TransferContent] [nvarchar](50) NOT NULL CONSTRAINT [DF_WalletTopUpRequests_TransferContent] DEFAULT (N''),
        [CreatedDate] [datetime] NOT NULL CONSTRAINT [DF_WalletTopUpRequests_CreatedDate] DEFAULT (getdate()),
        [ConfirmedDate] [datetime] NULL,
        CONSTRAINT [FK_WalletTopUpRequests_Users] FOREIGN KEY([UserId]) REFERENCES [dbo].[Users]([UserId])
    );
END
GO
