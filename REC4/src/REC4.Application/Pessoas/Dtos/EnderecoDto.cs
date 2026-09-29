namespace REC4.Application.Pessoas.Dtos;

public record EnderecoDto(
    Guid Id,
    string Logradouro,
    string Numero,
    string? Complemento,
    string Bairro,
    string Cidade,
    string UF,
    string CEP,
    string Pais,
    bool IsPrincipal
);

public record EnderecoCreateDto(
    string Logradouro,
    string Numero,
    string? Complemento,
    string Bairro,
    string Cidade,
    string UF,
    string CEP,
    string Pais,
    bool IsPrincipal
);
