namespace REC4.Application.Pessoas.Dtos;

public record ContaBancariaDto(
    Guid Id,
    string Banco,
    string Agencia,
    string Conta,
    string? DigitoConta,
    string TipoConta,
    bool IsPrincipal
);

public record ContaBancariaCreateDto(
    string Banco,
    string Agencia,
    string Conta,
    string? DigitoConta,
    string TipoConta,
    bool IsPrincipal
);
