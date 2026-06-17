using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EXE.Models;

/// <summary>
/// Mã giới thiệu của user. Bạn bè đăng ký + đặt hàng thì cả hai nhận thưởng (voucher/điểm).
/// </summary>
public partial class Referral
{
    [Key]
    public int ReferralId { get; set; }

    public int ReferrerUserId { get; set; }

    [StringLength(50)]
    public string Code { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Số lần tối đa mã có thể dùng (null = không giới hạn).</summary>
    public int? MaxUses { get; set; }

    public int UsedCount { get; set; }

    [ForeignKey("ReferrerUserId")]
    [InverseProperty("Referrals")]
    public virtual User ReferrerUser { get; set; } = null!;

    [InverseProperty("Referral")]
    public virtual ICollection<ReferralUsage> ReferralUsages { get; set; } = new List<ReferralUsage>();
}
