using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EXE.Models;

public partial class SellerPayoutItem
{
    [Key]
    public int SellerPayoutItemId { get; set; }

    public int SellerPayoutId { get; set; }

    public int OrderSettlementId { get; set; }

    public int OrderId { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal Amount { get; set; }

    [ForeignKey("SellerPayoutId")]
    [InverseProperty("SellerPayoutItems")]
    public virtual SellerPayout SellerPayout { get; set; } = null!;

    [ForeignKey("OrderSettlementId")]
    [InverseProperty("SellerPayoutItems")]
    public virtual OrderSettlement OrderSettlement { get; set; } = null!;

    [ForeignKey("OrderId")]
    [InverseProperty("SellerPayoutItems")]
    public virtual Order Order { get; set; } = null!;
}
