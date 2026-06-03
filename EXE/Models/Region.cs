using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EXE.Models;

public partial class Region
{
    [Key]
    public int RegionId { get; set; }

    [StringLength(150)]
    public string? RegionName { get; set; }

    [StringLength(150)]
    public string? Province { get; set; }

    public string? Description { get; set; }

    [StringLength(300)]
    public string? ImageUrl { get; set; }

    [InverseProperty("Region")]
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    [InverseProperty("Region")]
    public virtual ICollection<Voucher> Vouchers { get; set; } = new List<Voucher>();
}
