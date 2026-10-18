using System.Globalization;

namespace CleanIT.Core.Formatting;

/// <summary>
/// fr-FR culture whose thousands separator is a regular no-break space (U+00A0).
/// The ICU default (narrow no-break space, U+202F) is missing from some Segoe UI weights
/// and renders as no space at all ("1984" instead of "1 984").
/// </summary>
public static class FrenchCulture
{
    public static CultureInfo Instance { get; } = Create();

    private static CultureInfo Create()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("fr-FR").Clone();
        culture.NumberFormat.NumberGroupSeparator = " ";
        culture.NumberFormat.CurrencyGroupSeparator = " ";
        culture.NumberFormat.PercentGroupSeparator = " ";
        return CultureInfo.ReadOnly(culture);
    }
}
