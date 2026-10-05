using System.Globalization;

namespace Fleet.Domain.Common;

/// <summary>
/// pt-BR formatting for user-facing messages built on the server. Explicit culture and literal separators:
/// the server culture (and Windows regional overrides) must never change what the user reads.
/// </summary>
public static class BrazilianFormat
{
    private static readonly NumberFormatInfo Numbers = new CultureInfo("pt-BR").NumberFormat;

    public static string Number(double value) => value.ToString("N0", Numbers);

    /// <summary>Used where fractional precision matters (engine hours, money) — defaults to no decimals like <see cref="Number(double)"/>.</summary>
    public static string Number(decimal value, int decimals = 0) => value.ToString(decimals == 0 ? "N0" : $"N{decimals}", Numbers);

    /// <summary>Up to one decimal, no thousands separator ("26,5", "3") — percentages and hours in messages.</summary>
    public static string Compact(decimal value) => value.ToString("0.#", Numbers);

    /// <summary>"R$ 18.420,50" — only in texts already behind the *.viewcosts permissions.</summary>
    public static string Currency(decimal value) => "R$ " + value.ToString("N2", Numbers);

    /// <summary>Signed whole percent ("+19%", "-7%") for comparisons.</summary>
    public static string SignedPercent(decimal value) => (value > 0 ? "+" : "") + Math.Round(value, 0, MidpointRounding.AwayFromZero).ToString("0", Numbers) + "%";

    public static string Date(DateOnly date) => date.ToString("dd'/'MM'/'yyyy", CultureInfo.InvariantCulture);

    public static string DateTime(DateTime local) => local.ToString("dd'/'MM'/'yyyy HH':'mm", CultureInfo.InvariantCulture);
}
