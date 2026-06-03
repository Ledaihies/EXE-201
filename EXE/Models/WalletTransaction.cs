using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EXE.Models;

public class WalletTransaction
{
    [Key]
    public int WalletTransactionId { get; set; }

    public int WalletId { get; set; }

    public int? OrderId { get; set; }

    [StringLength(30)]
    public string Type { get; set; } = "";

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal BalanceAfter { get; set; }

    [StringLength(300)]
    public string? Description { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    [ForeignKey("WalletId")]
    public virtual Wallet? Wallet { get; set; }

    [ForeignKey("OrderId")]
    public virtual Order? Order { get; set; }
}
