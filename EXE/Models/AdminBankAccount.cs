using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EXE.Models;

public class AdminBankAccount
{
    [Key]
    public int BankAccountId { get; set; }

    [StringLength(120)]
    public string? BankName { get; set; }

    [StringLength(50)]
    public string? AccountNumber { get; set; }

    [StringLength(120)]
    public string? AccountHolder { get; set; }

    [StringLength(300)]
    public string? TransferNote { get; set; }

    [StringLength(255)]
    public string? QrImageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    [Column(TypeName = "datetime")]
    public DateTime? UpdatedDate { get; set; }
}
