using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EXE.Models;

/// <summary>
/// Ghi nhận bạn bè dùng mã giới thiệu: ai đăng ký, đơn hàng đầu tiên (nếu có).
/// Dùng để trả thưởng cho cả người giới thiệu và người được giới thiệu.
/// </summary>
[Table("ReferralUsage")]
public partial class ReferralUsage
{
    [Key]
    public int Id { get; set; }

    public int ReferralId { get; set; }

    public int ReferredUserId { get; set; }

    /// <summary>Đơn hàng đầu tiên của người được giới thiệu (sau khi đặt mới tính thưởng).</summary>
    public int? OrderId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime UsedAt { get; set; }

    /// <summary>Đã trả thưởng cho người giới thiệu (voucher/điểm).</summary>
    public bool RewardGivenToReferrer { get; set; }

    /// <summary>Đã trả thưởng cho người được giới thiệu.</summary>
    public bool RewardGivenToReferred { get; set; }

    [ForeignKey("OrderId")]
    [InverseProperty("ReferralUsages")]
    public virtual Order? Order { get; set; }

    [ForeignKey("ReferralId")]
    [InverseProperty("ReferralUsages")]
    public virtual Referral Referral { get; set; } = null!;

    [ForeignKey("ReferredUserId")]
    [InverseProperty("ReferralUsagesAsReferred")]
    public virtual User ReferredUser { get; set; } = null!;
}
