using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EXE.Models;

public partial class ShippingMethod
{
    [Key]
    public int ShippingMethodId { get; set; }

    [StringLength(100)]
    public string? MethodName { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal? Price { get; set; }

    [InverseProperty("ShippingMethod")]
    public virtual ICollection<OrderShipping> OrderShippings { get; set; } = new List<OrderShipping>();
}
