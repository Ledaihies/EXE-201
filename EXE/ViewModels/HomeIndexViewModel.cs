using EXE.Models;

namespace EXE.ViewModels;

public class HomeIndexViewModel
{
    public List<Product> FeaturedProducts { get; set; } = new();
    public List<Product> AdvertisedProducts { get; set; } = new();
    public List<Product> DefaultRegionProducts { get; set; } = new();
    public string DefaultRegionLabel { get; set; } = "Hà Nội";
    public List<Voucher> ActiveVouchers { get; set; } = new();

    // Tóm tắt hành trình cho trang chủ (giấy khen nhỏ)
    public HomeJourneySummaryViewModel? JourneySummary { get; set; }
}

public class HomeJourneySummaryViewModel
{
    public string? JourneyLevelName { get; set; }
    public int RegionsCount { get; set; }
    public bool HasNorth { get; set; }
    public bool HasCentral { get; set; }
    public bool HasSouth { get; set; }
    public List<string> NextGoals { get; set; } = new();
}
