using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EXE.Models;

public class ProductOccasion
{
    public int ProductId { get; set; }
    public int OccasionId { get; set; }

    [ForeignKey("ProductId")]
    public virtual Product Product { get; set; } = null!;

    [ForeignKey("OccasionId")]
    public virtual Occasion Occasion { get; set; } = null!;
}
