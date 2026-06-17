using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EXE.Models;

/// <summary>
/// Chiến dịch khuyến mãi (ví dụ: Tết, 8/3, đặc sản Miền Trung giảm 10%).
/// Voucher có thể gắn với Campaign và/hoặc Region.
/// </summary>
public partial class Campaign
{
    [Key]
    public int CampaignId { get; set; }

    [StringLength(200)]
    public string? Name { get; set; }

    public string? Description { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? StartDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? EndDate { get; set; }

    /// <summary>Null = toàn hệ thống; có giá trị = chỉ áp cho vùng đó.</summary>
    public int? RegionId { get; set; }

    public bool IsActive { get; set; } = true;

    [Column(TypeName = "datetime")]
    public DateTime? CreatedDate { get; set; }

    [ForeignKey("RegionId")]
    [InverseProperty("Campaigns")]
    public virtual Region? Region { get; set; }

    [InverseProperty("Campaign")]
    public virtual ICollection<Voucher> Vouchers { get; set; } = new List<Voucher>();
}
