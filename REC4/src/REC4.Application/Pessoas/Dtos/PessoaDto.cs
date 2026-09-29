using REC4.Domain.Enums;

namespace REC4.Application.Pessoas.Dtos;

public record PessoaDto(
    Guid Id,
    TipoPessoa TipoPessoa,
    string CpfCnpj,
    string Nome,
    DateTime? DataNascimento,
    string? RG,
    string? OrgaoEmissorRG,
    string? UFRG,
    DateTime? DataEmissaoRG,
    string? NomeMae,
    string? NomePai,
    string? WhatsApp,
    IReadOnlyList<PerfilDto> Perfis,
    IReadOnlyList<EnderecoDto> Enderecos,
    IReadOnlyList<ContaBancariaDto> ContasBancarias
);

public record PessoaCreateDto(
    TipoPessoa TipoPessoa,
    string CpfCnpj,
    string Nome,
    DateTime? DataNascimento,
    string? RG,
    string? OrgaoEmissorRG,
    string? UFRG,
    DateTime? DataEmissaoRG,
    string? NomeMae,
    string? NomePai,
    string? WhatsApp,
    IReadOnlyList<int>? PerfilIds
);

public record PessoaUpdateDto(
    string Nome,
    DateTime? DataNascimento,
    string? RG,
    string? OrgaoEmissorRG,
    string? UFRG,
    DateTime? DataEmissaoRG,
    string? NomeMae,
    string? NomePai,
    string? WhatsApp
);
