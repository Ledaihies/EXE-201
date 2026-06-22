using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EXE.Models;

public partial class CODCollection
{
    [Key]
    public int CODCollectionId { get; set; }

    public int OrderId { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal Amount { get; set; }

    [StringLength(50)]
    public string Status { get; set; } = "PendingCollection";

    [Column(TypeName = "datetime")]
    public DateTime? CollectedAt { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? RemittedToAdminAt { get; set; }

    public int? ConfirmedByAdminId { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [ForeignKey("OrderId")]
    [InverseProperty("CODCollection")]
    public virtual Order Order { get; set; } = null!;

    [ForeignKey("ConfirmedByAdminId")]
    public virtual User? ConfirmedByAdmin { get; set; }
}
