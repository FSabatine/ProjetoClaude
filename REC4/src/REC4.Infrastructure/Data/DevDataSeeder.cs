using Microsoft.EntityFrameworkCore;
using REC4.Application.Common;
using REC4.Domain.Entities;
using REC4.Domain.Seed;

namespace REC4.Infrastructure.Data;

// Usuarios de desenvolvimento para permitir o primeiro login antes de existir uma tela de
// administracao de usuarios funcional. So roda em Development (ver Program.cs) e so cria
// registros se a tabela Usuarios estiver vazia (idempotente).
public static class DevDataSeeder
{
    public static async Task SeedDevUsersAsync(Rec4DbContext db, IPasswordHasher passwordHasher, CancellationToken cancellationToken = default)
    {
        if (await db.Usuarios.AnyAsync(cancellationToken))
            return;

        var grupos = await db.Grupos.ToDictionaryAsync(g => g.Nome, g => g.Id, cancellationToken);
        var now = DateTime.UtcNow;

        var seedUsers = new (string Nome, string Login, string Senha, string Grupo)[]
        {
            ("Administrador", "admin@rec4.local", "Rec4!Admin123", GruposPadrao.Controladoria),
            ("Usuário Financeiro", "financeiro@rec4.local", "Rec4!Financeiro123", GruposPadrao.Financeiro),
            ("Usuário Logística", "logistica@rec4.local", "Rec4!Logistica123", GruposPadrao.Logistica)
        };

        foreach (var seed in seedUsers)
        {
            db.Usuarios.Add(new Usuario
            {
                Id = Guid.NewGuid(),
                Nome = seed.Nome,
                Login = seed.Login,
                SenhaHash = passwordHasher.Hash(seed.Senha),
                GrupoId = grupos[seed.Grupo],
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
