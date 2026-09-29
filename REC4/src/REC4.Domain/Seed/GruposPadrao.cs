namespace REC4.Domain.Seed;

public static class GruposPadrao
{
    public const string Controladoria = "Controladoria";
    public const string Financeiro = "Financeiro";
    public const string Logistica = "Logística";

    public static readonly IReadOnlyList<string> Nomes = new[] { Controladoria, Financeiro, Logistica };

    // TODO — REGRA DE NEGÓCIO A CONFIRMAR: "acesso amplo" da Controladoria foi interpretado aqui como
    // todas as permissões do catálogo atual (incluindo Usuario.*). Confirmar com o cliente se é isso mesmo.
    public static readonly IReadOnlyDictionary<string, string[]> PermissoesPorGrupo = new Dictionary<string, string[]>
    {
        [Controladoria] = PermissoesPadrao.Catalogo.Select(p => p.Chave).ToArray(),
        [Financeiro] = new[]
        {
            "Financeiro.Visualizar", "Financeiro.Criar", "Financeiro.Editar",
            "Conciliacao.Visualizar", "Conciliacao.Editar",
            "Pessoa.Visualizar"
        },
        [Logistica] = new[]
        {
            "Documento.Emitir", "Documento.Visualizar",
            "Pessoa.Visualizar"
        }
    };
}
