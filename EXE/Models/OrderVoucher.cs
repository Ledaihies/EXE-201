using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EXE.Models;

[Table("OrderVoucher")]
public partial class OrderVoucher
{
    [Key]
    public int Id { get; set; }

    public int? OrderId { get; set; }

    public int? VoucherId { get; set; }

    [ForeignKey("OrderId")]
    [InverseProperty("OrderVouchers")]
    public virtual Order? Order { get; set; }

    [ForeignKey("VoucherId")]
    [InverseProperty("OrderVouchers")]
    public virtual Voucher? Voucher { get; set; }
}
