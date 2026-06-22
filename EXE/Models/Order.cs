using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EXE.Models;

public partial class Order
{
    [Key]
    public int OrderId { get; set; }

    public int? UserId { get; set; }

    public int? StatusId { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal? TotalAmount { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal? ShippingFee { get; set; }

    [StringLength(50)]
    public string? PaymentStatus { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? OrderDate { get; set; }

    [StringLength(300)]
    public string? ShippingAddress { get; set; }

    [StringLength(150)]
    public string? ReceiverName { get; set; }

    [StringLength(20)]
    public string? ReceiverPhone { get; set; }

    public int? ToDistrictId { get; set; }

    [StringLength(20)]
    public string? ToWardCode { get; set; }

    [StringLength(500)]
    public string? ShippingNote { get; set; }

    [InverseProperty("Order")]
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    [InverseProperty("Order")]
    public virtual ICollection<OrderShipping> OrderShippings { get; set; } = new List<OrderShipping>();

    [InverseProperty("Order")]
    public virtual ICollection<OrderVoucher> OrderVouchers { get; set; } = new List<OrderVoucher>();

    [InverseProperty("Order")]
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    [InverseProperty("Order")]
    public virtual ICollection<OrderReturnRequest> ReturnRequests { get; set; } = new List<OrderReturnRequest>();

    [InverseProperty("Order")]
    public virtual ICollection<OrderSettlement> OrderSettlements { get; set; } = new List<OrderSettlement>();

    [InverseProperty("Order")]
    public virtual ICollection<SellerPayoutItem> SellerPayoutItems { get; set; } = new List<SellerPayoutItem>();

    [InverseProperty("Order")]
    public virtual CODCollection? CODCollection { get; set; }

    [ForeignKey("StatusId")]
    [InverseProperty("Orders")]
    public virtual OrderStatus? Status { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("Orders")]
    public virtual User? User { get; set; }
}
