namespace REC4.Application.Common;

// Indice unico filtrado (IsPrincipal=1) e checado por instrucao, nao no fim da transacao: precisa de
// dois SaveChanges (desmarcar, depois marcar) para nunca haver dois "principal" ao mesmo tempo.
// Compartilhado entre PessoaService (ContaBancaria/Endereco) e UsuarioService (UsuarioEscritorio).
public static class PrincipalSwapHelper
{
    public static async Task SaveAsync(
        IRec4DbContext db,
        bool needsSwap,
        Action clearOld,
        Action applyNew,
        CancellationToken cancellationToken)
    {
        if (!needsSwap)
        {
            applyNew();
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        clearOld();
        await db.SaveChangesAsync(cancellationToken);

        applyNew();
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
