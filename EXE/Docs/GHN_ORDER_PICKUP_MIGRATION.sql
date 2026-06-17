IF COL_LENGTH('dbo.Orders', 'ReceiverName') IS NULL
    ALTER TABLE [dbo].[Orders] ADD [ReceiverName] [nvarchar](150) NULL;
GO

IF COL_LENGTH('dbo.Orders', 'ReceiverPhone') IS NULL
    ALTER TABLE [dbo].[Orders] ADD [ReceiverPhone] [nvarchar](20) NULL;
GO

IF COL_LENGTH('dbo.Orders', 'ToDistrictId') IS NULL
    ALTER TABLE [dbo].[Orders] ADD [ToDistrictId] [int] NULL;
GO

IF COL_LENGTH('dbo.Orders', 'ToWardCode') IS NULL
    ALTER TABLE [dbo].[Orders] ADD [ToWardCode] [nvarchar](20) NULL;
GO

IF COL_LENGTH('dbo.Orders', 'ShippingNote') IS NULL
    ALTER TABLE [dbo].[Orders] ADD [ShippingNote] [nvarchar](500) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[ShippingMethods] WHERE [MethodName] = N'Giao Hang Nhanh')
BEGIN
    INSERT INTO [dbo].[ShippingMethods] ([MethodName], [Price])
    VALUES (N'Giao Hang Nhanh', 0);
END
GO
