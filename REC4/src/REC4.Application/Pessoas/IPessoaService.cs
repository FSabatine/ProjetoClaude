using REC4.Application.Pessoas.Dtos;

namespace REC4.Application.Pessoas;

public interface IPessoaService
{
    Task<PessoaDto> CreateAsync(PessoaCreateDto dto, CancellationToken cancellationToken = default);
    Task<PessoaDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<PessoaDto> Items, int TotalCount)> ListAsync(int page, int pageSize, int? perfilId = null, CancellationToken cancellationToken = default);
    Task<PessoaDto> UpdateAsync(Guid id, PessoaUpdateDto dto, CancellationToken cancellationToken = default);
    Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PessoaDto> SetPerfisAsync(Guid pessoaId, IReadOnlyList<int> perfilIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PerfilDto>> ListPerfisAsync(CancellationToken cancellationToken = default);

    Task<EnderecoDto> AddEnderecoAsync(Guid pessoaId, EnderecoCreateDto dto, CancellationToken cancellationToken = default);
    Task SetEnderecoPrincipalAsync(Guid pessoaId, Guid enderecoId, CancellationToken cancellationToken = default);

    Task<ContaBancariaDto> AddContaBancariaAsync(Guid pessoaId, ContaBancariaCreateDto dto, CancellationToken cancellationToken = default);
    Task SetContaPrincipalAsync(Guid pessoaId, Guid contaId, CancellationToken cancellationToken = default);
}
