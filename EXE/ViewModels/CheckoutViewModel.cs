using EXE.Models;

namespace EXE.ViewModels;

public class CheckoutViewModel
{
    public List<CartItem> Items { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total => Math.Max(0, Subtotal + ShippingFee - DiscountAmount);

    public string? VoucherCode { get; set; }
    public string? ShippingAddress { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactName { get; set; }
    public int? ToDistrictId { get; set; }
    public string? ToWardCode { get; set; }
    public string? Note { get; set; }
}

