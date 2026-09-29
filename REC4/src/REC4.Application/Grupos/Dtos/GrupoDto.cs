using REC4.Application.Permissoes.Dtos;

namespace REC4.Application.Grupos.Dtos;

public record GrupoDto(int Id, string Nome);

public record GrupoDetailDto(int Id, string Nome, IReadOnlyList<PermissaoDto> Permissoes);

public record GrupoCreateDto(string Nome, IReadOnlyList<int> PermissaoIds);

public record GrupoUpdateDto(string Nome, IReadOnlyList<int> PermissaoIds);
