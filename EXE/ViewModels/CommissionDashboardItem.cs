namespace EXE.ViewModels;

public class CommissionDashboardItem
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public int? SellerId { get; set; }
    public string SellerName { get; set; } = "";
    public string? SellerBankName { get; set; }
    public string? SellerBankAccountNumber { get; set; }
    public string? SellerBankAccountName { get; set; }
    public string? SellerBankBranch { get; set; }
    public int QuantitySold { get; set; }
    public int PaidOrderCount { get; set; }
    public decimal GrossRevenue { get; set; }
    public decimal AdminCommission { get; set; }
    public decimal ShippingRefundAmount { get; set; }
    public decimal SellerPayout { get; set; }
}
