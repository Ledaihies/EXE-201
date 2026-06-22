using EXE.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace EXE.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class StaffOnlyAttribute : Attribute, IAsyncActionFilter
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
        var roleName = await RoleAccess.GetRoleNameAsync(http, db, userId.Value);
        var isStaffOrAdmin = RoleAccess.IsAnyRole(roleName, RoleAccess.Staff, RoleAccess.Admin);

        if (!isStaffOrAdmin)
        {
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }
}

