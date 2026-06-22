using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EXE.Models;

public partial class OrderSettlement
{
    [Key]
    public int OrderSettlementId { get; set; }

    public int OrderId { get; set; }

    public int SellerId { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal GrossAmount { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal PlatformFee { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal ShippingFee { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal CodFee { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal SellerReceivable { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal AdminRevenue { get; set; }

    [StringLength(50)]
    public string PaymentMethod { get; set; } = "COD";

    [StringLength(50)]
    public string SettlementStatus { get; set; } = "NotReady";

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Column(TypeName = "datetime")]
    public DateTime? SettledAt { get; set; }

    [ForeignKey("OrderId")]
    [InverseProperty("OrderSettlements")]
    public virtual Order Order { get; set; } = null!;

    [ForeignKey("SellerId")]
    public virtual User Seller { get; set; } = null!;

    [InverseProperty("OrderSettlement")]
    public virtual ICollection<SellerPayoutItem> SellerPayoutItems { get; set; } = new List<SellerPayoutItem>();
}
