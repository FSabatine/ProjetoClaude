namespace REC4.Domain.Seed;

public static class PermissoesPadrao
{
    public static readonly IReadOnlyList<(string Chave, string Descricao)> Catalogo = new[]
    {
        ("Pessoa.Visualizar", "Visualizar pessoas cadastradas"),
        ("Pessoa.Criar", "Cadastrar novas pessoas"),
        ("Pessoa.Editar", "Editar ou inativar pessoas cadastradas"),
        ("Usuario.Visualizar", "Visualizar usuários do sistema"),
        ("Usuario.Criar", "Cadastrar novos usuários"),
        ("Usuario.Editar", "Editar usuários existentes"),
        ("Financeiro.Visualizar", "Visualizar lançamentos financeiros"),
        ("Financeiro.Criar", "Criar lançamentos financeiros"),
        ("Financeiro.Editar", "Editar lançamentos financeiros"),
        ("Conciliacao.Visualizar", "Visualizar conciliação financeira"),
        ("Conciliacao.Editar", "Editar conciliação financeira"),
        ("Documento.Emitir", "Emitir documentos de transporte"),
        ("Documento.Visualizar", "Visualizar documentos de transporte"),
        ("Escritorio.Visualizar", "Visualizar escritórios cadastrados"),
        ("Escritorio.Criar", "Cadastrar novos escritórios"),
        ("Escritorio.Editar", "Editar escritórios existentes")
    };
}
