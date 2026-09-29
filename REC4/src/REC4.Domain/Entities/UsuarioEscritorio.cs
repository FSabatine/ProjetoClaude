namespace REC4.Domain.Entities;

public class UsuarioEscritorio
{
    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public Guid EscritorioId { get; set; }
    public Escritorio Escritorio { get; set; } = null!;

    public bool IsPrincipal { get; set; }
}
