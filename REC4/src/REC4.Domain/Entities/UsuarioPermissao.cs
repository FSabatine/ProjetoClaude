namespace REC4.Domain.Entities;

// So tem significado quando Usuario.TemPermissaoPersonalizada = true; guarda o conjunto de
// permissoes que substitui (nao soma) as permissoes do Grupo do usuario.
public class UsuarioPermissao
{
    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public int PermissaoId { get; set; }
    public Permissao Permissao { get; set; } = null!;
}
