using Microsoft.EntityFrameworkCore;
using REC4.Application.Common;
using REC4.Application.Exceptions;
using REC4.Application.Pessoas.Dtos;
using REC4.Domain.Entities;

namespace REC4.Application.Pessoas;

public class PessoaService : IPessoaService
{
    private readonly IRec4DbContext _db;

    public PessoaService(IRec4DbContext db)
    {
        _db = db;
    }

    public async Task<PessoaDto> CreateAsync(PessoaCreateDto dto, CancellationToken cancellationToken = default)
    {
        var cpfCnpj = dto.CpfCnpj.Trim();

        var alreadyExists = await _db.Pessoas.AnyAsync(p => p.CpfCnpj == cpfCnpj, cancellationToken);
        if (alreadyExists)
            throw new DuplicateDocumentException(cpfCnpj);

        var now = DateTime.UtcNow;
        var pessoa = new Pessoa
        {
            Id = Guid.NewGuid(),
            TipoPessoa = dto.TipoPessoa,
            CpfCnpj = cpfCnpj,
            Nome = dto.Nome,
            DataNascimento = dto.DataNascimento,
            RG = dto.RG,
            OrgaoEmissorRG = dto.OrgaoEmissorRG,
            UFRG = dto.UFRG,
            DataEmissaoRG = dto.DataEmissaoRG,
            NomeMae = dto.NomeMae,
            NomePai = dto.NomePai,
            WhatsApp = dto.WhatsApp,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        if (dto.PerfilIds is { Count: > 0 })
        {
            foreach (var perfilId in dto.PerfilIds.Distinct())
                pessoa.PessoaPerfis.Add(new PessoaPerfil { PessoaId = pessoa.Id, PerfilId = perfilId });
        }

        _db.Pessoas.Add(pessoa);
        await _db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(pessoa.Id, cancellationToken);
    }

    public async Task<PessoaDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var pessoa = await LoadPessoaAsync(id, cancellationToken);
        return pessoa.ToDto();
    }

    public async Task<(IReadOnlyList<PessoaDto> Items, int TotalCount)> ListAsync(int page, int pageSize, int? perfilId = null, CancellationToken cancellationToken = default)
    {
        var query = _db.Pessoas
            .Include(p => p.PessoaPerfis).ThenInclude(pp => pp.Perfil)
            .Include(p => p.Enderecos)
            .Include(p => p.ContasBancarias)
            .Where(p => perfilId == null || p.PessoaPerfis.Any(pp => pp.PerfilId == perfilId))
            .OrderBy(p => p.Nome);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items.Select(p => p.ToDto()).ToList(), totalCount);
    }

    public async Task<PessoaDto> UpdateAsync(Guid id, PessoaUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var pessoa = await LoadPessoaAsync(id, cancellationToken);

        pessoa.Nome = dto.Nome;
        pessoa.DataNascimento = dto.DataNascimento;
        pessoa.RG = dto.RG;
        pessoa.OrgaoEmissorRG = dto.OrgaoEmissorRG;
        pessoa.UFRG = dto.UFRG;
        pessoa.DataEmissaoRG = dto.DataEmissaoRG;
        pessoa.NomeMae = dto.NomeMae;
        pessoa.NomePai = dto.NomePai;
        pessoa.WhatsApp = dto.WhatsApp;
        pessoa.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return pessoa.ToDto();
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var pessoa = await LoadPessoaAsync(id, cancellationToken);

        pessoa.IsActive = false;
        pessoa.DeletedAt = DateTime.UtcNow;
        pessoa.UpdatedAt = pessoa.DeletedAt.Value;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PessoaDto> SetPerfisAsync(Guid pessoaId, IReadOnlyList<int> perfilIds, CancellationToken cancellationToken = default)
    {
        var pessoa = await LoadPessoaAsync(pessoaId, cancellationToken);
        var desired = perfilIds.Distinct().ToHashSet();

        var toRemove = pessoa.PessoaPerfis.Where(pp => !desired.Contains(pp.PerfilId)).ToList();
        foreach (var pp in toRemove)
            pessoa.PessoaPerfis.Remove(pp);

        var current = pessoa.PessoaPerfis.Select(pp => pp.PerfilId).ToHashSet();
        foreach (var perfilId in desired.Where(id => !current.Contains(id)))
            pessoa.PessoaPerfis.Add(new PessoaPerfil { PessoaId = pessoa.Id, PerfilId = perfilId });

        await _db.SaveChangesAsync(cancellationToken);
        return pessoa.ToDto();
    }

    public async Task<IReadOnlyList<PerfilDto>> ListPerfisAsync(CancellationToken cancellationToken = default)
    {
        var perfis = await _db.Perfis.OrderBy(p => p.Nome).ToListAsync(cancellationToken);
        return perfis.Select(p => p.ToDto()).ToList();
    }

    public async Task<EnderecoDto> AddEnderecoAsync(Guid pessoaId, EnderecoCreateDto dto, CancellationToken cancellationToken = default)
    {
        var pessoa = await LoadPessoaAsync(pessoaId, cancellationToken);
        var tornaPrincipal = dto.IsPrincipal || pessoa.Enderecos.Count == 0;
        var existentesPrincipais = pessoa.Enderecos.Where(e => e.IsPrincipal).ToList();

        var endereco = new Endereco
        {
            Id = Guid.NewGuid(),
            PessoaId = pessoa.Id,
            Logradouro = dto.Logradouro,
            Numero = dto.Numero,
            Complemento = dto.Complemento,
            Bairro = dto.Bairro,
            Cidade = dto.Cidade,
            UF = dto.UF,
            CEP = dto.CEP,
            Pais = dto.Pais,
            IsPrincipal = tornaPrincipal
        };

        await SavePrincipalChangeAsync(
            needsSwap: tornaPrincipal && existentesPrincipais.Count > 0,
            clearOld: () => existentesPrincipais.ForEach(e => e.IsPrincipal = false),
            applyNew: () => pessoa.Enderecos.Add(endereco),
            cancellationToken);

        return endereco.ToDto();
    }

    public async Task SetEnderecoPrincipalAsync(Guid pessoaId, Guid enderecoId, CancellationToken cancellationToken = default)
    {
        var pessoa = await LoadPessoaAsync(pessoaId, cancellationToken);
        var alvo = pessoa.Enderecos.FirstOrDefault(e => e.Id == enderecoId)
            ?? throw new NotFoundException(nameof(Endereco), enderecoId);
        var outrasPrincipais = pessoa.Enderecos.Where(e => e.Id != alvo.Id && e.IsPrincipal).ToList();

        await SavePrincipalChangeAsync(
            needsSwap: outrasPrincipais.Count > 0,
            clearOld: () => outrasPrincipais.ForEach(e => e.IsPrincipal = false),
            applyNew: () => alvo.IsPrincipal = true,
            cancellationToken);
    }

    public async Task<ContaBancariaDto> AddContaBancariaAsync(Guid pessoaId, ContaBancariaCreateDto dto, CancellationToken cancellationToken = default)
    {
        var pessoa = await LoadPessoaAsync(pessoaId, cancellationToken);
        var tornaPrincipal = dto.IsPrincipal || pessoa.ContasBancarias.Count == 0;
        var existentesPrincipais = pessoa.ContasBancarias.Where(c => c.IsPrincipal).ToList();

        var conta = new ContaBancaria
        {
            Id = Guid.NewGuid(),
            PessoaId = pessoa.Id,
            Banco = dto.Banco,
            Agencia = dto.Agencia,
            Conta = dto.Conta,
            DigitoConta = dto.DigitoConta,
            TipoConta = dto.TipoConta,
            IsPrincipal = tornaPrincipal
        };

        await SavePrincipalChangeAsync(
            needsSwap: tornaPrincipal && existentesPrincipais.Count > 0,
            clearOld: () => existentesPrincipais.ForEach(c => c.IsPrincipal = false),
            applyNew: () => pessoa.ContasBancarias.Add(conta),
            cancellationToken);

        return conta.ToDto();
    }

    public async Task SetContaPrincipalAsync(Guid pessoaId, Guid contaId, CancellationToken cancellationToken = default)
    {
        var pessoa = await LoadPessoaAsync(pessoaId, cancellationToken);
        var alvo = pessoa.ContasBancarias.FirstOrDefault(c => c.Id == contaId)
            ?? throw new NotFoundException(nameof(ContaBancaria), contaId);
        var outrasPrincipais = pessoa.ContasBancarias.Where(c => c.Id != alvo.Id && c.IsPrincipal).ToList();

        await SavePrincipalChangeAsync(
            needsSwap: outrasPrincipais.Count > 0,
            clearOld: () => outrasPrincipais.ForEach(c => c.IsPrincipal = false),
            applyNew: () => alvo.IsPrincipal = true,
            cancellationToken);
    }

    private Task SavePrincipalChangeAsync(bool needsSwap, Action clearOld, Action applyNew, CancellationToken cancellationToken) =>
        PrincipalSwapHelper.SaveAsync(_db, needsSwap, clearOld, applyNew, cancellationToken);

    private async Task<Pessoa> LoadPessoaAsync(Guid id, CancellationToken cancellationToken)
    {
        var pessoa = await _db.Pessoas
            .Include(p => p.PessoaPerfis).ThenInclude(pp => pp.Perfil)
            .Include(p => p.Enderecos)
            .Include(p => p.ContasBancarias)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return pessoa ?? throw new NotFoundException(nameof(Pessoa), id);
    }
}
