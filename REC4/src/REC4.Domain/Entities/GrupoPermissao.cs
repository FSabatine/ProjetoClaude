namespace REC4.Domain.Entities;

public class GrupoPermissao
{
    public int GrupoId { get; set; }
    public Grupo Grupo { get; set; } = null!;

    public int PermissaoId { get; set; }
    public Permissao Permissao { get; set; } = null!;
}
