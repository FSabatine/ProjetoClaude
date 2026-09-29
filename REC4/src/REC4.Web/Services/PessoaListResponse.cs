using REC4.Application.Pessoas.Dtos;

namespace REC4.Web.Services;

public record PessoaListResponse(IReadOnlyList<PessoaDto> Items, int TotalCount, int Page, int PageSize);
