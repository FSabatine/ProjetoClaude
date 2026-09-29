namespace REC4.Domain.Entities;

public class Grupo
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;

    public ICollection<GrupoPermissao> GrupoPermissoes { get; set; } = new List<GrupoPermissao>();
    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}
