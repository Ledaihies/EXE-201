using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EXE.Models;

public class AdvertisingPackage
{
    [Key]
    public int PackageId { get; set; }

    [StringLength(50)]
    public string? Code { get; set; }

    [StringLength(120)]
    public string? Name { get; set; }

    [StringLength(300)]
    public string? Description { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal Price { get; set; }

    public int Days { get; set; }

    public bool IsActive { get; set; } = true;

    [Column(TypeName = "datetime")]
    public DateTime? CreatedDate { get; set; }
}
