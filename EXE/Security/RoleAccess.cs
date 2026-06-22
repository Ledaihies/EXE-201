using EXE.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace EXE.Security;

public static class RoleAccess
{
    public const string Admin = "Admin";
    public const string Staff = "Staff";
    public const string Seller = "Seller";
    public const string User = "User";

    public static bool IsRole(string? actual, string expected)
    {
        var normalizedActual = NormalizeKey(actual);
        var normalizedExpected = NormalizeKey(expected);
        if (string.Equals(normalizedActual, normalizedExpected, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return GetRoleAliases(expected).Contains(normalizedActual);
    }

    public static bool IsAnyRole(string? actual, params string[] expectedRoles)
    {
        var normalized = Normalize(actual);
        return expectedRoles.Any(role => string.Equals(normalized, role, StringComparison.OrdinalIgnoreCase));
    }

    public static async Task<string> GetRoleNameAsync(HttpContext http, ApplicationDbContext db, int userId)
    {
        var roleName = Normalize(await db.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .Where(u => u.UserId == userId)
            .Select(u => u.Role != null ? u.Role.RoleName : null)
            .FirstOrDefaultAsync());

        if (!string.IsNullOrWhiteSpace(roleName))
        {
            http.Session.SetString("RoleName", roleName);
            return roleName;
        }

        return Normalize(http.Session.GetString("RoleName"));
    }

    public static string GetRoleName(HttpContext http, ApplicationDbContext db, int userId)
    {
        var roleName = Normalize(db.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .Where(u => u.UserId == userId)
            .Select(u => u.Role != null ? u.Role.RoleName : null)
            .FirstOrDefault());

        if (!string.IsNullOrWhiteSpace(roleName))
        {
            http.Session.SetString("RoleName", roleName);
            return roleName;
        }

        return Normalize(http.Session.GetString("RoleName"));
    }

    public static string Normalize(string? roleName)
    {
        return (roleName ?? string.Empty).Trim();
    }

    private static HashSet<string> GetRoleAliases(string role)
    {
        return NormalizeKey(role) switch
        {
            "admin" => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "admin",
                "administrator",
                "quantri",
                "quantrivien",
                "qtri"
            },
            "seller" => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "seller",
                "nguoiban",
                "nguoibn",
                "kenhnguoiban",
                "shop",
                "vendor",
                "merchant"
            },
            "staff" => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "staff",
                "nhanvien",
                "nhanvienkho",
                "employee"
            },
            "user" => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "user",
                "buyer",
                "customer",
                "khachhang",
                "nguoimua"
            },
            _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { NormalizeKey(role) }
        };
    }

    private static string NormalizeKey(string? value)
    {
        var text = Normalize(value).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var formD = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Replace("đ", "d", StringComparison.OrdinalIgnoreCase);
    }
}
