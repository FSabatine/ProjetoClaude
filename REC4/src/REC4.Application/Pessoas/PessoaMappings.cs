using REC4.Application.Pessoas.Dtos;
using REC4.Domain.Entities;

namespace REC4.Application.Pessoas;

internal static class PessoaMappings
{
    public static PessoaDto ToDto(this Pessoa pessoa) => new(
        pessoa.Id,
        pessoa.TipoPessoa,
        pessoa.CpfCnpj,
        pessoa.Nome,
        pessoa.DataNascimento,
        pessoa.RG,
        pessoa.OrgaoEmissorRG,
        pessoa.UFRG,
        pessoa.DataEmissaoRG,
        pessoa.NomeMae,
        pessoa.NomePai,
        pessoa.WhatsApp,
        pessoa.PessoaPerfis.Select(pp => pp.Perfil.ToDto()).ToList(),
        pessoa.Enderecos.Select(e => e.ToDto()).ToList(),
        pessoa.ContasBancarias.Select(c => c.ToDto()).ToList()
    );

    public static PerfilDto ToDto(this Perfil perfil) => new(perfil.Id, perfil.Nome);

    public static EnderecoDto ToDto(this Endereco endereco) => new(
        endereco.Id,
        endereco.Logradouro,
        endereco.Numero,
        endereco.Complemento,
        endereco.Bairro,
        endereco.Cidade,
        endereco.UF,
        endereco.CEP,
        endereco.Pais,
        endereco.IsPrincipal
    );

    public static ContaBancariaDto ToDto(this ContaBancaria conta) => new(
        conta.Id,
        conta.Banco,
        conta.Agencia,
        conta.Conta,
        conta.DigitoConta,
        conta.TipoConta,
        conta.IsPrincipal
    );
}
