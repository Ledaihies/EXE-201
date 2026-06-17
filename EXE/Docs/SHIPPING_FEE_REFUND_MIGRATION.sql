IF COL_LENGTH('dbo.Orders', 'ShippingFee') IS NULL
BEGIN
    ALTER TABLE dbo.Orders ADD ShippingFee decimal(10, 2) NULL;
END;
