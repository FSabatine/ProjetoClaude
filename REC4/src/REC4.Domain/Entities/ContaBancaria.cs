namespace REC4.Domain.Entities;

public class ContaBancaria
{
    public Guid Id { get; set; }

    public Guid PessoaId { get; set; }
    public Pessoa Pessoa { get; set; } = null!;

    public string Banco { get; set; } = string.Empty;
    public string Agencia { get; set; } = string.Empty;
    public string Conta { get; set; } = string.Empty;
    public string? DigitoConta { get; set; }
    public string TipoConta { get; set; } = string.Empty;

    public bool IsPrincipal { get; set; }
}
