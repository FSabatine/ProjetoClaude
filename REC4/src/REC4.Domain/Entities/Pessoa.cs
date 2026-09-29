using REC4.Domain.Enums;

namespace REC4.Domain.Entities;

public class Pessoa
{
    public Guid Id { get; set; }

    public TipoPessoa TipoPessoa { get; set; }
    public string CpfCnpj { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime? DeletedAt { get; set; }

    public DateTime? DataNascimento { get; set; }
    public string? RG { get; set; }
    public string? OrgaoEmissorRG { get; set; }
    public string? UFRG { get; set; }
    public DateTime? DataEmissaoRG { get; set; }
    public string? NomeMae { get; set; }
    public string? NomePai { get; set; }
    public string? WhatsApp { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<PessoaPerfil> PessoaPerfis { get; set; } = new List<PessoaPerfil>();
    public ICollection<ContaBancaria> ContasBancarias { get; set; } = new List<ContaBancaria>();
    public ICollection<Endereco> Enderecos { get; set; } = new List<Endereco>();
}
