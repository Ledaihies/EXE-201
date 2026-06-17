using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EXE.Models;

public class OrderReturnRequest
{
    [Key]
    public int ReturnRequestId { get; set; }

    public int OrderId { get; set; }

    public int UserId { get; set; }

    [StringLength(1000)]
    public string Reason { get; set; } = string.Empty;

    [StringLength(300)]
    public string ProofImageUrl { get; set; } = string.Empty;

    [StringLength(30)]
    public string Status { get; set; } = "Pending";

    [Column(TypeName = "decimal(10, 2)")]
    public decimal RefundAmount { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? SellerConfirmedDate { get; set; }

    public int? SellerConfirmedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? RefundedDate { get; set; }

    [ForeignKey(nameof(OrderId))]
    [InverseProperty(nameof(Order.ReturnRequests))]
    public virtual Order? Order { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual User? User { get; set; }

    [ForeignKey(nameof(SellerConfirmedByUserId))]
    public virtual User? SellerConfirmedByUser { get; set; }
}
