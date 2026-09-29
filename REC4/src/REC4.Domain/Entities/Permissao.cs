namespace REC4.Domain.Entities;

public class Permissao
{
    public int Id { get; set; }
    public string Chave { get; set; } = string.Empty;
    public string? Descricao { get; set; }

    public ICollection<GrupoPermissao> GrupoPermissoes { get; set; } = new List<GrupoPermissao>();
}
