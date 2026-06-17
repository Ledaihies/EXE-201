using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Text;
using EXE.Models;

namespace EXE.Services;

public class InvoicePdfService : IInvoicePdfService
{
    private const int PageWidth = 1240;
    private const int PageHeight = 1754;
    private const float PdfWidth = 595f;
    private const float PdfHeight = 842f;

    public byte[] CreateSellerInvoice(Order order, User seller, IReadOnlyList<OrderItem> sellerItems)
    {
        var pages = RenderPages(order, seller, sellerItems);
        return BuildPdfFromJpegPages(pages);
    }

    private static List<byte[]> RenderPages(Order order, User seller, IReadOnlyList<OrderItem> sellerItems)
    {
        var pages = new List<byte[]>();
        var total = sellerItems.Sum(i => (i.Price ?? 0m) * (i.Quantity ?? 0));
        var chunks = sellerItems.Select((item, index) => (Item: item, Index: index))
            .Chunk(7)
            .Select(chunk => chunk.ToList())
            .ToList();

        if (chunks.Count == 0)
        {
            chunks.Add(new List<(OrderItem Item, int Index)>());
        }

        for (var pageIndex = 0; pageIndex < chunks.Count; pageIndex++)
        {
            using var bmp = new Bitmap(PageWidth, PageHeight);
            bmp.SetResolution(150, 150);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.White);

            DrawInvoicePage(g, order, seller, chunks[pageIndex], total, pageIndex + 1, chunks.Count);

            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Jpeg);
            pages.Add(ms.ToArray());
        }

        return pages;
    }

    private static void DrawInvoicePage(
        Graphics g,
        Order order,
        User seller,
        IReadOnlyList<(OrderItem Item, int Index)> items,
        decimal grandTotal,
        int pageNumber,
        int pageCount)
    {
        using var dark = new SolidBrush(Color.FromArgb(20, 31, 46));
        using var muted = new SolidBrush(Color.FromArgb(99, 116, 139));
        using var primary = new SolidBrush(Color.FromArgb(22, 101, 216));
        using var lightBlue = new SolidBrush(Color.FromArgb(232, 241, 255));
        using var borderPen = new Pen(Color.FromArgb(203, 213, 225), 2);
        using var accentPen = new Pen(Color.FromArgb(22, 101, 216), 5);

        using var titleFont = new Font("Arial", 31, FontStyle.Bold);
        using var hFont = new Font("Arial", 18, FontStyle.Bold);
        using var normalFont = new Font("Arial", 15, FontStyle.Regular);
        using var boldFont = new Font("Arial", 15, FontStyle.Bold);
        using var smallFont = new Font("Arial", 13, FontStyle.Regular);
        using var smallBold = new Font("Arial", 13, FontStyle.Bold);
        using var tableFont = new Font("Arial", 14, FontStyle.Regular);
        using var tableBold = new Font("Arial", 14, FontStyle.Bold);

        FillRoundRect(g, lightBlue, 70, 70, 1100, 180, 18);
        g.DrawLine(accentPen, 95, 92, 95, 225);
        g.DrawString("HÓA ĐƠN BÁN HÀNG", titleFont, dark, 125, 98);
        g.DrawString($"Mã hóa đơn: INV-{order.OrderId:000000}", boldFont, primary, 128, 158);
        g.DrawString($"Ngày lập: {DateTime.Now:dd/MM/yyyy HH:mm}", normalFont, muted, 128, 195);
        DrawRight(g, $"Trang {pageNumber}/{pageCount}", smallFont, muted, 1140, 210);

        DrawSectionTitle(g, "Thông tin người bán", 70, 292, hFont, primary);
        DrawTextBlock(g, new[]
        {
            ("Tên shop", seller.FullName ?? seller.Email ?? "Người bán"),
            ("Email", seller.Email ?? "Chưa cập nhật"),
            ("Điện thoại", seller.Phone ?? "Chưa cập nhật"),
            ("Địa chỉ", seller.Address ?? "Chưa cập nhật")
        }, 70, 338, 510, smallBold, smallFont, dark, muted);

        DrawSectionTitle(g, "Thông tin khách hàng", 660, 292, hFont, primary);
        DrawTextBlock(g, new[]
        {
            ("Khách hàng", order.User?.FullName ?? order.User?.Email ?? "Khách hàng"),
            ("Email", order.User?.Email ?? "Chưa cập nhật"),
            ("Điện thoại", order.User?.Phone ?? "Chưa cập nhật"),
            ("Giao đến", order.ShippingAddress ?? "Chưa cập nhật")
        }, 660, 338, 510, smallBold, smallFont, dark, muted);

        var y = 690;
        g.FillRectangle(new SolidBrush(Color.FromArgb(20, 31, 46)), 70, y, 1100, 56);
        DrawCell(g, "STT", tableBold, Brushes.White, 95, y + 18, 70, false);
        DrawCell(g, "Sản phẩm", tableBold, Brushes.White, 180, y + 18, 470, false);
        DrawCell(g, "SL", tableBold, Brushes.White, 710, y + 18, 70, true);
        DrawCell(g, "Đơn giá", tableBold, Brushes.White, 815, y + 18, 150, true);
        DrawCell(g, "Thành tiền", tableBold, Brushes.White, 1000, y + 18, 150, true);

        y += 56;
        var rowIndex = 0;
        foreach (var (item, index) in items)
        {
            var rowBrush = rowIndex % 2 == 0 ? Brushes.White : new SolidBrush(Color.FromArgb(248, 250, 252));
            g.FillRectangle(rowBrush, 70, y, 1100, 92);
            g.DrawRectangle(borderPen, 70, y, 1100, 92);

            var qty = item.Quantity ?? 0;
            var price = item.Price ?? 0m;
            var line = qty * price;
            DrawCell(g, (index + 1).ToString(), tableFont, dark, 95, y + 32, 70, false);
            DrawWrapped(g, item.Product?.ProductName ?? $"SP #{item.ProductId}", tableFont, dark, new RectangleF(180, y + 22, 470, 54));
            DrawCell(g, qty.ToString(), tableFont, dark, 710, y + 32, 70, true);
            DrawCell(g, Money(price), tableFont, dark, 815, y + 32, 150, true);
            DrawCell(g, Money(line), tableBold, dark, 1000, y + 32, 150, true);

            y += 92;
            rowIndex++;
        }

        var subtotal = items.Sum(x => (x.Item.Price ?? 0m) * (x.Item.Quantity ?? 0));
        var isLastPage = pageNumber == pageCount;

        if (isLastPage)
        {
            y += 35;
            DrawTotals(g, subtotal, grandTotal, y, hFont, normalFont, boldFont, dark, muted, primary, borderPen);
        }

        g.DrawLine(borderPen, 70, 1620, 1170, 1620);
        g.DrawString("Cảm ơn quý khách đã mua hàng tại Nguồn Việt.", smallFont, muted, 70, 1645);
        DrawRight(g, "Hóa đơn được tạo tự động từ hệ thống bán hàng.", smallFont, muted, 1170, 1645);
    }

    private static void DrawTotals(Graphics g, decimal pageSubtotal, decimal grandTotal, int y, Font hFont, Font normalFont, Font boldFont, Brush dark, Brush muted, Brush primary, Pen borderPen)
    {
        g.DrawString("Ghi chú", hFont, dark, 70, y);
        DrawWrapped(g, "Hóa đơn này dùng cho người bán xác nhận hàng hóa, doanh thu và đối soát đơn hàng trên hệ thống.", normalFont, muted, new RectangleF(70, y + 45, 560, 90));

        g.DrawRectangle(borderPen, 720, y, 450, 160);
        g.DrawString("Tạm tính", normalFont, muted, 750, y + 28);
        DrawRight(g, Money(pageSubtotal), normalFont, dark, 1140, y + 28);
        g.DrawString("Tổng thanh toán", boldFont, dark, 750, y + 82);
        DrawRight(g, Money(grandTotal), hFont, primary, 1140, y + 78);
    }

    private static void DrawTextBlock(Graphics g, IEnumerable<(string Label, string Value)> rows, int x, int y, int width, Font labelFont, Font valueFont, Brush dark, Brush muted)
    {
        foreach (var (label, value) in rows)
        {
            g.DrawString(label + ":", labelFont, dark, x, y);
            DrawWrapped(g, value, valueFont, muted, new RectangleF(x + 132, y - 3, width - 132, 58));
            y += 64;
        }
    }

    private static void DrawSectionTitle(Graphics g, string text, int x, int y, Font font, Brush brush)
    {
        g.DrawString(text, font, brush, x, y);
    }

    private static void DrawWrapped(Graphics g, string text, Font font, Brush brush, RectangleF rect)
    {
        using var format = new StringFormat { Trimming = StringTrimming.EllipsisWord, FormatFlags = StringFormatFlags.LineLimit };
        g.DrawString(text, font, brush, rect, format);
    }

    private static void DrawCell(Graphics g, string text, Font font, Brush brush, int x, int y, int width, bool right)
    {
        if (right)
        {
            DrawRight(g, text, font, brush, x + width, y);
        }
        else
        {
            g.DrawString(text, font, brush, x, y);
        }
    }

    private static void DrawRight(Graphics g, string text, Font font, Brush brush, float right, float y)
    {
        var size = g.MeasureString(text, font);
        g.DrawString(text, font, brush, right - size.Width, y);
    }

    private static string Money(decimal value)
    {
        return string.Format("{0:N0} đ", value);
    }

    private static void FillRoundRect(Graphics g, Brush brush, int x, int y, int width, int height, int radius)
    {
        using var path = new GraphicsPath();
        path.AddArc(x, y, radius, radius, 180, 90);
        path.AddArc(x + width - radius, y, radius, radius, 270, 90);
        path.AddArc(x + width - radius, y + height - radius, radius, radius, 0, 90);
        path.AddArc(x, y + height - radius, radius, radius, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }

    private static byte[] BuildPdfFromJpegPages(IReadOnlyList<byte[]> jpegPages)
    {
        var objects = new List<byte[]>();
        void Add(string text) => objects.Add(Encoding.ASCII.GetBytes(text));

        Add(string.Empty);
        Add(string.Empty);

        var pageObjectIds = new List<int>();
        for (var i = 0; i < jpegPages.Count; i++)
        {
            var imageId = objects.Count + 1;
            Add($"<< /Type /XObject /Subtype /Image /Width {PageWidth} /Height {PageHeight} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {jpegPages[i].Length} >>\nstream\n");
            var imageObject = objects[^1].Concat(jpegPages[i]).Concat(Encoding.ASCII.GetBytes("\nendstream")).ToArray();
            objects[^1] = imageObject;

            var content = $"q\n{PdfWidth} 0 0 {PdfHeight} 0 0 cm\n/Im{i + 1} Do\nQ";
            var contentId = objects.Count + 1;
            Add($"<< /Length {content.Length} >>\nstream\n{content}\nendstream");

            var pageId = objects.Count + 1;
            pageObjectIds.Add(pageId);
            Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PdfWidth} {PdfHeight}] /Resources << /XObject << /Im{i + 1} {imageId} 0 R >> >> /Contents {contentId} 0 R >>");
        }

        var kids = string.Join(" ", pageObjectIds.Select(id => $"{id} 0 R"));
        objects[0] = Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>");
        objects[1] = Encoding.ASCII.GetBytes($"<< /Type /Pages /Count {pageObjectIds.Count} /Kids [{kids}] >>");

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.ASCII, true);
        writer.Write(Encoding.ASCII.GetBytes("%PDF-1.4\n%\u00e2\u00e3\u00cf\u00d3\n"));

        var offsets = new List<long> { 0 };
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(ms.Position);
            writer.Write(Encoding.ASCII.GetBytes($"{i + 1} 0 obj\n"));
            AddBytesToWriter(writer, objects[i]);
            writer.Write(Encoding.ASCII.GetBytes("\nendobj\n"));
        }

        var xref = ms.Position;
        writer.Write(Encoding.ASCII.GetBytes($"xref\n0 {objects.Count + 1}\n"));
        writer.Write(Encoding.ASCII.GetBytes("0000000000 65535 f \n"));
        foreach (var offset in offsets.Skip(1))
        {
            writer.Write(Encoding.ASCII.GetBytes($"{offset:0000000000} 00000 n \n"));
        }

        writer.Write(Encoding.ASCII.GetBytes($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF"));
        return ms.ToArray();
    }

    private static void AddBytesToWriter(BinaryWriter writer, byte[] bytes)
    {
        writer.Write(bytes);
    }
}
