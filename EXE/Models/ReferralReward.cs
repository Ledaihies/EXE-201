using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EXE.Models;

public class ReferralReward
{
    [Key]
    public int ReferralRewardId { get; set; }

    public int UserId { get; set; }
    public int VoucherId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;

    [ForeignKey("VoucherId")]
    public virtual Voucher Voucher { get; set; } = null!;
}
