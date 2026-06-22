using EXE.Services;

namespace EXE.Middleware;

public sealed class WebsiteVisitMiddleware
{
    private readonly RequestDelegate _next;

    public WebsiteVisitMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IWebsiteVisitService visitService)
    {
        await visitService.RecordVisitAsync(context);
        await _next(context);
    }
}
