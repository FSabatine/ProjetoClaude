using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using REC4.Domain.Entities;

namespace REC4.Application.Common;

public interface IRec4DbContext
{
    DbSet<Pessoa> Pessoas { get; }
    DbSet<Perfil> Perfis { get; }
    DbSet<PessoaPerfil> PessoaPerfis { get; }
    DbSet<ContaBancaria> ContasBancarias { get; }
    DbSet<Endereco> Enderecos { get; }

    DbSet<Usuario> Usuarios { get; }
    DbSet<Grupo> Grupos { get; }
    DbSet<Permissao> Permissoes { get; }
    DbSet<GrupoPermissao> GrupoPermissoes { get; }

    DbSet<Escritorio> Escritorios { get; }
    DbSet<UsuarioEscritorio> UsuarioEscritorios { get; }
    DbSet<UsuarioEmitente> UsuarioEmitentes { get; }
    DbSet<UsuarioPermissao> UsuarioPermissoes { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
