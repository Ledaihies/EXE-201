using EXE.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace EXE.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AdminOnlyAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        var userId = http.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            context.Result = new RedirectToActionResult("Login", "Auth", null);
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var isAdmin = await db.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .AnyAsync(u => u.UserId == userId.Value &&
                           u.Role != null &&
                           u.Role.RoleName == "Admin");

        if (!isAdmin)
        {
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }
}

