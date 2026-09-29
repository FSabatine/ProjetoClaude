using FluentAssertions;
using REC4.Application.Escritorios;
using REC4.Application.Escritorios.Dtos;
using REC4.Application.Exceptions;
using REC4.Infrastructure.Data;
using Xunit;

namespace REC4.Application.Tests.Escritorios;

public class EscritorioServiceTests : IDisposable
{
    private readonly Rec4DbContext _db;
    private readonly EscritorioService _sut;

    public EscritorioServiceTests()
    {
        _db = TestDbContextFactory.CreateInMemory();
        _sut = new EscritorioService(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task CreateAsync_PersisteEscritorioAtivo()
    {
        var escritorio = await _sut.CreateAsync(new EscritorioCreateDto("Matriz"));

        escritorio.Nome.Should().Be("Matriz");
        escritorio.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_PermiteInativar()
    {
        var escritorio = await _sut.CreateAsync(new EscritorioCreateDto("Matriz"));

        var atualizado = await _sut.UpdateAsync(escritorio.Id, new EscritorioUpdateDto("Matriz", false));

        atualizado.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_ComIdInexistente_LancaNotFoundException()
    {
        var act = () => _sut.GetByIdAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
