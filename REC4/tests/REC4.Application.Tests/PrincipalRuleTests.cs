using FluentAssertions;
using REC4.Application.Pessoas;
using REC4.Application.Pessoas.Dtos;
using REC4.Domain.Enums;
using REC4.Infrastructure.Data;
using Xunit;

namespace REC4.Application.Tests;

public class PrincipalRuleTests : IDisposable
{
    private readonly Rec4DbContext _db;
    private readonly PessoaService _sut;

    public PrincipalRuleTests()
    {
        _db = TestDbContextFactory.CreateInMemory();
        _sut = new PessoaService(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<Guid> CriarPessoaAsync()
    {
        var pessoa = await _sut.CreateAsync(new PessoaCreateDto(
            TipoPessoa.Fisica, "52998224725", "João da Silva", null, null, null, null, null, null, null, null, null));
        return pessoa.Id;
    }

    [Fact]
    public async Task AddContaBancariaAsync_PrimeiraConta_TornaSePrincipalAutomaticamente()
    {
        var pessoaId = await CriarPessoaAsync();

        var conta = await _sut.AddContaBancariaAsync(pessoaId,
            new ContaBancariaCreateDto("Banco A", "0001", "12345", "6", "Corrente", IsPrincipal: false));

        conta.IsPrincipal.Should().BeTrue();
    }

    [Fact]
    public async Task AddContaBancariaAsync_NovaContaPrincipal_DesmarcaAAnterior()
    {
        var pessoaId = await CriarPessoaAsync();
        var primeira = await _sut.AddContaBancariaAsync(pessoaId,
            new ContaBancariaCreateDto("Banco A", "0001", "111", "1", "Corrente", IsPrincipal: true));

        var segunda = await _sut.AddContaBancariaAsync(pessoaId,
            new ContaBancariaCreateDto("Banco B", "0002", "222", "2", "Poupança", IsPrincipal: true));

        var pessoa = await _sut.GetByIdAsync(pessoaId);
        pessoa.ContasBancarias.Single(c => c.Id == primeira.Id).IsPrincipal.Should().BeFalse();
        pessoa.ContasBancarias.Single(c => c.Id == segunda.Id).IsPrincipal.Should().BeTrue();
    }

    [Fact]
    public async Task SetContaPrincipalAsync_TrocaAContaPrincipalDeFormaExclusiva()
    {
        var pessoaId = await CriarPessoaAsync();
        var primeira = await _sut.AddContaBancariaAsync(pessoaId,
            new ContaBancariaCreateDto("Banco A", "0001", "111", "1", "Corrente", IsPrincipal: true));
        var segunda = await _sut.AddContaBancariaAsync(pessoaId,
            new ContaBancariaCreateDto("Banco B", "0002", "222", "2", "Poupança", IsPrincipal: false));

        await _sut.SetContaPrincipalAsync(pessoaId, segunda.Id);

        var pessoa = await _sut.GetByIdAsync(pessoaId);
        pessoa.ContasBancarias.Single(c => c.Id == primeira.Id).IsPrincipal.Should().BeFalse();
        pessoa.ContasBancarias.Single(c => c.Id == segunda.Id).IsPrincipal.Should().BeTrue();
    }

    [Fact]
    public async Task AddEnderecoAsync_NovoEnderecoPrincipal_DesmarcaOAnterior()
    {
        var pessoaId = await CriarPessoaAsync();
        var primeiro = await _sut.AddEnderecoAsync(pessoaId,
            new EnderecoCreateDto("Rua A", "100", null, "Centro", "São Paulo", "SP", "01000-000", "Brasil", IsPrincipal: true));

        var segundo = await _sut.AddEnderecoAsync(pessoaId,
            new EnderecoCreateDto("Rua B", "200", null, "Centro", "São Paulo", "SP", "01000-001", "Brasil", IsPrincipal: true));

        var pessoa = await _sut.GetByIdAsync(pessoaId);
        pessoa.Enderecos.Single(e => e.Id == primeiro.Id).IsPrincipal.Should().BeFalse();
        pessoa.Enderecos.Single(e => e.Id == segundo.Id).IsPrincipal.Should().BeTrue();
    }

    [Fact]
    public async Task SetEnderecoPrincipalAsync_TrocaOEnderecoPrincipalDeFormaExclusiva()
    {
        var pessoaId = await CriarPessoaAsync();
        var primeiro = await _sut.AddEnderecoAsync(pessoaId,
            new EnderecoCreateDto("Rua A", "100", null, "Centro", "São Paulo", "SP", "01000-000", "Brasil", IsPrincipal: true));
        var segundo = await _sut.AddEnderecoAsync(pessoaId,
            new EnderecoCreateDto("Rua B", "200", null, "Centro", "São Paulo", "SP", "01000-001", "Brasil", IsPrincipal: false));

        await _sut.SetEnderecoPrincipalAsync(pessoaId, segundo.Id);

        var pessoa = await _sut.GetByIdAsync(pessoaId);
        pessoa.Enderecos.Single(e => e.Id == primeiro.Id).IsPrincipal.Should().BeFalse();
        pessoa.Enderecos.Single(e => e.Id == segundo.Id).IsPrincipal.Should().BeTrue();
    }
}
