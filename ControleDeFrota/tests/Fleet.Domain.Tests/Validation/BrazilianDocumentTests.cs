using FluentAssertions;
using Fleet.Domain.Validation;

namespace Fleet.Domain.Tests.Validation;

public class CpfTests
{
    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("52998224725")]
    [InlineData(" 529 982 247 25 ")]
    public void IsValid_ValidCpf_ReturnsTrue(string cpf) => Cpf.IsValid(cpf).Should().BeTrue();

    [Theory]
    [InlineData("529.982.247-24")] // wrong second digit
    [InlineData("529.982.247-15")] // wrong first digit
    [InlineData("111.111.111-11")] // repeated digits pass the checksum but are invalid
    [InlineData("5299822472")]     // 10 digits
    [InlineData("529982247250")]   // 12 digits
    [InlineData("52998224A25")]    // letter
    [InlineData("")]
    [InlineData(null)]
    public void IsValid_InvalidCpf_ReturnsFalse(string? cpf) => Cpf.IsValid(cpf).Should().BeFalse();

    [Fact]
    public void Normalize_MaskedCpf_ReturnsDigitsOnly() => Cpf.Normalize("529.982.247-25").Should().Be("52998224725");
}

public class CnpjTests
{
    [Theory]
    [InlineData("11.222.333/0001-81")]  // numeric
    [InlineData("11222333000181")]
    [InlineData("12.ABC.345/01DE-35")]  // alphanumeric (IN RFB 2.229/2024)
    [InlineData("12abc34501de35")]      // lowercase is normalized
    public void IsValid_ValidCnpj_ReturnsTrue(string cnpj) => Cnpj.IsValid(cnpj).Should().BeTrue();

    [Theory]
    [InlineData("11.222.333/0001-82")]  // wrong check digit
    [InlineData("12.ABC.345/01DE-36")]  // wrong check digit, alphanumeric
    [InlineData("12.ABC.345/01DE-3A")]  // check digits must be numeric
    [InlineData("00.000.000/0000-00")]  // repeated
    [InlineData("1122233300018")]       // 13 characters
    [InlineData("11.222.333/0001-8!")]
    [InlineData(null)]
    public void IsValid_InvalidCnpj_ReturnsFalse(string? cnpj) => Cnpj.IsValid(cnpj).Should().BeFalse();

    [Fact]
    public void Normalize_MaskedAlphanumericCnpj_ReturnsUppercaseWithoutSeparators() =>
        Cnpj.Normalize("12.abc.345/01de-35").Should().Be("12ABC34501DE35");
}

public class LicensePlateTests
{
    [Theory]
    [InlineData("ABC1234")]  // legacy
    [InlineData("ABC-1234")]
    [InlineData("ABC1D23")]  // Mercosul
    [InlineData("abc1d23")]
    [InlineData("RDX 1A23")]
    public void IsValid_ValidPlate_ReturnsTrue(string plate) => LicensePlate.IsValid(plate).Should().BeTrue();

    [Theory]
    [InlineData("AB12345")]
    [InlineData("ABCD123")]
    [InlineData("ABC12D3")]
    [InlineData("ABC123")]
    [InlineData("ABC12345")]
    [InlineData("")]
    [InlineData(null)]
    public void IsValid_InvalidPlate_ReturnsFalse(string? plate) => LicensePlate.IsValid(plate).Should().BeFalse();

    [Fact]
    public void Normalize_MaskedLowercasePlate_ReturnsUppercaseWithoutHyphen() =>
        LicensePlate.Normalize("abc-1d23").Should().Be("ABC1D23");
}

public class RenavamTests
{
    [Theory]
    [InlineData("12345678900")]
    [InlineData("123456789")]    // legacy 9 digits, padded to 00123456789
    [InlineData("00123456789")]
    public void IsValid_ValidRenavam_ReturnsTrue(string renavam) => Renavam.IsValid(renavam).Should().BeTrue();

    [Theory]
    [InlineData("12345678901")]
    [InlineData("11111111111")]
    [InlineData("1234567")]
    [InlineData("1234567890A")]
    [InlineData(null)]
    public void IsValid_InvalidRenavam_ReturnsFalse(string? renavam) => Renavam.IsValid(renavam).Should().BeFalse();

    [Fact]
    public void Normalize_LegacyNineDigits_PadsWithZeros() => Renavam.Normalize("123456789").Should().Be("00123456789");
}

public class ChassisTests
{
    [Theory]
    [InlineData("9BWZZZ377VT004251")]
    [InlineData("9bwzzz377vt004251")]
    public void IsValid_ValidVin_ReturnsTrue(string vin) => Chassis.IsValid(vin).Should().BeTrue();

    [Theory]
    [InlineData("9BWZZZ377VT00425")]   // 16 characters
    [InlineData("9BWZZZ377VT0042511")] // 18 characters
    [InlineData("9BWZZZ377VT00425I")]  // I not allowed
    [InlineData("9BWZZZ377VT00425O")]  // O not allowed
    [InlineData("9BWZZZ377VT00425Q")]  // Q not allowed
    [InlineData(null)]
    public void IsValid_InvalidVin_ReturnsFalse(string? vin) => Chassis.IsValid(vin).Should().BeFalse();
}

public class ContactFormatTests
{
    [Theory]
    [InlineData("80010-000", true)]
    [InlineData("80010000", true)]
    [InlineData("8001000", false)]
    [InlineData("00000-000", false)]
    [InlineData("8001A000", false)]
    public void ZipCode_IsValid(string zip, bool expected) => ZipCode.IsValid(zip).Should().Be(expected);

    [Theory]
    [InlineData("(41) 99999-8888", true)]  // mobile
    [InlineData("(41) 3333-4444", true)]   // landline
    [InlineData("41 89999-8888", false)]   // 11 digits must start with 9
    [InlineData("(01) 3333-4444", false)]  // invalid area code
    [InlineData("3333-4444", false)]       // missing area code
    public void Phone_IsValid(string phone, bool expected) => Phone.IsValid(phone).Should().Be(expected);

    [Theory]
    [InlineData("frota@rodoxisto.com.br", true)]
    [InlineData(" Frota@Empresa.com ", true)]
    [InlineData("frota@empresa", false)]
    [InlineData("frota empresa@x.com", false)]
    [InlineData("@empresa.com", false)]
    [InlineData("frota@@empresa.com", false)]
    public void EmailAddress_IsValid(string email, bool expected) => EmailAddress.IsValid(email).Should().Be(expected);

    [Theory]
    [InlineData("PR", true)]
    [InlineData("sp", true)]
    [InlineData("XX", false)]
    [InlineData("", false)]
    public void BrazilianStates_IsValid(string state, bool expected) => BrazilianStates.IsValid(state).Should().Be(expected);

    [Theory]
    [InlineData("04512345678", true)]
    [InlineData("0451234567", false)]
    [InlineData("11111111111", false)]
    [InlineData("0451234567A", false)]
    public void DriverLicenseNumber_IsValid(string number, bool expected) =>
        DriverLicenseNumber.IsValid(number).Should().Be(expected);
}
