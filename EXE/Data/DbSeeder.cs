using EXE.Models;
using EXE.Security;
using Microsoft.EntityFrameworkCore;

namespace EXE.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        await EnsureMarketplaceSchemaAsync(db);

        // Roles (idempotent)
        var hasAdminRole = await db.Roles.AnyAsync(r => r.RoleName == "Admin");
        var hasUserRole = await db.Roles.AnyAsync(r => r.RoleName == "User");
        var hasSellerRole = await db.Roles.AnyAsync(r => r.RoleName == "Seller");
        if (!hasAdminRole) db.Roles.Add(new Role { RoleName = "Admin" });
        if (!hasUserRole) db.Roles.Add(new Role { RoleName = "User" });
        if (!hasSellerRole) db.Roles.Add(new Role { RoleName = "Seller" });
        if (!hasAdminRole || !hasUserRole || !hasSellerRole) await db.SaveChangesAsync();

        var adminRoleId = await db.Roles.Where(r => r.RoleName == "Admin").Select(r => (int?)r.RoleId).FirstOrDefaultAsync();
        if (adminRoleId.HasValue)
        {
            // Promote likely admin accounts (common in student projects): emails containing "admin".
            // EF Core cannot translate Contains(string, StringComparison) to SQL; use ToLower().Contains for server-side filter.
            var candidates = await db.Users
                .Where(u => u.Email != null && u.Email.ToLower().Contains("admin"))
                .ToListAsync();

            var changed = false;
            foreach (var u in candidates)
            {
                if (u.RoleId != adminRoleId.Value)
                {
                    u.RoleId = adminRoleId.Value;
                    changed = true;
                }
            }
            if (changed) await db.SaveChangesAsync();

            // Ensure at least one admin exists.
            var anyAdmin = await db.Users.AnyAsync(u => u.RoleId == adminRoleId.Value);
            if (!anyAdmin)
            {
                db.Users.Add(new User
                {
                    FullName = "Administrator",
                    Email = "admin@local",
                    PasswordHash = PasswordHasher.Hash("Admin@12345"),
                    RoleId = adminRoleId.Value,
                    CreatedDate = DateTime.Now
                });
                await db.SaveChangesAsync();
            }
        }

        // Order statuses
        if (!await db.OrderStatuses.AnyAsync())
        {
            db.OrderStatuses.AddRange(
                new OrderStatus { StatusName = "Mới" },
                new OrderStatus { StatusName = "Đang xử lý" },
                new OrderStatus { StatusName = "Đang giao" },
                new OrderStatus { StatusName = "Hoàn thành" },
                new OrderStatus { StatusName = "Đã hủy" }
            );
            await db.SaveChangesAsync();
        }

        var marketplaceStatuses = new[]
        {
            "Cho nguoi ban xac nhan",
            "Cho lay hang",
            "Cho giao hang",
            "Da nhan hang",
            "Da huy",
            "Cho xac nhan hoan hang",
            "Da hoan tien"
        };
        foreach (var statusName in marketplaceStatuses)
        {
            if (!await db.OrderStatuses.AnyAsync(s => s.StatusName == statusName))
            {
                db.OrderStatuses.Add(new OrderStatus { StatusName = statusName });
            }
        }
        await db.SaveChangesAsync();

        // Payment methods
        if (!await db.PaymentMethods.AnyAsync())
        {
            db.PaymentMethods.AddRange(
                new PaymentMethod { MethodName = "COD" },
                new PaymentMethod { MethodName = "Chuyển khoản" }
            );
            await db.SaveChangesAsync();
        }

        // Regions
        if (!await db.Regions.AnyAsync())
        {
            db.Regions.AddRange(
                new Region { RegionName = "Miền Bắc", Province = "Hà Nội", Description = "Vùng miền Bắc" },
                new Region { RegionName = "Miền Trung", Province = "Thừa Thiên Huế", Description = "Vùng miền Trung" },
                new Region { RegionName = "Miền Nam", Province = "TP. Hồ Chí Minh", Description = "Vùng miền Nam" },
                new Region { RegionName = "Tây Nguyên", Province = "Đắk Lắk", Description = "Vùng Tây Nguyên" },
                new Region { RegionName = "Tây Nam Bộ", Province = "Cần Thơ", Description = "Vùng Tây Nam Bộ" }
            );
            await db.SaveChangesAsync();
        }

        // Categories
        if (!await db.Categories.AnyAsync())
        {
            db.Categories.AddRange(
                new Category { CategoryName = "Trà - cà phê", Description = "Đồ uống đặc sản" },
                new Category { CategoryName = "Bánh - kẹo", Description = "Bánh kẹo đặc sản" },
                new Category { CategoryName = "Mắm & gia vị", Description = "Gia vị vùng miền" },
                new Category { CategoryName = "Khô - sấy", Description = "Khô, sấy, đặc sản mang đi" }
            );
            await db.SaveChangesAsync();
        }

        // Voucher mẫu (theo vùng / dịp) – phần Occasions đã được gỡ bỏ (không seed Occasions/ProductOccasions nữa).
        if (!await db.Vouchers.AnyAsync())
        {
            var rHue = await db.Regions.Where(r => r.Province == "Thừa Thiên Huế").Select(r => (int?)r.RegionId).FirstOrDefaultAsync();
            var catTea = await db.Categories.Where(c => c.CategoryName == "Trà - cà phê").Select(c => (int?)c.CategoryId).FirstOrDefaultAsync();
            var now = DateTime.Now;
            db.Vouchers.AddRange(
                new Voucher
                {
                    Code = "TET10",
                    Description = "Giảm 10% đơn hàng dịp Tết",
                    DiscountPercent = 10,
                    MinOrderAmount = 200000,
                    StartDate = now,
                    ExpiryDate = now.AddMonths(3),
                    MaxUses = 100,
                    OccasionTag = "Tet"
                },
                new Voucher
                {
                    Code = "TRUNG10",
                    Description = "Mua đặc sản Miền Trung giảm 10%",
                    DiscountPercent = 10,
                    MinOrderAmount = 150000,
                    RegionId = rHue,
                    StartDate = now,
                    ExpiryDate = now.AddYears(1),
                    MaxUses = 200
                },
                new Voucher
                {
                    Code = "TRA50K",
                    Description = "Giảm 50.000đ đơn trà - cà phê từ 200k",
                    DiscountFixed = 50000,
                    MinOrderAmount = 200000,
                    CategoryId = catTea,
                    StartDate = now,
                    ExpiryDate = now.AddMonths(6),
                    MaxUses = 50
                }
            );
            await db.SaveChangesAsync();
        }

        // Products + images + reviews (idempotent: thêm nếu chưa có theo tên)
        {
            var catTea = await db.Categories.Where(c => c.CategoryName == "Trà - cà phê").Select(c => (int?)c.CategoryId).FirstOrDefaultAsync();
            var catCandy = await db.Categories.Where(c => c.CategoryName == "Bánh - kẹo").Select(c => (int?)c.CategoryId).FirstOrDefaultAsync();
            var catSauce = await db.Categories.Where(c => c.CategoryName == "Mắm & gia vị").Select(c => (int?)c.CategoryId).FirstOrDefaultAsync();
            var catDry = await db.Categories.Where(c => c.CategoryName == "Khô - sấy").Select(c => (int?)c.CategoryId).FirstOrDefaultAsync();

            var rHN = await db.Regions.Where(r => r.Province == "Hà Nội").Select(r => (int?)r.RegionId).FirstOrDefaultAsync();
            var rHue = await db.Regions.Where(r => r.Province == "Thừa Thiên Huế").Select(r => (int?)r.RegionId).FirstOrDefaultAsync();
            var rHCM = await db.Regions.Where(r => r.Province == "TP. Hồ Chí Minh").Select(r => (int?)r.RegionId).FirstOrDefaultAsync();
            var rDL = await db.Regions.Where(r => r.Province == "Đắk Lắk").Select(r => (int?)r.RegionId).FirstOrDefaultAsync();
            var rCT = await db.Regions.Where(r => r.Province == "Cần Thơ").Select(r => (int?)r.RegionId).FirstOrDefaultAsync();

            var now = DateTime.Now;

            async Task<Product> EnsureProductAsync(string name, string description, decimal price, int? stock, int? catId, int? regionId)
            {
                var existing = await db.Products.FirstOrDefaultAsync(p => p.ProductName == name);
                if (existing != null)
                {
                    // Cập nhật nhẹ mô tả/giá nếu trống hoặc 0
                    if (string.IsNullOrWhiteSpace(existing.Description))
                        existing.Description = description;
                    if (!existing.Price.HasValue || existing.Price <= 0)
                        existing.Price = price;
                    if (!existing.RegionId.HasValue && regionId.HasValue)
                        existing.RegionId = regionId;
                    if (!existing.CategoryId.HasValue && catId.HasValue)
                        existing.CategoryId = catId;
                    return existing;
                }

                var p = new Product
                {
                    ProductName = name,
                    Description = description,
                    Price = price,
                    Stock = stock,
                    CategoryId = catId,
                    RegionId = regionId,
                    ApprovalStatus = "Approved",
                    CreatedDate = now
                };
                db.Products.Add(p);
                return p;
            }

            // Hà Nội
            var p1 = await EnsureProductAsync(
                "Trà sen Tây Hồ",
                "Trà sen Tây Hồ được ủ từ những búp sen Hồ Tây vào chính vụ, kết hợp với trà Thái Nguyên tuyển chọn. Mỗi mẻ trà đều được ướp nhiều lần với gạo sen, tạo nên hương thơm thanh khiết, hậu vị ngọt dịu và lan tỏa rất lâu trong khoang miệng. Đây là lựa chọn lý tưởng để làm quà biếu cao cấp cho người thân, đối tác, đặc biệt là những ai yêu thích văn hóa thưởng trà Hà Nội.",
                159000, 80, catTea, rHN);

            var p2 = await EnsureProductAsync(
                "Cốm làng Vòng",
                "Cốm làng Vòng nổi tiếng với hạt cốm dẹp, xanh mướt, dẻo thơm, được làm hoàn toàn thủ công từ lúa nếp non. Từng mẻ cốm được rang trên chảo gang, giã trong cối đá rồi sàng lọc kỹ càng. Khi ăn, bạn sẽ cảm nhận được vị ngọt mát, mùi thơm của lúa non hòa cùng chút bùi bùi đặc trưng – gợi nhớ rõ nét mùa thu Hà Nội.",
                119000, 60, catCandy, rHN);

            var p3 = await EnsureProductAsync(
                "Ô mai sấu Hà Nội",
                "Ô mai sấu Hà Nội kết hợp vị chua nhẹ của quả sấu xanh với vị mặn, ngọt, cay hài hòa. Trái sấu được chọn lọc, ngâm và sên trong nhiều giờ để giữ được độ giòn, không quá khô cũng không quá mềm. Mỗi miếng ô mai là sự tổng hòa của vị chua – mặn – ngọt – cay, rất thích hợp để nhâm nhi cùng tách trà nóng hoặc mang theo khi đi xa.",
                69000, 120, catCandy, rHN);

            // Huế
            var p4 = await EnsureProductAsync(
                "Mắm ruốc Huế",
                "Mắm ruốc Huế là linh hồn của nhiều món ăn miền Trung như bún bò Huế, cơm hến, thịt luộc chấm mắm ruốc. Ruốc tươi được ủ lên men cùng muối theo tỷ lệ chuẩn, sau đó được nêm nếm với sả, tỏi, ớt để dậy mùi thơm đặc trưng. Hũ mắm ruốc đậm đà, chỉ cần pha loãng với nước sôi và thêm chút chanh tỏi ớt là đã có chén nước chấm hấp dẫn cho cả mâm cơm.",
                69000, 200, catSauce, rHue);

            // Tây Nguyên
            var p5 = await EnsureProductAsync(
                "Cà phê Buôn Ma Thuột rang mộc",
                "Cà phê rang mộc Buôn Ma Thuột sử dụng hạt cà phê Robusta và Arabica được tuyển chọn từ các nông trại trên cao nguyên bazan trù phú. Hạt được rang chậm ở nhiệt độ chuẩn, không tẩm ướp phụ gia, giữ trọn mùi thơm socola, hạt dẻ cùng hậu vị đắng nhẹ, ngọt sâu. Phù hợp cho pha phin truyền thống hoặc pha máy hiện đại.",
                139000, 100, catTea, rDL);

            // Miền Tây
            var p6 = await EnsureProductAsync(
                "Bánh pía Sóc Trăng sầu riêng",
                "Bánh pía Sóc Trăng với lớp vỏ mỏng nhiều lớp, nhân sầu riêng – đậu xanh béo bùi kết hợp lòng đỏ trứng muối mặn mà. Khi cắt bánh, phần nhân mềm mịn, dậy mùi sầu riêng nhưng không gắt, thích hợp để làm quà biếu sau mỗi chuyến đi miền Tây. Bánh được đóng gói hút chân không, tiện bảo quản và mang xa.",
                89000, 150, catCandy, rCT);

            var p7 = await EnsureProductAsync(
                "Khô cá lóc đồng Cần Thơ",
                "Khô cá lóc đồng Cần Thơ được làm từ cá lóc nuôi tự nhiên, thịt chắc, ít mỡ. Cá được sơ chế sạch, ướp muối, đường, tiêu, tỏi rồi phơi nắng nhiều ngày đến khi miếng khô lên màu vàng nâu đẹp mắt. Khi nướng hoặc chiên, thịt cá thơm lừng, dai nhẹ, mặn mà, rất hợp để làm mồi nhắm hoặc ăn cùng cơm trắng, cháo trắng.",
                159000, 90, catDry, rCT);

            // Miền Nam / TP.HCM & Tây Ninh
            var p8 = await EnsureProductAsync(
                "Muối tôm Tây Ninh đặc biệt",
                "Muối tôm Tây Ninh được làm từ tôm khô giã nhuyễn, rang cùng muối, tỏi, ớt và một chút đường theo công thức gia truyền. Hạt muối khô, tơi, có màu cam đỏ đẹp, vị mặn mà xen lẫn chút ngọt và cay nhẹ. Đây là “best-seller” khi ăn kèm trái cây, đặc biệt là xoài xanh, cóc, ổi hoặc dùng để ướp nướng hải sản.",
                39000, 300, catSauce, rHCM);

            var p9 = await EnsureProductAsync(
                "Bò một nắng Gia Lai",
                "Bò một nắng Gia Lai sử dụng thịt bò tươi bản địa, tẩm ướp sả, ớt, tiêu và các loại gia vị Tây Nguyên rồi phơi một nắng mạnh để mặt ngoài se lại nhưng bên trong vẫn giữ được độ mềm ẩm. Khi nướng trên than hoa, miếng bò dậy mùi thơm, ăn kèm muối kiến vàng chua mặn tạo nên hương vị rất riêng của núi rừng.",
                259000, 70, catDry, rDL);

            // Thêm một số sản phẩm khác để đa dạng catalogue
            var p10 = await EnsureProductAsync(
                "Chè lam làng Thạch Xá",
                "Chè lam Thạch Xá (Hà Nội) được làm từ bột nếp rang, mật mía, gừng và lạc rang. Miếng chè dẻo mềm, thơm mùi nếp, vị ngọt dịu và hơi cay nhẹ nơi đầu lưỡi. Đây là món quà quê gắn liền với ký ức ngày Tết ở nhiều gia đình miền Bắc.",
                59000, 80, catCandy, rHN);

            var p11 = await EnsureProductAsync(
                "Mè xửng Huế",
                "Mè xửng Huế dẻo dai, chan hòa giữa vị ngọt của đường mạch nha, vị bùi của lạc và mè rang vàng. Khi ăn, kẹo không quá dính răng, thơm nhẹ mùi vừng, rất hợp để nhâm nhi cùng tách trà nóng trong những ngày mưa.",
                49000, 120, catCandy, rHue);

            var p12 = await EnsureProductAsync(
                "Mắm cá linh miền Tây",
                "Mắm cá linh là linh hồn của nhiều món lẩu mắm, bún mắm ở vùng sông nước Cửu Long. Cá linh tươi được làm sạch, ướp muối và ủ trong chum sành đủ thời gian để lên men tự nhiên, cho hương thơm nồng nhưng hậu vị rất bùi và béo.",
                89000, 110, catSauce, rCT);

            var p13 = await EnsureProductAsync(
                "Trà atiso Đà Lạt",
                "Trà atiso Đà Lạt được sấy từ bông atiso tươi trên cao nguyên, giúp thanh nhiệt, dễ ngủ và hỗ trợ tiêu hóa. Nước trà có màu vàng trong, vị hơi ngọt hậu, thường được dùng sau bữa ăn hoặc những ngày thời tiết nóng.",
                129000, 90, catTea, rDL);

            var p14 = await EnsureProductAsync(
                "Khô mực một nắng Phan Thiết",
                "Khô mực một nắng Phan Thiết sử dụng mực tươi, dày thịt, phơi trong một nắng gắt để bề mặt se lại nhưng bên trong vẫn còn độ mềm. Khi nướng trên than, thớ mực bung đều, thơm lừng và ngọt thịt, rất hợp làm món nhậu hoặc quà biếu biển.",
                289000, 60, catDry, rHCM);

            var p15 = await EnsureProductAsync(
                "Muối tiêu chanh Tây Ninh",
                "Muối tiêu chanh Tây Ninh được xay mịn từ muối, tiêu, vỏ chanh sấy và một ít ớt. Hạt muối khô, vị mặn mà chua nhẹ, thường dùng chấm hải sản luộc, đồ nướng hoặc trái cây ít chua. Đây là gia vị không thể thiếu của nhiều tín đồ ăn vặt.",
                35000, 200, catSauce, rHCM);

            var p16 = await EnsureProductAsync(
                "Bánh đậu xanh Hải Dương",
                "Bánh đậu xanh Hải Dương truyền thống với nhân đậu xanh mịn, vỏ bánh mỏng tan, vị ngọt thanh. Bánh được đóng gói từng viên nhỏ tiện mang theo, thích hợp làm quà biếu và nhâm nhi cùng trà.",
                45000, 150, catCandy, rHN);

            var p17 = await EnsureProductAsync(
                "Kẹo lạc Hải Hậu",
                "Kẹo lạc Hải Hậu (Nam Định) giòn thơm, vị ngọt vừa, từng hạt lạc rang bọc trong lớp kẹo mạch nha. Món quà quê dân dã, nhâm nhi cùng trà hoặc làm quà biếu vùng đồng bằng.",
                42000, 120, catCandy, rHN);

            var p18 = await EnsureProductAsync(
                "Trà shan tuyết cổ thụ",
                "Trà shan tuyết cổ thụ vùng Tây Bắc từ cây chè trăm năm, búp phủ lông trắng. Nước trà vàng sáng, vị chát nhẹ rồi ngọt hậu, hương thơm tự nhiên. Dành cho người thích trà thuần.",
                189000, 60, catTea, rHN);

            var p19 = await EnsureProductAsync(
                "Bánh tráng phơi sương Trảng Bàng",
                "Bánh tráng phơi sương Trảng Bàng (Tây Ninh) mỏng mềm, dai vừa, thơm mùi gạo. Cuốn với thịt luộc, rau sống, bún và nước mắm pha là món ăn vặt đặc trưng miền Đông.",
                39000, 200, catCandy, rHCM);

            var p20 = await EnsureProductAsync(
                "Nước mắm Phú Quốc loại đặc biệt",
                "Nước mắm Phú Quốc loại đặc biệt ủ từ cá cơm và muối Bà Rịa, độ đạm cao, màu cánh gián đẹp. Vị mặn ngọt cân bằng, thơm nồng, dùng chấm hoặc nấu đều ngon.",
                95000, 80, catSauce, rHCM);

            await db.SaveChangesAsync();

            var demoAdvertisedProducts = new[] { p1, p2, p3, p4, p5, p6, p7, p8, p9, p10 };
            for (var i = 0; i < demoAdvertisedProducts.Length; i++)
            {
                var product = demoAdvertisedProducts[i];
                product.ApprovalStatus = "Approved";
                product.AdvertisingBudget = 1000000m - (i * 50000m);
                product.AdvertisingPackageCode = "demo_banner";
                product.AdvertisingPackageName = "Demo thanh quang cao";
                product.AdvertisingDays = 30;
                product.AdvertisingPaidDate = now;
                product.AdvertisingEndDate = now.AddDays(30);
                product.CreatedDate ??= now.AddMinutes(-i);
            }

            await db.SaveChangesAsync();

            // Ảnh sản phẩm: chỉ thêm nếu chưa có ảnh nào cho sản phẩm đó
            async Task EnsureImageAsync(Product p, string fileName)
            {
                if (p.ProductId == 0) return;
                var hasImage = await db.ProductImages.AnyAsync(pi => pi.ProductId == p.ProductId);
                if (!hasImage)
                {
                    db.ProductImages.Add(new ProductImage
                    {
                        ProductId = p.ProductId,
                        ImageUrl = fileName
                    });
                }
            }

            await EnsureImageAsync(p1, "fruite-item-2.jpg");
            await EnsureImageAsync(p2, "featur-1.jpg");
            await EnsureImageAsync(p3, "fruite-item-5.jpg");
            await EnsureImageAsync(p4, "fruite-item-4.jpg");
            await EnsureImageAsync(p5, "fruite-item-3.jpg");
            await EnsureImageAsync(p6, "fruite-item-1.jpg");
            await EnsureImageAsync(p7, "dry-item-1.jpg");
            await EnsureImageAsync(p8, "spice-item-1.jpg");
            await EnsureImageAsync(p9, "dry-item-2.jpg");
            await EnsureImageAsync(p10, "candy-item-1.jpg");
            await EnsureImageAsync(p11, "candy-item-2.jpg");
            await EnsureImageAsync(p12, "spice-item-2.jpg");
            await EnsureImageAsync(p13, "tea-item-1.jpg");
            await EnsureImageAsync(p14, "dry-item-3.jpg");
            await EnsureImageAsync(p15, "spice-item-3.jpg");
            await EnsureImageAsync(p16, "candy-item-1.jpg");
            await EnsureImageAsync(p17, "candy-item-2.jpg");
            await EnsureImageAsync(p18, "tea-item-1.jpg");
            await EnsureImageAsync(p19, "fruite-item-1.jpg");
            await EnsureImageAsync(p20, "fruite-item-4.jpg");

            await db.SaveChangesAsync();

            // Sample reviews cho các sản phẩm nổi bật – chỉ thêm nếu sản phẩm chưa có review nào
            async Task EnsureReviewAsync(Product p, int rating, string comment)
            {
                if (p.ProductId == 0) return;
                var hasReview = await db.Reviews.AnyAsync(r => r.ProductId == p.ProductId);
                if (!hasReview)
                {
                    db.Reviews.Add(new Review
                    {
                        ProductId = p.ProductId,
                        Rating = rating,
                        Comment = comment,
                        CreatedDate = now
                    });
                }
            }

            await EnsureReviewAsync(p1, 5, "Trà sen thơm dịu, hương sen rõ nhưng không gắt. Pha ấm đầu đã rất thơm, sang ấm thứ hai vị vẫn ngọt và thanh. Rất hợp để biếu người lớn tuổi.");
            await EnsureReviewAsync(p2, 5, "Cốm dẻo, hạt xanh mướt, ăn kèm chuối chín thì chuẩn vị mùa thu Hà Nội. Đóng gói cẩn thận, giao hàng nhanh, ăn cảm giác rất fresh.");
            await EnsureReviewAsync(p5, 4, "Cà phê rang mộc đúng kiểu, không bị tẩm bơ hay hương liệu. Pha phin ra nước sánh, thơm, uống không đường vẫn thấy hậu ngọt.");
            await EnsureReviewAsync(p6, 5, "Bánh pía mềm, nhân nhiều, mùi sầu riêng vừa phải không bị nặng. Mang biếu gia đình rất được khen.");
            await EnsureReviewAsync(p8, 5, "Muối tôm rất thơm, hạt tơi, không bị vón cục. Chấm xoài, cóc là hết ý. Lâu lắm mới ăn được muối tôm chuẩn như vậy.");

            await db.SaveChangesAsync();
        }
    }

    private static async Task EnsureMarketplaceSchemaAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH('Products', 'SellerId') IS NULL
BEGIN
    ALTER TABLE Products ADD SellerId INT NULL;
END;
""");

        await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH('Products', 'ApprovalStatus') IS NULL
BEGIN
    ALTER TABLE Products ADD ApprovalStatus NVARCHAR(20) NULL;
END;
""");

        await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH('Products', 'OriginProofImageUrl') IS NULL
BEGIN
    ALTER TABLE Products ADD OriginProofImageUrl NVARCHAR(255) NULL;
END;
""");

        await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH('Products', 'ViewCount') IS NULL
BEGIN
    ALTER TABLE Products ADD ViewCount INT NULL CONSTRAINT DF_Products_ViewCount DEFAULT 0;
END;
""");

        await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH('Products', 'AdvertisingBudget') IS NULL
BEGIN
    ALTER TABLE Products ADD AdvertisingBudget DECIMAL(10, 2) NULL CONSTRAINT DF_Products_AdvertisingBudget DEFAULT 0;
END;
""");

        await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH('Products', 'AdvertisingPackageCode') IS NULL
BEGIN
    ALTER TABLE Products ADD AdvertisingPackageCode NVARCHAR(50) NULL;
END;
""");

        await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH('Products', 'AdvertisingPackageName') IS NULL
BEGIN
    ALTER TABLE Products ADD AdvertisingPackageName NVARCHAR(120) NULL;
END;
""");

        await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH('Products', 'AdvertisingDays') IS NULL
BEGIN
    ALTER TABLE Products ADD AdvertisingDays INT NULL;
END;
""");

        await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH('Products', 'AdvertisingPaidDate') IS NULL
BEGIN
    ALTER TABLE Products ADD AdvertisingPaidDate DATETIME NULL;
END;
""");

        await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH('Products', 'AdvertisingEndDate') IS NULL
BEGIN
    ALTER TABLE Products ADD AdvertisingEndDate DATETIME NULL;
END;
""");

        await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH('Orders', 'ShippingFee') IS NULL
BEGIN
    ALTER TABLE Orders ADD ShippingFee DECIMAL(10, 2) NULL;
END;
""");

        await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH('Vouchers', 'Description') IS NULL
BEGIN
    ALTER TABLE Vouchers ADD Description NVARCHAR(300) NULL;
END;

IF COL_LENGTH('Vouchers', 'DiscountFixed') IS NULL
BEGIN
    ALTER TABLE Vouchers ADD DiscountFixed DECIMAL(12, 2) NULL;
END;

IF COL_LENGTH('Vouchers', 'MinOrderAmount') IS NULL
BEGIN
    ALTER TABLE Vouchers ADD MinOrderAmount DECIMAL(12, 2) NULL;
END;

IF COL_LENGTH('Vouchers', 'StartDate') IS NULL
BEGIN
    ALTER TABLE Vouchers ADD StartDate DATETIME NULL;
END;

IF COL_LENGTH('Vouchers', 'MaxUses') IS NULL
BEGIN
    ALTER TABLE Vouchers ADD MaxUses INT NULL;
END;

IF COL_LENGTH('Vouchers', 'RegionId') IS NULL
BEGIN
    ALTER TABLE Vouchers ADD RegionId INT NULL;
END;

IF COL_LENGTH('Vouchers', 'CategoryId') IS NULL
BEGIN
    ALTER TABLE Vouchers ADD CategoryId INT NULL;
END;

IF COL_LENGTH('Vouchers', 'OccasionTag') IS NULL
BEGIN
    ALTER TABLE Vouchers ADD OccasionTag NVARCHAR(50) NULL;
END;
""");

        await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH('Vouchers', 'IsFreeShipping') IS NULL
BEGIN
    ALTER TABLE Vouchers ADD IsFreeShipping BIT NOT NULL CONSTRAINT DF_Vouchers_IsFreeShipping DEFAULT 0;
END;
""");

        await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH('Vouchers', 'IsActive') IS NULL
BEGIN
    ALTER TABLE Vouchers ADD IsActive BIT NOT NULL CONSTRAINT DF_Vouchers_IsActive DEFAULT 1;
END;
""");

        await db.Database.ExecuteSqlRawAsync("""
UPDATE Products
SET ApprovalStatus = N'Approved'
WHERE ApprovalStatus IS NULL;
""");

        await db.Database.ExecuteSqlRawAsync("""
UPDATE Products
SET ViewCount = 0
WHERE ViewCount IS NULL;
""");

        await db.Database.ExecuteSqlRawAsync("""
UPDATE Products
SET AdvertisingBudget = 0
WHERE AdvertisingBudget IS NULL;
""");

        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID('AdminBankAccounts', 'U') IS NULL
BEGIN
    CREATE TABLE AdminBankAccounts (
        BankAccountId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        BankName NVARCHAR(120) NULL,
        AccountNumber NVARCHAR(50) NULL,
        AccountHolder NVARCHAR(120) NULL,
        TransferNote NVARCHAR(300) NULL,
        QrImageUrl NVARCHAR(255) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_AdminBankAccounts_IsActive DEFAULT 1,
        UpdatedDate DATETIME NULL CONSTRAINT DF_AdminBankAccounts_UpdatedDate DEFAULT GETDATE()
    );
END;
""");

        await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH('AdminBankAccounts', 'QrImageUrl') IS NULL
BEGIN
    ALTER TABLE AdminBankAccounts ADD QrImageUrl NVARCHAR(255) NULL;
END;
""");

        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID('AdvertisingPackages', 'U') IS NULL
BEGIN
    CREATE TABLE AdvertisingPackages (
        PackageId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Code NVARCHAR(50) NULL,
        Name NVARCHAR(120) NULL,
        Description NVARCHAR(300) NULL,
        Price DECIMAL(10,2) NOT NULL,
        Days INT NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_AdvertisingPackages_IsActive DEFAULT 1,
        CreatedDate DATETIME NULL CONSTRAINT DF_AdvertisingPackages_CreatedDate DEFAULT GETDATE()
    );
END;
""");

        await db.Database.ExecuteSqlRawAsync("""
IF NOT EXISTS (SELECT 1 FROM AdvertisingPackages)
BEGIN
    INSERT INTO AdvertisingPackages (Code, Name, Description, Price, Days, IsActive, CreatedDate)
    VALUES
    (N'banner_1d', N'Banner 1 ngày', N'Hiển thị trên banner và thanh quảng cáo trang chủ trong 1 ngày.', 23000, 1, 1, GETDATE()),
    (N'banner_3d', N'Banner 3 ngày', N'Gói thử nghiệm 3 ngày trên banner và thanh quảng cáo.', 65000, 3, 1, GETDATE()),
    (N'banner_7d', N'Banner 7 ngày', N'Phù hợp sản phẩm mới cần tăng nhận diện trong tuần đầu.', 149000, 7, 1, GETDATE()),
    (N'banner_14d', N'Banner 14 ngày', N'Ưu tiên hiển thị dài hơn cho sản phẩm đang bán tốt.', 279000, 14, 1, GETDATE()),
    (N'banner_30d', N'Banner 30 ngày', N'Gói tháng cho shop muốn duy trì hiện diện trên trang chủ.', 529000, 30, 1, GETDATE());
END;
""");

        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID('AdvertisingPaymentRequests', 'U') IS NULL
BEGIN
    CREATE TABLE AdvertisingPaymentRequests (
        RequestId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ProductId INT NOT NULL,
        SellerId INT NOT NULL,
        PackageCode NVARCHAR(50) NULL,
        PackageName NVARCHAR(120) NULL,
        Amount DECIMAL(10,2) NOT NULL,
        Days INT NOT NULL,
        Status NVARCHAR(30) NULL,
        TransferContent NVARCHAR(300) NULL,
        CreatedDate DATETIME NULL CONSTRAINT DF_AdvertisingPaymentRequests_CreatedDate DEFAULT GETDATE(),
        ConfirmedDate DATETIME NULL,
        ConfirmedByAdminId INT NULL,
        CONSTRAINT FK_AdvertisingPaymentRequests_Product FOREIGN KEY (ProductId) REFERENCES Products(ProductId),
        CONSTRAINT FK_AdvertisingPaymentRequests_Seller FOREIGN KEY (SellerId) REFERENCES Users(UserId)
    );
END;
""");

        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID('OrderReturnRequests', 'U') IS NULL
BEGIN
    CREATE TABLE OrderReturnRequests (
        ReturnRequestId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        OrderId INT NOT NULL,
        UserId INT NOT NULL,
        Reason NVARCHAR(1000) NOT NULL,
        ProofImageUrl NVARCHAR(300) NOT NULL,
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_OrderReturnRequests_Status DEFAULT N'Pending',
        RefundAmount DECIMAL(10,2) NOT NULL,
        CreatedDate DATETIME NOT NULL CONSTRAINT DF_OrderReturnRequests_CreatedDate DEFAULT GETDATE(),
        SellerConfirmedDate DATETIME NULL,
        SellerConfirmedByUserId INT NULL,
        RefundedDate DATETIME NULL,
        CONSTRAINT FK_OrderReturnRequests_Order FOREIGN KEY (OrderId) REFERENCES Orders(OrderId),
        CONSTRAINT FK_OrderReturnRequests_User FOREIGN KEY (UserId) REFERENCES Users(UserId),
        CONSTRAINT FK_OrderReturnRequests_SellerConfirmedBy FOREIGN KEY (SellerConfirmedByUserId) REFERENCES Users(UserId)
    );
END;
""");

        await db.Database.ExecuteSqlRawAsync("""
IF NOT EXISTS (SELECT 1 FROM AdminBankAccounts)
BEGIN
    INSERT INTO AdminBankAccounts (BankName, AccountNumber, AccountHolder, TransferNote, IsActive, UpdatedDate)
    VALUES (N'Chưa cấu hình', N'0000000000', N'ADMIN NGUỒN VIỆT', N'Admin cập nhật tài khoản nhận tiền quảng cáo tại mục Quảng cáo.', 1, GETDATE());
END;
""");

        await db.Database.ExecuteSqlRawAsync("""
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
""");
    }
}

