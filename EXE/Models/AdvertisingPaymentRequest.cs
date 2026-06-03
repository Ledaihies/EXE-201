using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EXE.Models;

public class AdvertisingPaymentRequest
{
    [Key]
    public int RequestId { get; set; }

    public int ProductId { get; set; }

    public int SellerId { get; set; }

    [StringLength(50)]
    public string? PackageCode { get; set; }

    [StringLength(120)]
    public string? PackageName { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal Amount { get; set; }

    public int Days { get; set; }

    [StringLength(30)]
    public string? Status { get; set; }

    [StringLength(300)]
    public string? TransferContent { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ConfirmedDate { get; set; }

    public int? ConfirmedByAdminId { get; set; }

    [ForeignKey("ProductId")]
    public virtual Product? Product { get; set; }

    [ForeignKey("SellerId")]
    public virtual User? Seller { get; set; }
}
