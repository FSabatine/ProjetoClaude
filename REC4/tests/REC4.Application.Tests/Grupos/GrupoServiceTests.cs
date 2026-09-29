using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using REC4.Application.Exceptions;
using REC4.Application.Grupos;
using REC4.Application.Grupos.Dtos;
using REC4.Infrastructure.Data;
using Xunit;

namespace REC4.Application.Tests.Grupos;

public class GrupoServiceTests : IDisposable
{
    private readonly Rec4DbContext _db;
    private readonly GrupoService _sut;

    public GrupoServiceTests()
    {
        _db = TestDbContextFactory.CreateInMemory();
        _sut = new GrupoService(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<int[]> AlgumasPermissaoIdsAsync(int quantidade) =>
        await _db.Permissoes.OrderBy(p => p.Id).Take(quantidade).Select(p => p.Id).ToArrayAsync();

    [Fact]
    public async Task CreateAsync_PersisteGrupoComPermissoesInformadas()
    {
        var permissaoIds = await AlgumasPermissaoIdsAsync(2);

        var grupo = await _sut.CreateAsync(new GrupoCreateDto("Grupo Teste", permissaoIds));

        grupo.Nome.Should().Be("Grupo Teste");
        grupo.Permissoes.Select(p => p.Id).Should().BeEquivalentTo(permissaoIds);
    }

    [Fact]
    public async Task CreateAsync_ComNomeDuplicado_LancaDuplicateGroupNameException()
    {
        await _sut.CreateAsync(new GrupoCreateDto("Grupo Teste", Array.Empty<int>()));

        var act = () => _sut.CreateAsync(new GrupoCreateDto("Grupo Teste", Array.Empty<int>()));

        await act.Should().ThrowAsync<DuplicateGroupNameException>();
    }

    [Fact]
    public async Task UpdateAsync_SubstituiConjuntoDePermissoesDoGrupo()
    {
        var permissaoIds = await AlgumasPermissaoIdsAsync(3);
        var grupo = await _sut.CreateAsync(new GrupoCreateDto("Grupo Teste", permissaoIds[..2]));

        var atualizado = await _sut.UpdateAsync(grupo.Id, new GrupoUpdateDto("Grupo Renomeado", new[] { permissaoIds[2] }));

        atualizado.Nome.Should().Be("Grupo Renomeado");
        atualizado.Permissoes.Select(p => p.Id).Should().BeEquivalentTo(new[] { permissaoIds[2] });
    }
}
