namespace REC4.Domain.Entities;

public class Perfil
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;

    public ICollection<PessoaPerfil> PessoaPerfis { get; set; } = new List<PessoaPerfil>();
}
