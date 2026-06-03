using EXE.Models;

namespace EXE.ViewModels;

public class WalletTopUpPaymentViewModel
{
    public WalletTopUpRequest Request { get; set; } = new();
    public string QrUrl { get; set; } = "";
    public bool IsPaid { get; set; }
}
