using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EXE.Models;

[Table("OrderShipping")]
public partial class OrderShipping
{
    [Key]
    public int ShippingId { get; set; }

    public int? OrderId { get; set; }

    public int? ShippingMethodId { get; set; }

    [StringLength(100)]
    public string? TrackingNumber { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ShippingDate { get; set; }

    [ForeignKey("OrderId")]
    [InverseProperty("OrderShippings")]
    public virtual Order? Order { get; set; }

    [ForeignKey("ShippingMethodId")]
    [InverseProperty("OrderShippings")]
    public virtual ShippingMethod? ShippingMethod { get; set; }
}
