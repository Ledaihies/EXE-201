using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EXE.Models;

public class Occasion
{
    [Key]
    public int OccasionId { get; set; }

    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [StringLength(150)]
    public string? Name { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(300)]
    public string? ImageUrl { get; set; }

    public int SortOrder { get; set; }

    [InverseProperty("Occasion")]
    public virtual ICollection<ProductOccasion> ProductOccasions { get; set; } = new List<ProductOccasion>();
}
