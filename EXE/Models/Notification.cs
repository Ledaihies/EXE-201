using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EXE.Models;

public partial class Notification
{
    [Key]
    public int NotificationId { get; set; }

    public int UserId { get; set; }

    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Message { get; set; } = string.Empty;

    [StringLength(50)]
    public string Type { get; set; } = "System";

    [StringLength(100)]
    public string? RelatedEntityType { get; set; }

    public int? RelatedEntityId { get; set; }

    public bool IsRead { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("Notifications")]
    public virtual User User { get; set; } = null!;
}
