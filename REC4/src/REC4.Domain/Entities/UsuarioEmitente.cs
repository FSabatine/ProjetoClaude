namespace REC4.Domain.Entities;

public class UsuarioEmitente
{
    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public Guid PessoaId { get; set; }
    public Pessoa Pessoa { get; set; } = null!;
}
