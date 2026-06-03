using EXE.Models;

namespace EXE.Services;

public interface IInvoicePdfService
{
    byte[] CreateSellerInvoice(Order order, User seller, IReadOnlyList<OrderItem> sellerItems);
}
