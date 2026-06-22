using EXE.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace EXE.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SellerOnlyAttribute : Attribute, IAsyncActionFilter
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

        var sessionRole = RoleAccess.Normalize(http.Session.GetString("RoleName"));
        if (RoleAccess.IsAnyRole(sessionRole, RoleAccess.Seller, RoleAccess.Admin))
        {
            await next();
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var roleName = await RoleAccess.GetRoleNameAsync(http, db, userId.Value);
        var isSellerOrAdmin = RoleAccess.IsAnyRole(roleName, RoleAccess.Seller, RoleAccess.Admin);

        if (!isSellerOrAdmin)
        {
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }
}
