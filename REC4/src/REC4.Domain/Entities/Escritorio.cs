namespace REC4.Domain.Entities;

public class Escritorio
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<UsuarioEscritorio> UsuarioEscritorios { get; set; } = new List<UsuarioEscritorio>();
}
