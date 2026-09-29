namespace Fleet.Domain.Common;

/// <summary>Brazilian postal address, stored inline in the owner's table.</summary>
public sealed class Address
{
    public const int StreetMaxLength = 150;
    public const int NumberMaxLength = 20;
    public const int ComplementMaxLength = 80;
    public const int NeighborhoodMaxLength = 80;
    public const int CityMaxLength = 80;

    public string? Street { get; set; }
    public string? Number { get; set; }
    public string? Complement { get; set; }
    public string? Neighborhood { get; set; }
    public string? City { get; set; }
    /// <summary>UF, two uppercase letters.</summary>
    public string? State { get; set; }
    /// <summary>CEP, 8 digits.</summary>
    public string? ZipCode { get; set; }
}
