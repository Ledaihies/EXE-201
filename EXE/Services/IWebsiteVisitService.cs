using EXE.ViewModels;

namespace EXE.Services;

public interface IWebsiteVisitService
{
    Task RecordVisitAsync(HttpContext context);

    Task<WebsiteVisitStatsViewModel> GetStatsAsync(DateTime? from = null, DateTime? to = null);
}
