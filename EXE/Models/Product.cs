using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EXE.Models;

public partial class Product
{
    [Key]
    public int ProductId { get; set; }

    [StringLength(200)]
    public string? ProductName { get; set; }

    public string? Description { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal? Price { get; set; }

    public int? Stock { get; set; }

    public int? CategoryId { get; set; }

    public int? RegionId { get; set; }

    public int? SellerId { get; set; }

    [StringLength(20)]
    public string? ApprovalStatus { get; set; }

    [StringLength(255)]
    public string? OriginProofImageUrl { get; set; }

    public int? ViewCount { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal? AdvertisingBudget { get; set; }

    [StringLength(50)]
    public string? AdvertisingPackageCode { get; set; }

    [StringLength(120)]
    public string? AdvertisingPackageName { get; set; }

    public int? AdvertisingDays { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? AdvertisingPaidDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? AdvertisingEndDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedDate { get; set; }

    [InverseProperty("Product")]
    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    [ForeignKey("CategoryId")]
    [InverseProperty("Products")]
    public virtual Category? Category { get; set; }

    [InverseProperty("Product")]
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    [InverseProperty("Product")]
    public virtual ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();

    [ForeignKey("RegionId")]
    [InverseProperty("Products")]
    public virtual Region? Region { get; set; }

    [ForeignKey("SellerId")]
    [InverseProperty("SellerProducts")]
    public virtual User? Seller { get; set; }

    [InverseProperty("Product")]
    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    [InverseProperty("Product")]
    public virtual ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();

    [InverseProperty("Product")]
    public virtual ICollection<ProductOccasion> ProductOccasions { get; set; } = new List<ProductOccasion>();
}
