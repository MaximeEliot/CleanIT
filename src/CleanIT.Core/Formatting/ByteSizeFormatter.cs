using System.Globalization;

namespace CleanIT.Core.Formatting;

/// <summary>Human readable sizes in French ("1,5 Go", "320 Mo", "512 octets").</summary>
public static class ByteSizeFormatter
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly string[] Units = ["octets", "Ko", "Mo", "Go", "To", "Po"];

    public static string Format(long bytes)
    {
        if (bytes < 0)
        {
            return "-" + Format(-bytes);
        }

        if (bytes < 1024)
        {
            return bytes.ToString("N0", French) + (bytes < 2 ? " octet" : " octets");
        }

        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        var format = value < 10 ? "0.##" : value < 100 ? "0.#" : "0";
        return value.ToString(format, French) + " " + Units[unit];
    }
}
