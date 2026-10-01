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

    public static string Date(DateOnly date) => date.ToString("dd'/'MM'/'yyyy", CultureInfo.InvariantCulture);

    public static string DateTime(DateTime local) => local.ToString("dd'/'MM'/'yyyy HH':'mm", CultureInfo.InvariantCulture);
}
