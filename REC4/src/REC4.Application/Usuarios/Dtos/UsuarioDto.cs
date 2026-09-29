using REC4.Domain.Enums;

namespace REC4.Application.Usuarios.Dtos;

public record UsuarioResumoDto(Guid Id, string Nome, string Login, string GrupoNome, bool IsActive);

public record UsuarioEscritorioDto(Guid Id, string Nome, bool IsPrincipal);

public record UsuarioEmitenteDto(Guid PessoaId, string Nome);

public record UsuarioDto(
    Guid Id,
    string Nome,
    string Login,
    int GrupoId,
    string GrupoNome,
    bool IsActive,
    Guid? PessoaId,
    string? PessoaNome,
    TipoUsuario TipoUsuario,
    int? LimiteDiasEdicaoFinanceiro,
    bool TemPermissaoPersonalizada,
    IReadOnlyList<string> PermissoesEfetivas,
    IReadOnlyList<UsuarioEscritorioDto> Escritorios,
    IReadOnlyList<UsuarioEmitenteDto> Emitentes,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record UsuarioCreateDto(
    string Nome,
    string Login,
    string Senha,
    int GrupoId,
    Guid? PessoaId,
    TipoUsuario TipoUsuario,
    int? LimiteDiasEdicaoFinanceiro
);

public record UsuarioUpdateDto(
    string Nome,
    int GrupoId,
    bool IsActive,
    Guid? PessoaId,
    TipoUsuario TipoUsuario,
    int? LimiteDiasEdicaoFinanceiro
);

public record UsuarioPermissoesUpdateDto(bool Personalizado, IReadOnlyList<int> PermissaoIds);

public record UsuarioEscritorioCreateDto(Guid EscritorioId, bool IsPrincipal);

public record UsuarioEmitenteCreateDto(Guid PessoaId);

public record MeuUsuarioDto(Guid Id, string Nome, string Login, string Grupo, bool IsActive, IReadOnlyList<string> Permissoes);
