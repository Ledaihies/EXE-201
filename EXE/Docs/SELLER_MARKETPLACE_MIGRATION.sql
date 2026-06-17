-- Marketplace seller upgrade.
-- App startup also applies this defensively through DbSeeder.

IF NOT EXISTS (SELECT 1 FROM Roles WHERE RoleName = N'Seller')
BEGIN
    INSERT INTO Roles (RoleName) VALUES (N'Seller');
END;

IF COL_LENGTH('Products', 'SellerId') IS NULL
BEGIN
    ALTER TABLE Products ADD SellerId INT NULL;
END;
GO

IF COL_LENGTH('Products', 'ApprovalStatus') IS NULL
BEGIN
    ALTER TABLE Products ADD ApprovalStatus NVARCHAR(20) NULL;
END;
GO

IF COL_LENGTH('Products', 'OriginProofImageUrl') IS NULL
BEGIN
    ALTER TABLE Products ADD OriginProofImageUrl NVARCHAR(255) NULL;
END;
GO

UPDATE Products
SET ApprovalStatus = N'Approved'
WHERE ApprovalStatus IS NULL;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_Products_Seller'
)
BEGIN
    ALTER TABLE Products
    ADD CONSTRAINT FK_Products_Seller
    FOREIGN KEY (SellerId) REFERENCES Users(UserId);
END;
