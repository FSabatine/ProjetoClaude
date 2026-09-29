using System.Net.Http.Json;
using System.Text.Json;
using REC4.Application.Pessoas.Dtos;

namespace REC4.Web.Services;

public class PessoaApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public PessoaApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<PessoaListResponse> ListAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var response = await _http.GetAsync($"api/pessoas?page={page}&pageSize={pageSize}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<PessoaListResponse>(JsonOptions, cancellationToken))!;
    }

    public async Task<PessoaDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _http.GetAsync($"api/pessoas/{id}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<PessoaDto>(JsonOptions, cancellationToken))!;
    }

    public async Task<PessoaDto> CreateAsync(PessoaCreateDto dto, CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync("api/pessoas", dto, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<PessoaDto>(JsonOptions, cancellationToken))!;
    }

    public async Task<PessoaDto> UpdateAsync(Guid id, PessoaUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var response = await _http.PutAsJsonAsync($"api/pessoas/{id}", dto, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<PessoaDto>(JsonOptions, cancellationToken))!;
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _http.DeleteAsync($"api/pessoas/{id}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<PessoaDto> SetPerfisAsync(Guid id, IReadOnlyList<int> perfilIds, CancellationToken cancellationToken = default)
    {
        var response = await _http.PutAsJsonAsync($"api/pessoas/{id}/perfis", perfilIds, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<PessoaDto>(JsonOptions, cancellationToken))!;
    }

    public async Task<IReadOnlyList<PerfilDto>> ListPerfisAsync(CancellationToken cancellationToken = default)
    {
        var response = await _http.GetAsync("api/perfis", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<PerfilDto>>(JsonOptions, cancellationToken))!;
    }

    public async Task<EnderecoDto> AddEnderecoAsync(Guid pessoaId, EnderecoCreateDto dto, CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync($"api/pessoas/{pessoaId}/enderecos", dto, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<EnderecoDto>(JsonOptions, cancellationToken))!;
    }

    public async Task SetEnderecoPrincipalAsync(Guid pessoaId, Guid enderecoId, CancellationToken cancellationToken = default)
    {
        var response = await _http.PutAsync($"api/pessoas/{pessoaId}/enderecos/{enderecoId}/principal", null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<ContaBancariaDto> AddContaBancariaAsync(Guid pessoaId, ContaBancariaCreateDto dto, CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync($"api/pessoas/{pessoaId}/contas-bancarias", dto, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<ContaBancariaDto>(JsonOptions, cancellationToken))!;
    }

    public async Task SetContaPrincipalAsync(Guid pessoaId, Guid contaId, CancellationToken cancellationToken = default)
    {
        var response = await _http.PutAsync($"api/pessoas/{pessoaId}/contas-bancarias/{contaId}/principal", null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        throw new ApiException(await ExtractMessageAsync(response, cancellationToken));
    }

    private static async Task<string> ExtractMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiProblemResponse>(JsonOptions, cancellationToken);
            if (problem?.Errors is { Count: > 0 })
                return string.Join(" ", problem.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));

            if (!string.IsNullOrWhiteSpace(problem?.Title))
                return problem.Title;
        }
        catch (JsonException)
        {
            // corpo nao era problem+json (ex.: erro de infraestrutura); cai no fallback abaixo.
        }

        return $"A API respondeu com o status {(int)response.StatusCode} ({response.StatusCode}).";
    }

    private record ApiProblemResponse(string? Title, List<ApiFieldError>? Errors);

    private record ApiFieldError(string PropertyName, string ErrorMessage);
}
