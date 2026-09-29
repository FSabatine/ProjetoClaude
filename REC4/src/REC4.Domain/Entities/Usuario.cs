using REC4.Domain.Enums;

namespace REC4.Domain.Entities;

public class Usuario
{
    public Guid Id { get; set; }

    public string Nome { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string SenhaHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public int GrupoId { get; set; }
    public Grupo Grupo { get; set; } = null!;

    public Guid? PessoaId { get; set; }
    public Pessoa? Pessoa { get; set; }

    public TipoUsuario TipoUsuario { get; set; } = TipoUsuario.Operador;
    public int? LimiteDiasEdicaoFinanceiro { get; set; }

    public bool TemPermissaoPersonalizada { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<UsuarioEscritorio> UsuarioEscritorios { get; set; } = new List<UsuarioEscritorio>();
    public ICollection<UsuarioEmitente> UsuarioEmitentes { get; set; } = new List<UsuarioEmitente>();
    public ICollection<UsuarioPermissao> UsuarioPermissoes { get; set; } = new List<UsuarioPermissao>();
}
