using EXE.Models;

namespace EXE.ViewModels;

public class JourneyStopViewModel
{
    public int RegionId { get; set; }
    public string RegionName { get; set; } = string.Empty;
    public string? Province { get; set; }
    public DateTime? FirstVisitedAt { get; set; }
    public int OrdersCount { get; set; }
    public decimal TotalAmount { get; set; }
    public List<Product> SampleProducts { get; set; } = new();
}

public class CustomerJourneyViewModel
{
    public List<JourneyStopViewModel> Stops { get; set; } = new();
    public int TotalOrders { get; set; }
    public decimal TotalAmount { get; set; }

    // Tổng số vùng đã "check-in"
    public int RegionsCount { get; set; }

    // Các huy hiệu đạt được (gamification đơn giản)
    public List<string> Badges { get; set; } = new();

    // Gợi ý điểm đến / vùng tiếp theo (dạng text)
    public List<string> Suggestions { get; set; } = new();

    // Gợi ý vùng tiếp theo có link (RegionId để dẫn tới /Product?regionId=...)
    public List<NextRegionSuggestionViewModel> NextRegionSuggestions { get; set; } = new();

    // Tiến độ 3 miền: đã ghé Bắc / Trung / Nam (để hiển thị progress)
    public bool HasNorth { get; set; }
    public bool HasCentral { get; set; }
    public bool HasSouth { get; set; }

    /// <summary>Cấp độ hành trình: Khám phá (1-2), Du khách (3-4), Nhà thám hiểm (5+). Null khi chưa có vùng nào.</summary>
    public string? JourneyLevelName { get; set; }

    /// <summary>Thứ tự cấp độ để hiển thị (1=Khám phá, 2=Du khách, 3=Nhà thám hiểm).</summary>
    public int JourneyLevelOrder { get; set; }

    /// <summary>Mục tiêu nhỏ tiếp theo: ví dụ "Mua thêm 1 vùng nữa để nhận huy hiệu Phượt thủ".</summary>
    public List<string> NextGoals { get; set; } = new();
}

public class NextRegionSuggestionViewModel
{
    public int RegionId { get; set; }
    public string RegionName { get; set; } = string.Empty;
    public string? Province { get; set; }
    /// <summary>Lý do gợi ý ngắn gọn, ví dụ: "Hoàn thành trải nghiệm 3 miền"</summary>
    public string Reason { get; set; } = string.Empty;
}

