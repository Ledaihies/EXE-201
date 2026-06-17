using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EXE.Models;

[Table("OrderStatus")]
public partial class OrderStatus
{
    [Key]
    public int StatusId { get; set; }

    [StringLength(50)]
    public string? StatusName { get; set; }

    [InverseProperty("Status")]
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
