namespace EXE.ViewModels;

public sealed class WebsiteVisitStatsViewModel
{
    public int TotalVisits { get; set; }

    public int TodayVisits { get; set; }

    public int UniqueVisitors { get; set; }

    public int ProductViews { get; set; }

    public List<int> DailyVisits { get; set; } = new();
}
