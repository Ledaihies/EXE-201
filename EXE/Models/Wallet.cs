using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EXE.Models;

public class Wallet
{
    [Key]
    public int WalletId { get; set; }

    public int UserId { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Balance { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime UpdatedDate { get; set; }

    [ForeignKey("UserId")]
    public virtual User? User { get; set; }

    public virtual ICollection<WalletTransaction> Transactions { get; set; } = new List<WalletTransaction>();
}
