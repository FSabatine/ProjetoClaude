using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using REC4.Application.Common;
using REC4.Domain.Entities;

namespace REC4.Infrastructure.Data;

public class Rec4DbContext : DbContext, IRec4DbContext
{
    public Rec4DbContext(DbContextOptions<Rec4DbContext> options) : base(options)
    {
    }

    public DbSet<Pessoa> Pessoas => Set<Pessoa>();
    public DbSet<Perfil> Perfis => Set<Perfil>();
    public DbSet<PessoaPerfil> PessoaPerfis => Set<PessoaPerfil>();
    public DbSet<ContaBancaria> ContasBancarias => Set<ContaBancaria>();
    public DbSet<Endereco> Enderecos => Set<Endereco>();

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Grupo> Grupos => Set<Grupo>();
    public DbSet<Permissao> Permissoes => Set<Permissao>();
    public DbSet<GrupoPermissao> GrupoPermissoes => Set<GrupoPermissao>();

    public DbSet<Escritorio> Escritorios => Set<Escritorio>();
    public DbSet<UsuarioEscritorio> UsuarioEscritorios => Set<UsuarioEscritorio>();
    public DbSet<UsuarioEmitente> UsuarioEmitentes => Set<UsuarioEmitente>();
    public DbSet<UsuarioPermissao> UsuarioPermissoes => Set<UsuarioPermissao>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Database.BeginTransactionAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Rec4DbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
