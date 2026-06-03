using EXE.Models;

namespace EXE.ViewModels;

public class BankTransferPaymentViewModel
{
    public Order Order { get; set; } = new();
    public string PaymentCode { get; set; } = "";
    public string QrUrl { get; set; } = "";
    public bool IsPaid { get; set; }
}
