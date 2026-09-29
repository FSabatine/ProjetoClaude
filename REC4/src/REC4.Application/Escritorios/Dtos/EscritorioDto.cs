namespace REC4.Application.Escritorios.Dtos;

public record EscritorioDto(Guid Id, string Nome, bool IsActive);

public record EscritorioCreateDto(string Nome);

public record EscritorioUpdateDto(string Nome, bool IsActive);
