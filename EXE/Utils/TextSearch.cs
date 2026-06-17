using System;
using System.Globalization;
using System.Text;

namespace EXE.Utils;

public static class TextSearch
{
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public static bool ContainsNormalized(string normalizedHaystack, string? rawNeedle)
    {
        if (string.IsNullOrWhiteSpace(rawNeedle))
            return false;
        var normNeedle = Normalize(rawNeedle);
        if (string.IsNullOrEmpty(normNeedle))
            return false;
        return normalizedHaystack.Contains(normNeedle, StringComparison.Ordinal);
    }
}

