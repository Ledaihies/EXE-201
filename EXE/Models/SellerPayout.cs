using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EXE.Models;

public partial class SellerPayout
{
    [Key]
    public int SellerPayoutId { get; set; }

    public int SellerId { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal TotalAmount { get; set; }

    [StringLength(50)]
    public string Status { get; set; } = "Pending";

    [StringLength(100)]
    public string? PayoutMethod { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? PaidAt { get; set; }

    public int? CreatedByAdminId { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [ForeignKey("SellerId")]
    public virtual User Seller { get; set; } = null!;

    [ForeignKey("CreatedByAdminId")]
    public virtual User? CreatedByAdmin { get; set; }

    [InverseProperty("SellerPayout")]
    public virtual ICollection<SellerPayoutItem> SellerPayoutItems { get; set; } = new List<SellerPayoutItem>();
}
