using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EXE.Models;

public partial class Review
{
    [Key]
    public int ReviewId { get; set; }

    public int? ProductId { get; set; }

    public int? UserId { get; set; }

    public int? Rating { get; set; }

    public string? Comment { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedDate { get; set; }

    [ForeignKey("ProductId")]
    [InverseProperty("Reviews")]
    public virtual Product? Product { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("Reviews")]
    public virtual User? User { get; set; }
}
