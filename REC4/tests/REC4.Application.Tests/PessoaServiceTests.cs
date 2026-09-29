using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using REC4.Application.Exceptions;
using REC4.Application.Pessoas;
using REC4.Application.Pessoas.Dtos;
using REC4.Domain.Enums;
using REC4.Infrastructure.Data;
using Xunit;

namespace REC4.Application.Tests;

public class PessoaServiceTests : IDisposable
{
    private readonly Rec4DbContext _db;
    private readonly PessoaService _sut;

    public PessoaServiceTests()
    {
        _db = TestDbContextFactory.CreateInMemory();
        _sut = new PessoaService(_db);
    }

    public void Dispose() => _db.Dispose();

    private static PessoaCreateDto NovoClienteFisico(string cpf = "52998224725") => new(
        TipoPessoa.Fisica, cpf, "João da Silva", null, null, null, null, null, null, null, null, null);

    [Fact]
    public async Task CreateAsync_ComCpfDuplicadoEntreAtivos_LancaDuplicateDocumentException()
    {
        await _sut.CreateAsync(NovoClienteFisico());

        var act = () => _sut.CreateAsync(NovoClienteFisico());

        await act.Should().ThrowAsync<DuplicateDocumentException>();
    }

    [Fact]
    public async Task CreateAsync_ReaproveitandoCpfDePessoaInativada_PermiteCadastro()
    {
        var original = await _sut.CreateAsync(NovoClienteFisico());
        await _sut.SoftDeleteAsync(original.Id);

        var novo = await _sut.CreateAsync(NovoClienteFisico());

        novo.Id.Should().NotBe(original.Id);
        novo.CpfCnpj.Should().Be(original.CpfCnpj);
    }

    [Fact]
    public async Task SoftDeleteAsync_RemoveDaListagemPadraoMasMantemORegistro()
    {
        var pessoa = await _sut.CreateAsync(NovoClienteFisico());

        await _sut.SoftDeleteAsync(pessoa.Id);

        var (items, totalCount) = await _sut.ListAsync(page: 1, pageSize: 20);
        items.Should().BeEmpty();
        totalCount.Should().Be(0);

        var aindaExiste = await _db.Pessoas
            .IgnoreQueryFilters()
            .AnyAsync(p => p.Id == pessoa.Id && !p.IsActive);
        aindaExiste.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_PersisteDocumentoExatamenteComoRecebido()
    {
        // Formato/checksum do CPF é responsabilidade do PessoaCreateDtoValidator (ver DocumentValidatorTests);
        // o service apenas confia no valor já validado e o persiste como recebido.
        var dto = NovoClienteFisico("529.982.247-25");
        var pessoa = await _sut.CreateAsync(dto);

        pessoa.CpfCnpj.Should().Be("529.982.247-25");
    }

    [Fact]
    public async Task SetPerfisAsync_SubstituiConjuntoDePerfisDaPessoa()
    {
        var pessoa = await _sut.CreateAsync(NovoClienteFisico());
        var perfis = await _sut.ListPerfisAsync();
        var motorista = perfis.Single(p => p.Nome == "Motorista");
        var proprietario = perfis.Single(p => p.Nome == "Proprietário");

        var atualizado = await _sut.SetPerfisAsync(pessoa.Id, new[] { motorista.Id, proprietario.Id });
        atualizado.Perfis.Select(p => p.Nome).Should().BeEquivalentTo("Motorista", "Proprietário");

        var somenteMotorista = await _sut.SetPerfisAsync(pessoa.Id, new[] { motorista.Id });
        somenteMotorista.Perfis.Select(p => p.Nome).Should().BeEquivalentTo("Motorista");
    }
}
