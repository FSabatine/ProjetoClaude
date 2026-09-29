export type TipoPessoa = 0 | 1 // 0 = Física, 1 = Jurídica

export interface PerfilDto {
  id: number
  nome: string
}

export interface EnderecoDto {
  id: string
  logradouro: string
  numero: string
  complemento: string | null
  bairro: string
  cidade: string
  uf: string
  cep: string
  pais: string
  isPrincipal: boolean
}

export interface EnderecoCreateDto {
  logradouro: string
  numero: string
  complemento: string | null
  bairro: string
  cidade: string
  uf: string
  cep: string
  pais: string
  isPrincipal: boolean
}

export interface ContaBancariaDto {
  id: string
  banco: string
  agencia: string
  conta: string
  digitoConta: string | null
  tipoConta: string
  isPrincipal: boolean
}

export interface ContaBancariaCreateDto {
  banco: string
  agencia: string
  conta: string
  digitoConta: string | null
  tipoConta: string
  isPrincipal: boolean
}

export interface PessoaDto {
  id: string
  tipoPessoa: TipoPessoa
  cpfCnpj: string
  nome: string
  dataNascimento: string | null
  rg: string | null
  orgaoEmissorRG: string | null
  ufrg: string | null
  dataEmissaoRG: string | null
  nomeMae: string | null
  nomePai: string | null
  whatsApp: string | null
  perfis: PerfilDto[]
  enderecos: EnderecoDto[]
  contasBancarias: ContaBancariaDto[]
}

export interface PessoaCreateDto {
  tipoPessoa: TipoPessoa
  cpfCnpj: string
  nome: string
  dataNascimento: string | null
  rg: string | null
  orgaoEmissorRG: string | null
  ufrg: string | null
  dataEmissaoRG: string | null
  nomeMae: string | null
  nomePai: string | null
  whatsApp: string | null
  perfilIds: number[]
}

export interface PessoaUpdateDto {
  nome: string
  dataNascimento: string | null
  rg: string | null
  orgaoEmissorRG: string | null
  ufrg: string | null
  dataEmissaoRG: string | null
  nomeMae: string | null
  nomePai: string | null
  whatsApp: string | null
}
