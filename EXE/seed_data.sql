USE [DacSanVietDB];
GO

-- Seed dữ liệu mẫu để map gợi ý + chat truy vấn được "mượt" hơn.
-- Chạy file này SAU khi bạn đã tạo schema (db.sql).

IF NOT EXISTS (SELECT 1 FROM dbo.Regions)
BEGIN
    INSERT INTO dbo.Regions (RegionName, Province, Description, ImageUrl)
    VALUES
    (N'Miền Bắc', N'Hà Nội', N'Vùng miền Bắc', NULL),
    (N'Miền Trung', N'Thừa Thiên Huế', N'Vùng miền Trung', NULL),
    (N'Miền Nam', N'TP. Hồ Chí Minh', N'Vùng miền Nam', NULL),
    (N'Tây Nguyên', N'Đắk Lắk', N'Vùng Tây Nguyên', NULL),
    (N'Tây Nam Bộ', N'Cần Thơ', N'Vùng Tây Nam Bộ', NULL);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Categories)
BEGIN
    INSERT INTO dbo.Categories (CategoryName, Description)
    VALUES
    (N'Trà - cà phê', N'Đồ uống đặc sản'),
    (N'Bánh - kẹo', N'Bánh kẹo đặc sản'),
    (N'Mắm & gia vị', N'Gia vị vùng miền'),
    (N'Khô - sấy', N'Khô, sấy, đặc sản mang đi');
END
GO

-- Insert sample products if empty
IF NOT EXISTS (SELECT 1 FROM dbo.Products)
BEGIN
    DECLARE @catTea INT = (SELECT TOP 1 CategoryId FROM dbo.Categories WHERE CategoryName = N'Trà - cà phê');
    DECLARE @catCandy INT = (SELECT TOP 1 CategoryId FROM dbo.Categories WHERE CategoryName = N'Bánh - kẹo');
    DECLARE @catSauce INT = (SELECT TOP 1 CategoryId FROM dbo.Categories WHERE CategoryName = N'Mắm & gia vị');
    DECLARE @catDry INT = (SELECT TOP 1 CategoryId FROM dbo.Categories WHERE CategoryName = N'Khô - sấy');

    DECLARE @rHN INT = (SELECT TOP 1 RegionId FROM dbo.Regions WHERE Province = N'Hà Nội');
    DECLARE @rHue INT = (SELECT TOP 1 RegionId FROM dbo.Regions WHERE Province = N'Thừa Thiên Huế');
    DECLARE @rHCM INT = (SELECT TOP 1 RegionId FROM dbo.Regions WHERE Province = N'TP. Hồ Chí Minh');
    DECLARE @rDL INT = (SELECT TOP 1 RegionId FROM dbo.Regions WHERE Province = N'Đắk Lắk');
    DECLARE @rCT INT = (SELECT TOP 1 RegionId FROM dbo.Regions WHERE Province = N'Cần Thơ');

    INSERT INTO dbo.Products (ProductName, Description, Price, Stock, CategoryId, RegionId)
    VALUES
    (N'Trà sen Tây Hồ', N'Hương sen thanh nhẹ, vị trà đằm.', 159000, 50, @catTea, @rHN),
    (N'Mắm ruốc Huế', N'Nồng mà duyên, chấm nhẹ là bữa cơm đậm vị.', 69000, 200, @catSauce, @rHue),
    (N'Bánh pía Sóc Trăng', N'Ngọt thơm sầu riêng, béo đậu xanh.', 89000, 100, @catCandy, @rCT),
    (N'Cà phê Buôn Ma Thuột', N'Đậm, thơm, hậu vị dài.', 139000, 80, @catTea, @rDL),
    (N'Muối tôm Tây Ninh', N'Cay thơm đậm đà, ăn trái cây cực hợp.', 39000, 300, @catSauce, @rHCM);
END
GO

-- Add product images using existing template images (wwwroot/img/*)
IF NOT EXISTS (SELECT 1 FROM dbo.ProductImages)
BEGIN
    INSERT INTO dbo.ProductImages (ProductId, ImageUrl)
    SELECT ProductId, 'fruite-item-2.jpg' FROM dbo.Products WHERE ProductName = N'Trà sen Tây Hồ';

    INSERT INTO dbo.ProductImages (ProductId, ImageUrl)
    SELECT ProductId, 'featur-1.jpg' FROM dbo.Products WHERE ProductName = N'Mắm ruốc Huế';

    INSERT INTO dbo.ProductImages (ProductId, ImageUrl)
    SELECT ProductId, 'fruite-item-4.jpg' FROM dbo.Products WHERE ProductName = N'Cà phê Buôn Ma Thuột';

    INSERT INTO dbo.ProductImages (ProductId, ImageUrl)
    SELECT ProductId, 'fruite-item-3.jpg' FROM dbo.Products WHERE ProductName = N'Muối tôm Tây Ninh';

    INSERT INTO dbo.ProductImages (ProductId, ImageUrl)
    SELECT ProductId, 'fruite-item-5.jpg' FROM dbo.Products WHERE ProductName = N'Bánh pía Sóc Trăng';
END
GO

