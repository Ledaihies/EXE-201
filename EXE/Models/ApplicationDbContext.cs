using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace EXE.Models;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext()
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<AdminBankAccount> AdminBankAccounts { get; set; }

    public virtual DbSet<AdvertisingPaymentRequest> AdvertisingPaymentRequests { get; set; }

    public virtual DbSet<AdvertisingPackage> AdvertisingPackages { get; set; }

    public virtual DbSet<Cart> Carts { get; set; }

    public virtual DbSet<CartItem> CartItems { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<ContactMessage> ContactMessages { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderItem> OrderItems { get; set; }

    public virtual DbSet<OrderShipping> OrderShippings { get; set; }

    public virtual DbSet<OrderStatus> OrderStatuses { get; set; }

    public virtual DbSet<OrderVoucher> OrderVouchers { get; set; }

    public virtual DbSet<OrderReturnRequest> OrderReturnRequests { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<PaymentMethod> PaymentMethods { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductImage> ProductImages { get; set; }

    public virtual DbSet<Region> Regions { get; set; }

    public virtual DbSet<Review> Reviews { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<ShippingMethod> ShippingMethods { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Voucher> Vouchers { get; set; }

    public virtual DbSet<Wishlist> Wishlists { get; set; }

    public virtual DbSet<Wallet> Wallets { get; set; }

    public virtual DbSet<WalletTransaction> WalletTransactions { get; set; }

    public virtual DbSet<WalletTopUpRequest> WalletTopUpRequests { get; set; }

    // Occasions/ProductOccasions đã được gỡ khỏi runtime (không dùng bảng DB tương ứng).

    public virtual DbSet<ReferralReward> ReferralRewards { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // DbContext is configured in Program.cs via DI.
        if (!optionsBuilder.IsConfigured)
        {
            base.OnConfiguring(optionsBuilder);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Bỏ qua entity ProductOccasion để không yêu cầu bảng/primary key tương ứng.
        modelBuilder.Ignore<ProductOccasion>();

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("PK__AuditLog__5E5486488F2E8780");

            entity.Property(e => e.LogDate).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs).HasConstraintName("FK__AuditLogs__UserI__7A672E12");
        });

        modelBuilder.Entity<AdminBankAccount>(entity =>
        {
            entity.HasKey(e => e.BankAccountId);
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<AdvertisingPaymentRequest>(entity =>
        {
            entity.HasKey(e => e.RequestId);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.HasOne(d => d.Product).WithMany().HasForeignKey(d => d.ProductId);
            entity.HasOne(d => d.Seller).WithMany().HasForeignKey(d => d.SellerId);
        });

        modelBuilder.Entity<AdvertisingPackage>(entity =>
        {
            entity.HasKey(e => e.PackageId);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasKey(e => e.CartId).HasName("PK__Cart__51BCD7B71E1FAF54");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.User).WithMany(p => p.Carts).HasConstraintName("FK__Cart__UserId__4F7CD00D");
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasKey(e => e.CartItemId).HasName("PK__CartItem__488B0B0A5DDD38B6");

            entity.HasOne(d => d.Cart).WithMany(p => p.CartItems).HasConstraintName("FK__CartItems__CartI__52593CB8");

            entity.HasOne(d => d.Product).WithMany(p => p.CartItems).HasConstraintName("FK__CartItems__Produ__534D60F1");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__Categori__19093A0B87875527");
        });

        modelBuilder.Entity<ContactMessage>(entity =>
        {
            entity.HasKey(e => e.MessageId).HasName("PK__ContactM__C87C0C9C7CED8B3E");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PK__Orders__C3905BCF7CAEE387");

            entity.Property(e => e.OrderDate).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Status).WithMany(p => p.Orders).HasConstraintName("FK__Orders__StatusId__59FA5E80");

            entity.HasOne(d => d.User).WithMany(p => p.Orders).HasConstraintName("FK__Orders__UserId__59063A47");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.OrderItemId).HasName("PK__OrderIte__57ED06818A6EB4AF");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderItems).HasConstraintName("FK__OrderItem__Order__5CD6CB2B");

            entity.HasOne(d => d.Product).WithMany(p => p.OrderItems).HasConstraintName("FK__OrderItem__Produ__5DCAEF64");
        });

        modelBuilder.Entity<OrderShipping>(entity =>
        {
            entity.HasKey(e => e.ShippingId).HasName("PK__OrderShi__5FACD5807537B656");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderShippings).HasConstraintName("FK__OrderShip__Order__68487DD7");

            entity.HasOne(d => d.ShippingMethod).WithMany(p => p.OrderShippings).HasConstraintName("FK__OrderShip__Shipp__693CA210");
        });

        modelBuilder.Entity<OrderStatus>(entity =>
        {
            entity.HasKey(e => e.StatusId).HasName("PK__OrderSta__C8EE206343FB0F05");
        });

        modelBuilder.Entity<OrderVoucher>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__OrderVou__3214EC071D99CFCD");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderVouchers).HasConstraintName("FK__OrderVouc__Order__72C60C4A");

            entity.HasOne(d => d.Voucher).WithMany(p => p.OrderVouchers).HasConstraintName("FK__OrderVouc__Vouch__73BA3083");
        });

        modelBuilder.Entity<OrderReturnRequest>(entity =>
        {
            entity.HasKey(e => e.ReturnRequestId);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status).HasDefaultValue("Pending");
            entity.HasOne(d => d.Order).WithMany(p => p.ReturnRequests).HasForeignKey(d => d.OrderId);
            entity.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(d => d.SellerConfirmedByUser).WithMany().HasForeignKey(d => d.SellerConfirmedByUserId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PaymentId).HasName("PK__Payments__9B556A38C30DDBEE");

            entity.HasOne(d => d.Order).WithMany(p => p.Payments).HasConstraintName("FK__Payments__OrderI__628FA481");

            entity.HasOne(d => d.PaymentMethod).WithMany(p => p.Payments).HasConstraintName("FK__Payments__Paymen__6383C8BA");
        });

        modelBuilder.Entity<PaymentMethod>(entity =>
        {
            entity.HasKey(e => e.PaymentMethodId).HasName("PK__PaymentM__DC31C1D347701438");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.ProductId).HasName("PK__Products__B40CC6CDCE493AEF");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Category).WithMany(p => p.Products).HasConstraintName("FK__Products__Catego__4316F928");

            entity.HasOne(d => d.Region).WithMany(p => p.Products).HasConstraintName("FK__Products__Region__440B1D61");

            entity.HasOne(d => d.Seller)
                .WithMany(p => p.SellerProducts)
                .HasForeignKey(d => d.SellerId)
                .HasConstraintName("FK_Products_Seller");
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.HasKey(e => e.ImageId).HasName("PK__ProductI__7516F70C6AD34D94");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductImages).HasConstraintName("FK__ProductIm__Produ__46E78A0C");
        });

        modelBuilder.Entity<Region>(entity =>
        {
            entity.HasKey(e => e.RegionId).HasName("PK__Regions__ACD844A359AEE9C6");
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(e => e.ReviewId).HasName("PK__Reviews__74BC79CEB7F31E0F");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Product).WithMany(p => p.Reviews).HasConstraintName("FK__Reviews__Product__6D0D32F4");

            entity.HasOne(d => d.User).WithMany(p => p.Reviews).HasConstraintName("FK__Reviews__UserId__6E01572D");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Roles__8AFACE1A0DB7EB9B");
        });

        modelBuilder.Entity<ShippingMethod>(entity =>
        {
            entity.HasKey(e => e.ShippingMethodId).HasName("PK__Shipping__0C7833A48128C00B");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4C504E6613");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.HasOne(d => d.Role).WithMany(p => p.Users).HasConstraintName("FK__Users__RoleId__3B75D760");
        });

        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.HasKey(e => e.WalletId);
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");
            entity.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WalletTransaction>(entity =>
        {
            entity.HasKey(e => e.WalletTransactionId);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.HasOne(d => d.Wallet).WithMany(p => p.Transactions).HasForeignKey(d => d.WalletId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(d => d.Order).WithMany().HasForeignKey(d => d.OrderId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<WalletTopUpRequest>(entity =>
        {
            entity.HasKey(e => e.RequestId);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status).HasDefaultValue("Pending");
            entity.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.HasKey(e => e.VoucherId).HasName("PK__Vouchers__3AEE792195B35503");
            entity.HasOne(d => d.Region).WithMany(p => p.Vouchers).HasForeignKey(d => d.RegionId);
            entity.HasOne(d => d.Category).WithMany(p => p.Vouchers).HasForeignKey(d => d.CategoryId);
        });

        // Mapping cho Occasion/ProductOccasion đã được gỡ bỏ để tránh yêu cầu bảng DB tương ứng.

        modelBuilder.Entity<ReferralReward>(entity =>
        {
            entity.HasKey(e => e.ReferralRewardId);
            entity.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId);
            entity.HasOne(d => d.Voucher).WithMany(p => p.ReferralRewards).HasForeignKey(d => d.VoucherId);
        });

        modelBuilder.Entity<Wishlist>(entity =>
        {
            entity.HasKey(e => e.WishlistId).HasName("PK__Wishlist__233189EB7BA0E7C4");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Product).WithMany(p => p.Wishlists).HasConstraintName("FK__Wishlist__Produc__4BAC3F29");

            entity.HasOne(d => d.User).WithMany(p => p.Wishlists).HasConstraintName("FK__Wishlist__UserId__4AB81AF0");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
