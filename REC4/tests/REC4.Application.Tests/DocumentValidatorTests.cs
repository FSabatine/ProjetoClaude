using FluentAssertions;
using REC4.Application.Validation;
using REC4.Domain.Enums;
using Xunit;

namespace REC4.Application.Tests;

public class DocumentValidatorTests
{
    [Theory]
    [InlineData("52998224725")] // CPF valido conhecido
    [InlineData("111.444.777-35")]
    public void IsValid_ComCpfValido_RetornaTrue(string cpf)
    {
        DocumentValidator.IsValid(cpf, TipoPessoa.Fisica).Should().BeTrue();
    }

    [Theory]
    [InlineData("11111111111")] // digitos repetidos
    [InlineData("12345678900")] // digito verificador invalido
    [InlineData("123")] // tamanho invalido
    public void IsValid_ComCpfInvalido_RetornaFalse(string cpf)
    {
        DocumentValidator.IsValid(cpf, TipoPessoa.Fisica).Should().BeFalse();
    }

    [Theory]
    [InlineData("11222333000181")] // CNPJ valido conhecido
    [InlineData("11.222.333/0001-81")]
    public void IsValid_ComCnpjValido_RetornaTrue(string cnpj)
    {
        DocumentValidator.IsValid(cnpj, TipoPessoa.Juridica).Should().BeTrue();
    }

    [Theory]
    [InlineData("11111111111111")] // digitos repetidos
    [InlineData("11222333000199")] // digito verificador invalido
    [InlineData("123")] // tamanho invalido
    public void IsValid_ComCnpjInvalido_RetornaFalse(string cnpj)
    {
        DocumentValidator.IsValid(cnpj, TipoPessoa.Juridica).Should().BeFalse();
    }
}
