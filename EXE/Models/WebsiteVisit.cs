using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EXE.Models;

[Index(nameof(VisitDate), nameof(VisitorKey), IsUnique = true)]
public partial class WebsiteVisit
{
    [Key]
    public int WebsiteVisitId { get; set; }

    [StringLength(128)]
    public string VisitorKey { get; set; } = string.Empty;

    public int? UserId { get; set; }

    [StringLength(128)]
    public string? SessionId { get; set; }

    [StringLength(45)]
    public string? IpAddress { get; set; }

    [StringLength(255)]
    public string? UserAgent { get; set; }

    [StringLength(255)]
    public string? Path { get; set; }

    [Column(TypeName = "date")]
    public DateTime VisitDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime VisitedAt { get; set; }

    [ForeignKey("UserId")]
    public virtual User? User { get; set; }
}
