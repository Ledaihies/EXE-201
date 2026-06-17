using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EXE.Models;

public class WalletTopUpRequest
{
    [Key]
    public int RequestId { get; set; }

    public int UserId { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Amount { get; set; }

    [StringLength(30)]
    public string Status { get; set; } = "Pending";

    [StringLength(50)]
    public string TransferContent { get; set; } = "";

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ConfirmedDate { get; set; }

    [ForeignKey("UserId")]
    public virtual User? User { get; set; }
}
