using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EXE.Models;

public partial class Category
{
    [Key]
    public int CategoryId { get; set; }

    [StringLength(150)]
    public string? CategoryName { get; set; }

    [StringLength(300)]
    public string? Description { get; set; }

    [InverseProperty("Category")]
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    [InverseProperty("Category")]
    public virtual ICollection<Voucher> Vouchers { get; set; } = new List<Voucher>();
}
