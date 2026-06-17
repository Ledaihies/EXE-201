using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EXE.Models;

public partial class Voucher
{
    [Key]
    public int VoucherId { get; set; }

    [StringLength(50)]
    public string? Code { get; set; }

    [StringLength(300)]
    public string? Description { get; set; }

    /// <summary>Giảm theo % (ưu tiên nếu &gt; 0).</summary>
    public int? DiscountPercent { get; set; }

    /// <summary>Giảm số tiền cố định (khi DiscountPercent null hoặc 0).</summary>
    [Column(TypeName = "decimal(12, 2)")]
    public decimal? DiscountFixed { get; set; }

    public bool IsFreeShipping { get; set; }

    public bool IsActive { get; set; } = true;

    [Column(TypeName = "decimal(12, 2)")]
    public decimal? MinOrderAmount { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? StartDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ExpiryDate { get; set; }

    /// <summary>Số lần dùng tối đa (null = không giới hạn).</summary>
    public int? MaxUses { get; set; }

    /// <summary>Chỉ áp dụng cho đơn có sản phẩm thuộc vùng này (null = mọi vùng).</summary>
    public int? RegionId { get; set; }

    /// <summary>Chỉ áp dụng cho đơn có sản phẩm thuộc danh mục này (null = mọi danh mục).</summary>
    public int? CategoryId { get; set; }

    /// <summary>Dịp: Tet, 83, Corporate... (null = mọi dịp).</summary>
    [StringLength(50)]
    public string? OccasionTag { get; set; }

    [ForeignKey("RegionId")]
    [InverseProperty("Vouchers")]
    public virtual Region? Region { get; set; }

    [ForeignKey("CategoryId")]
    [InverseProperty("Vouchers")]
    public virtual Category? Category { get; set; }

    [InverseProperty("Voucher")]
    public virtual ICollection<OrderVoucher> OrderVouchers { get; set; } = new List<OrderVoucher>();

    [InverseProperty("Voucher")]
    public virtual ICollection<ReferralReward> ReferralRewards { get; set; } = new List<ReferralReward>();
}
