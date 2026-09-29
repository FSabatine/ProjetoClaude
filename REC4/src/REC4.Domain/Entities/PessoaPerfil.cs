namespace REC4.Domain.Entities;

public class PessoaPerfil
{
    public Guid PessoaId { get; set; }
    public Pessoa Pessoa { get; set; } = null!;

    public int PerfilId { get; set; }
    public Perfil Perfil { get; set; } = null!;
}
