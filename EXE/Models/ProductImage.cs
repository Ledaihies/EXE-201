using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EXE.Models;

public partial class ProductImage
{
    [Key]
    public int ImageId { get; set; }

    public int? ProductId { get; set; }

    [StringLength(300)]
    public string? ImageUrl { get; set; }

    [ForeignKey("ProductId")]
    [InverseProperty("ProductImages")]
    public virtual Product? Product { get; set; }
}
