namespace REC4.Domain.Entities;

public class Endereco
{
    public Guid Id { get; set; }

    public Guid PessoaId { get; set; }
    public Pessoa Pessoa { get; set; } = null!;

    public string Logradouro { get; set; } = string.Empty;
    public string Numero { get; set; } = string.Empty;
    public string? Complemento { get; set; }
    public string Bairro { get; set; } = string.Empty;
    public string Cidade { get; set; } = string.Empty;
    public string UF { get; set; } = string.Empty;
    public string CEP { get; set; } = string.Empty;
    public string Pais { get; set; } = "Brasil";

    public bool IsPrincipal { get; set; }
}
