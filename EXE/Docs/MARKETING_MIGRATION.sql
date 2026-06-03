-- Chạy script này nếu database đã tồn tại và chưa có bảng/cột mới cho Marketing (Voucher mở rộng, Occasion, Referral).
-- Nếu dùng EF Core Migrations thì có thể bỏ qua và chạy: dotnet ef migrations add MarketingFeatures && dotnet ef database update

-- 1. Voucher: thêm cột (bỏ qua nếu đã có)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Vouchers') AND name = 'Description')
    ALTER TABLE Vouchers ADD Description NVARCHAR(300) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Vouchers') AND name = 'DiscountFixed')
    ALTER TABLE Vouchers ADD DiscountFixed DECIMAL(12,2) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Vouchers') AND name = 'MinOrderAmount')
    ALTER TABLE Vouchers ADD MinOrderAmount DECIMAL(12,2) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Vouchers') AND name = 'StartDate')
    ALTER TABLE Vouchers ADD StartDate DATETIME NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Vouchers') AND name = 'MaxUses')
    ALTER TABLE Vouchers ADD MaxUses INT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Vouchers') AND name = 'RegionId')
    ALTER TABLE Vouchers ADD RegionId INT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Vouchers') AND name = 'CategoryId')
    ALTER TABLE Vouchers ADD CategoryId INT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Vouchers') AND name = 'OccasionTag')
    ALTER TABLE Vouchers ADD OccasionTag NVARCHAR(50) NULL;

-- 2. User: mã giới thiệu
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'ReferralCode')
    ALTER TABLE Users ADD ReferralCode NVARCHAR(20) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'ReferredByUserId')
    ALTER TABLE Users ADD ReferredByUserId INT NULL;
-- Unique index cho ReferralCode (chỉ trên giá trị không null)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_ReferralCode' AND object_id = OBJECT_ID('Users'))
    CREATE UNIQUE INDEX IX_Users_ReferralCode ON Users(ReferralCode) WHERE ReferralCode IS NOT NULL;

-- 3. Bảng Occasions
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Occasions')
BEGIN
    CREATE TABLE Occasions (
        OccasionId INT IDENTITY(1,1) PRIMARY KEY,
        Code NVARCHAR(50) NOT NULL,
        Name NVARCHAR(150) NULL,
        Description NVARCHAR(500) NULL,
        ImageUrl NVARCHAR(300) NULL,
        SortOrder INT NOT NULL DEFAULT 0
    );
END

-- 4. Bảng ProductOccasions
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductOccasions')
BEGIN
    CREATE TABLE ProductOccasions (
        ProductId INT NOT NULL,
        OccasionId INT NOT NULL,
        PRIMARY KEY (ProductId, OccasionId),
        FOREIGN KEY (ProductId) REFERENCES Products(ProductId),
        FOREIGN KEY (OccasionId) REFERENCES Occasions(OccasionId)
    );
END

-- 5. Bảng ReferralRewards
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ReferralRewards')
BEGIN
    CREATE TABLE ReferralRewards (
        ReferralRewardId INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NOT NULL,
        VoucherId INT NOT NULL,
        CreatedAt DATETIME NOT NULL,
        FOREIGN KEY (UserId) REFERENCES Users(UserId),
        FOREIGN KEY (VoucherId) REFERENCES Vouchers(VoucherId)
    );
END

-- 6. FK User.ReferredByUserId (nếu chưa có)
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Users_ReferredByUser')
    ALTER TABLE Users ADD CONSTRAINT FK_Users_ReferredByUser FOREIGN KEY (ReferredByUserId) REFERENCES Users(UserId);

-- 7. FK Voucher.RegionId, Voucher.CategoryId (nếu chưa có)
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('Vouchers') AND name LIKE '%Region%')
    ALTER TABLE Vouchers ADD CONSTRAINT FK_Vouchers_Region FOREIGN KEY (RegionId) REFERENCES Regions(RegionId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('Vouchers') AND name LIKE '%Category%')
    ALTER TABLE Vouchers ADD CONSTRAINT FK_Vouchers_Category FOREIGN KEY (CategoryId) REFERENCES Categories(CategoryId);
