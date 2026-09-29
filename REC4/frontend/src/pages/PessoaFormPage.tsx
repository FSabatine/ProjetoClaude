import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import {
  addContaBancaria,
  addEndereco,
  createPessoa,
  getPessoa,
  listPerfis,
  setContaPrincipal,
  setEnderecoPrincipal,
  setPerfis,
  updatePessoa,
} from '../api/pessoas'
import { extractErrorMessage } from '../api/errors'
import { useBreadcrumb } from '../layout/useBreadcrumb'
import type {
  ContaBancariaCreateDto,
  ContaBancariaDto,
  EnderecoCreateDto,
  EnderecoDto,
  PerfilDto,
  PessoaCreateDto,
  PessoaDto,
  PessoaUpdateDto,
  TipoPessoa,
} from '../types/pessoas'

type StepKey = 'pessoais' | 'bancarias' | 'enderecos'

const EMPTY_ENDERECO_FORM: EnderecoCreateDto = {
  logradouro: '',
  numero: '',
  complemento: '',
  bairro: '',
  cidade: '',
  uf: '',
  cep: '',
  pais: 'Brasil',
  isPrincipal: false,
}

const EMPTY_CONTA_FORM: ContaBancariaCreateDto = {
  banco: '',
  agencia: '',
  conta: '',
  digitoConta: '',
  tipoConta: '',
  isPrincipal: false,
}

function toDateInputValue(value: string): string {
  return value ? value.slice(0, 10) : ''
}

export default function PessoaFormPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()

  useBreadcrumb([
    { label: 'Pessoas', to: '/pessoas' },
    { label: id ? 'Editar pessoa' : 'Cadastro Pessoa' },
  ])

  const [pessoa, setPessoa] = useState<PessoaDto | null>(null)
  const [loading, setLoading] = useState<boolean>(Boolean(id))
  const [loadError, setLoadError] = useState<string | null>(null)

  const [tipoPessoa, setTipoPessoa] = useState<TipoPessoa>(0)
  const [cpfCnpj, setCpfCnpj] = useState('')
  const [nome, setNome] = useState('')
  const [dataNascimento, setDataNascimento] = useState('')
  const [rg, setRg] = useState('')
  const [orgaoEmissorRG, setOrgaoEmissorRG] = useState('')
  const [ufrg, setUfrg] = useState('')
  const [dataEmissaoRG, setDataEmissaoRG] = useState('')
  const [nomeMae, setNomeMae] = useState('')
  const [nomePai, setNomePai] = useState('')
  const [whatsApp, setWhatsApp] = useState('')

  const [availablePerfis, setAvailablePerfis] = useState<PerfilDto[]>([])
  const [selectedPerfis, setSelectedPerfis] = useState<PerfilDto[]>([])
  const [perfilError, setPerfilError] = useState<string | null>(null)

  const [enderecos, setEnderecos] = useState<EnderecoDto[]>([])
  const [enderecoForm, setEnderecoForm] = useState<EnderecoCreateDto>(EMPTY_ENDERECO_FORM)
  const [enderecoSaving, setEnderecoSaving] = useState(false)
  const [enderecoError, setEnderecoError] = useState<string | null>(null)

  const [contas, setContas] = useState<ContaBancariaDto[]>([])
  const [contaForm, setContaForm] = useState<ContaBancariaCreateDto>(EMPTY_CONTA_FORM)
  const [contaSaving, setContaSaving] = useState(false)
  const [contaError, setContaError] = useState<string | null>(null)

  const [saveError, setSaveError] = useState<string | null>(null)
  const [saveSuccess, setSaveSuccess] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const [activeStep, setActiveStep] = useState<StepKey>('pessoais')

  const pessoaId = pessoa?.id ?? id ?? null
  const hasPessoa = Boolean(pessoaId)
  const selectedPerfisIds = selectedPerfis.map((perfil) => perfil.id)

  useEffect(() => {
    listPerfis()
      .then(setAvailablePerfis)
      .catch((err) => setPerfilError(extractErrorMessage(err, 'Não foi possível carregar os perfis disponíveis.')))
  }, [])

  useEffect(() => {
    if (!id) {
      setLoading(false)
      return
    }
    let cancelled = false
    setLoading(true)
    getPessoa(id)
      .then((data) => {
        if (cancelled) return
        applyPessoa(data)
      })
      .catch((err) => {
        if (!cancelled) setLoadError(extractErrorMessage(err, 'Não foi possível carregar a pessoa.'))
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [id])

  function applyPessoa(data: PessoaDto) {
    setPessoa(data)
    setTipoPessoa(data.tipoPessoa)
    setCpfCnpj(data.cpfCnpj)
    setNome(data.nome)
    setDataNascimento(data.dataNascimento ?? '')
    setRg(data.rg ?? '')
    setOrgaoEmissorRG(data.orgaoEmissorRG ?? '')
    setUfrg(data.ufrg ?? '')
    setDataEmissaoRG(data.dataEmissaoRG ?? '')
    setNomeMae(data.nomeMae ?? '')
    setNomePai(data.nomePai ?? '')
    setWhatsApp(data.whatsApp ?? '')
    setSelectedPerfis(data.perfis)
    setEnderecos(data.enderecos)
    setContas(data.contasBancarias)
  }

  async function handleAddPerfil(perfilId: number) {
    if (selectedPerfisIds.includes(perfilId)) return
    const nextIds = [...selectedPerfisIds, perfilId]
    setPerfilError(null)
    if (pessoaId) {
      try {
        const updated = await setPerfis(pessoaId, nextIds)
        setPessoa(updated)
        setSelectedPerfis(updated.perfis)
      } catch (err) {
        setPerfilError(extractErrorMessage(err, 'Não foi possível atualizar os perfis.'))
      }
    } else {
      const perfil = availablePerfis.find((item) => item.id === perfilId)
      if (perfil) setSelectedPerfis((prev) => [...prev, perfil])
    }
  }

  async function handleRemovePerfil(perfilId: number) {
    const nextIds = selectedPerfisIds.filter((existingId) => existingId !== perfilId)
    setPerfilError(null)
    if (pessoaId) {
      try {
        const updated = await setPerfis(pessoaId, nextIds)
        setPessoa(updated)
        setSelectedPerfis(updated.perfis)
      } catch (err) {
        setPerfilError(extractErrorMessage(err, 'Não foi possível atualizar os perfis.'))
      }
    } else {
      setSelectedPerfis((prev) => prev.filter((perfil) => perfil.id !== perfilId))
    }
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSaveError(null)
    setSaveSuccess(null)
    setSaving(true)
    try {
      if (pessoaId) {
        const dto: PessoaUpdateDto = {
          nome,
          dataNascimento: dataNascimento || null,
          rg: rg || null,
          orgaoEmissorRG: orgaoEmissorRG || null,
          ufrg: ufrg || null,
          dataEmissaoRG: dataEmissaoRG || null,
          nomeMae: nomeMae || null,
          nomePai: nomePai || null,
          whatsApp: whatsApp || null,
        }
        const updated = await updatePessoa(pessoaId, dto)
        applyPessoa(updated)
        setSaveSuccess('Dados salvos com sucesso.')
      } else {
        const dto: PessoaCreateDto = {
          tipoPessoa,
          cpfCnpj,
          nome,
          dataNascimento: dataNascimento || null,
          rg: rg || null,
          orgaoEmissorRG: orgaoEmissorRG || null,
          ufrg: ufrg || null,
          dataEmissaoRG: dataEmissaoRG || null,
          nomeMae: nomeMae || null,
          nomePai: nomePai || null,
          whatsApp: whatsApp || null,
          perfilIds: selectedPerfisIds,
        }
        const created = await createPessoa(dto)
        // Endereços/contas bancárias precisam de um id real - navega para a edição.
        navigate(`/pessoas/${created.id}/editar`, { replace: true })
      }
    } catch (err) {
      setSaveError(extractErrorMessage(err, 'Não foi possível salvar a pessoa.'))
    } finally {
      setSaving(false)
    }
  }

  async function handleAddEndereco() {
    if (!pessoaId) return
    setEnderecoError(null)
    setEnderecoSaving(true)
    try {
      const created = await addEndereco(pessoaId, {
        ...enderecoForm,
        complemento: enderecoForm.complemento || null,
      })
      setEnderecos((prev) => (created.isPrincipal ? [...prev.map((e) => ({ ...e, isPrincipal: false })), created] : [...prev, created]))
      setEnderecoForm(EMPTY_ENDERECO_FORM)
    } catch (err) {
      setEnderecoError(extractErrorMessage(err, 'Não foi possível adicionar o endereço.'))
    } finally {
      setEnderecoSaving(false)
    }
  }

  async function handleSetEnderecoPrincipal(enderecoId: string) {
    if (!pessoaId) return
    setEnderecoError(null)
    try {
      await setEnderecoPrincipal(pessoaId, enderecoId)
      setEnderecos((prev) => prev.map((endereco) => ({ ...endereco, isPrincipal: endereco.id === enderecoId })))
    } catch (err) {
      setEnderecoError(extractErrorMessage(err, 'Não foi possível definir o endereço principal.'))
    }
  }

  async function handleAddConta() {
    if (!pessoaId) return
    setContaError(null)
    setContaSaving(true)
    try {
      const created = await addContaBancaria(pessoaId, {
        ...contaForm,
        digitoConta: contaForm.digitoConta || null,
      })
      setContas((prev) => (created.isPrincipal ? [...prev.map((c) => ({ ...c, isPrincipal: false })), created] : [...prev, created]))
      setContaForm(EMPTY_CONTA_FORM)
    } catch (err) {
      setContaError(extractErrorMessage(err, 'Não foi possível adicionar a conta bancária.'))
    } finally {
      setContaSaving(false)
    }
  }

  async function handleSetContaPrincipal(contaId: string) {
    if (!pessoaId) return
    setContaError(null)
    try {
      await setContaPrincipal(pessoaId, contaId)
      setContas((prev) => prev.map((conta) => ({ ...conta, isPrincipal: conta.id === contaId })))
    } catch (err) {
      setContaError(extractErrorMessage(err, 'Não foi possível definir a conta principal.'))
    }
  }

  if (loading) {
    return (
      <div className="card">
        <p className="text-muted">Carregando...</p>
      </div>
    )
  }

  if (loadError) {
    return (
      <div className="card">
        <div className="error-banner">{loadError}</div>
        <Link to="/pessoas" className="btn btn-outline">
          Voltar para a lista
        </Link>
      </div>
    )
  }

  return (
    <div className="pessoa-layout">
      <div className="card steps-card">
        <h3 className="card-title">Etapas do cadastro</h3>
        <div className="steps-list">
          <button
            type="button"
            className={`step-item${activeStep === 'pessoais' ? ' step-item-active' : ''}`}
            onClick={() => setActiveStep('pessoais')}
          >
            Informações pessoais
          </button>
          <button
            type="button"
            className={`step-item${activeStep === 'bancarias' ? ' step-item-active' : ''}`}
            onClick={() => setActiveStep('bancarias')}
          >
            Informações bancárias
          </button>
          <button
            type="button"
            className={`step-item${activeStep === 'enderecos' ? ' step-item-active' : ''}`}
            onClick={() => setActiveStep('enderecos')}
          >
            Endereços
          </button>
          <button type="button" className="step-item step-item-disabled" disabled title="Fora do escopo desta etapa">
            Financeiro
          </button>
          <button type="button" className="step-item step-item-disabled" disabled title="Fora do escopo desta etapa">
            Configurações Gerais
          </button>
        </div>
      </div>

      <div className="card form-card">
        {saveError && <div className="error-banner">{saveError}</div>}
        {saveSuccess && <div className="success-banner">{saveSuccess}</div>}
        {perfilError && <div className="error-banner">{perfilError}</div>}

        <form onSubmit={handleSubmit}>
          <div className="section" style={{ display: activeStep === 'pessoais' ? 'block' : 'none' }}>
            <div className="section-header">
              <h3>Tipo de Perfil</h3>
              <button type="button" className="btn btn-outline btn-sm" disabled title="Não implementado nesta etapa">
                Sincronizar REC1
              </button>
            </div>

            <div className="field">
              <span className="field-label">Tipo Perfil</span>
              <div className="pill-box">
                {selectedPerfis.map((perfil) => (
                  <span key={perfil.id} className="pill">
                    {perfil.nome}
                    <button
                      type="button"
                      className="pill-remove"
                      aria-label={`Remover ${perfil.nome}`}
                      onClick={() => handleRemovePerfil(perfil.id)}
                    >
                      ×
                    </button>
                  </span>
                ))}
                <select
                  className="pill-add-select"
                  value=""
                  onChange={(event) => {
                    const value = Number(event.target.value)
                    if (value) void handleAddPerfil(value)
                  }}
                >
                  <option value="">+ adicionar perfil...</option>
                  {availablePerfis
                    .filter((perfil) => !selectedPerfisIds.includes(perfil.id))
                    .map((perfil) => (
                      <option key={perfil.id} value={perfil.id}>
                        {perfil.nome}
                      </option>
                    ))}
                </select>
              </div>
            </div>

            <div className="field-row">
              <label className="field">
                <span className="field-label">Tipo Contribuinte</span>
                <select disabled defaultValue="" title="Campo não mapeado no backend nesta etapa">
                  <option value="">Não informado</option>
                </select>
              </label>
              <label className="field">
                <span className="field-label">Tipo de Pessoa</span>
                <select
                  value={tipoPessoa}
                  onChange={(event) => setTipoPessoa(Number(event.target.value) as TipoPessoa)}
                  disabled={hasPessoa}
                >
                  <option value={0}>Física</option>
                  <option value={1}>Jurídica</option>
                </select>
              </label>
              <label className="field">
                <span className="field-label">Documento</span>
                <input value={cpfCnpj} onChange={(event) => setCpfCnpj(event.target.value)} disabled={hasPessoa} required />
              </label>
            </div>

            <h3 className="mt-16">Dados Pessoais</h3>
            <label className="field">
              <span className="field-label">Nome Completo</span>
              <input value={nome} onChange={(event) => setNome(event.target.value)} required />
            </label>
            <div className="field-row">
              <label className="field">
                <span className="field-label">Data Nascimento</span>
                <input
                  type="date"
                  value={toDateInputValue(dataNascimento)}
                  onChange={(event) => setDataNascimento(event.target.value)}
                />
              </label>
              <label className="field">
                <span className="field-label">RG</span>
                <input value={rg} onChange={(event) => setRg(event.target.value)} />
              </label>
              <label className="field">
                <span className="field-label">Órgão Emissor RG</span>
                <input value={orgaoEmissorRG} onChange={(event) => setOrgaoEmissorRG(event.target.value)} />
              </label>
              <label className="field">
                <span className="field-label">UF RG</span>
                <input value={ufrg} maxLength={2} onChange={(event) => setUfrg(event.target.value)} />
              </label>
              <label className="field">
                <span className="field-label">Data emissão RG</span>
                <input
                  type="date"
                  value={toDateInputValue(dataEmissaoRG)}
                  onChange={(event) => setDataEmissaoRG(event.target.value)}
                />
              </label>
            </div>
            <div className="field-row">
              <label className="field">
                <span className="field-label">Nome Mãe</span>
                <input value={nomeMae} onChange={(event) => setNomeMae(event.target.value)} />
              </label>
              <label className="field">
                <span className="field-label">Nome Pai</span>
                <input value={nomePai} onChange={(event) => setNomePai(event.target.value)} />
              </label>
            </div>

            <h3 className="mt-16">Outras informações</h3>
            <label className="field">
              <span className="field-label">Whatsapp</span>
              <input value={whatsApp} onChange={(event) => setWhatsApp(event.target.value)} />
            </label>
          </div>

          <div className="section" style={{ display: activeStep === 'bancarias' ? 'block' : 'none' }}>
            <div className="section-header">
              <h3>Informações bancárias</h3>
            </div>

            {!hasPessoa ? (
              <p className="disabled-note">
                Salve as informações pessoais primeiro para cadastrar contas bancárias.
              </p>
            ) : (
              <>
                {contaError && <div className="error-banner">{contaError}</div>}

                {contas.length > 0 && (
                  <table className="data-table mt-16" style={{ marginBottom: 16 }}>
                    <thead>
                      <tr>
                        <th>Banco</th>
                        <th>Agência</th>
                        <th>Conta</th>
                        <th>Tipo</th>
                        <th>Principal</th>
                        <th></th>
                      </tr>
                    </thead>
                    <tbody>
                      {contas.map((conta) => (
                        <tr key={conta.id}>
                          <td>{conta.banco}</td>
                          <td>{conta.agencia}</td>
                          <td>
                            {conta.conta}
                            {conta.digitoConta ? `-${conta.digitoConta}` : ''}
                          </td>
                          <td>{conta.tipoConta}</td>
                          <td>{conta.isPrincipal ? 'Sim' : 'Não'}</td>
                          <td>
                            {!conta.isPrincipal && (
                              <button type="button" className="link-btn" onClick={() => handleSetContaPrincipal(conta.id)}>
                                Tornar principal
                              </button>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}

                <div className="field-row">
                  <label className="field">
                    <span className="field-label">Banco</span>
                    <input
                      value={contaForm.banco}
                      onChange={(event) => setContaForm((prev) => ({ ...prev, banco: event.target.value }))}
                    />
                  </label>
                  <label className="field">
                    <span className="field-label">Agência</span>
                    <input
                      value={contaForm.agencia}
                      onChange={(event) => setContaForm((prev) => ({ ...prev, agencia: event.target.value }))}
                    />
                  </label>
                  <label className="field">
                    <span className="field-label">Conta</span>
                    <input
                      value={contaForm.conta}
                      onChange={(event) => setContaForm((prev) => ({ ...prev, conta: event.target.value }))}
                    />
                  </label>
                  <label className="field">
                    <span className="field-label">Dígito</span>
                    <input
                      value={contaForm.digitoConta ?? ''}
                      onChange={(event) => setContaForm((prev) => ({ ...prev, digitoConta: event.target.value }))}
                    />
                  </label>
                  <label className="field">
                    <span className="field-label">Tipo de Conta</span>
                    <input
                      value={contaForm.tipoConta}
                      placeholder="Corrente, Poupança..."
                      onChange={(event) => setContaForm((prev) => ({ ...prev, tipoConta: event.target.value }))}
                    />
                  </label>
                </div>
                <label className="field" style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
                  <input
                    type="checkbox"
                    style={{ width: 'auto' }}
                    checked={contaForm.isPrincipal}
                    onChange={(event) => setContaForm((prev) => ({ ...prev, isPrincipal: event.target.checked }))}
                  />
                  <span className="field-label" style={{ textTransform: 'none' }}>
                    Definir como conta principal
                  </span>
                </label>
                <button
                  type="button"
                  className="btn btn-outline btn-sm"
                  onClick={() => void handleAddConta()}
                  disabled={contaSaving || !contaForm.banco || !contaForm.conta}
                >
                  {contaSaving ? 'Adicionando...' : 'Adicionar conta'}
                </button>
              </>
            )}
          </div>

          <div className="section" style={{ display: activeStep === 'enderecos' ? 'block' : 'none' }}>
            <div className="section-header">
              <h3>Endereços</h3>
            </div>

            {!hasPessoa ? (
              <p className="disabled-note">Salve as informações pessoais primeiro para cadastrar endereços.</p>
            ) : (
              <>
                {enderecoError && <div className="error-banner">{enderecoError}</div>}

                {enderecos.length > 0 && (
                  <table className="data-table mt-16" style={{ marginBottom: 16 }}>
                    <thead>
                      <tr>
                        <th>Logradouro</th>
                        <th>Cidade</th>
                        <th>UF</th>
                        <th>CEP</th>
                        <th>Principal</th>
                        <th></th>
                      </tr>
                    </thead>
                    <tbody>
                      {enderecos.map((endereco) => (
                        <tr key={endereco.id}>
                          <td>
                            {endereco.logradouro}, {endereco.numero}
                          </td>
                          <td>{endereco.cidade}</td>
                          <td>{endereco.uf}</td>
                          <td>{endereco.cep}</td>
                          <td>{endereco.isPrincipal ? 'Sim' : 'Não'}</td>
                          <td>
                            {!endereco.isPrincipal && (
                              <button
                                type="button"
                                className="link-btn"
                                onClick={() => handleSetEnderecoPrincipal(endereco.id)}
                              >
                                Tornar principal
                              </button>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}

                <div className="field-row">
                  <label className="field">
                    <span className="field-label">Logradouro</span>
                    <input
                      value={enderecoForm.logradouro}
                      onChange={(event) => setEnderecoForm((prev) => ({ ...prev, logradouro: event.target.value }))}
                    />
                  </label>
                  <label className="field">
                    <span className="field-label">Número</span>
                    <input
                      value={enderecoForm.numero}
                      onChange={(event) => setEnderecoForm((prev) => ({ ...prev, numero: event.target.value }))}
                    />
                  </label>
                  <label className="field">
                    <span className="field-label">Complemento</span>
                    <input
                      value={enderecoForm.complemento ?? ''}
                      onChange={(event) => setEnderecoForm((prev) => ({ ...prev, complemento: event.target.value }))}
                    />
                  </label>
                </div>
                <div className="field-row">
                  <label className="field">
                    <span className="field-label">Bairro</span>
                    <input
                      value={enderecoForm.bairro}
                      onChange={(event) => setEnderecoForm((prev) => ({ ...prev, bairro: event.target.value }))}
                    />
                  </label>
                  <label className="field">
                    <span className="field-label">Cidade</span>
                    <input
                      value={enderecoForm.cidade}
                      onChange={(event) => setEnderecoForm((prev) => ({ ...prev, cidade: event.target.value }))}
                    />
                  </label>
                  <label className="field">
                    <span className="field-label">UF</span>
                    <input
                      value={enderecoForm.uf}
                      maxLength={2}
                      onChange={(event) => setEnderecoForm((prev) => ({ ...prev, uf: event.target.value }))}
                    />
                  </label>
                  <label className="field">
                    <span className="field-label">CEP</span>
                    <input
                      value={enderecoForm.cep}
                      onChange={(event) => setEnderecoForm((prev) => ({ ...prev, cep: event.target.value }))}
                    />
                  </label>
                  <label className="field">
                    <span className="field-label">País</span>
                    <input
                      value={enderecoForm.pais}
                      onChange={(event) => setEnderecoForm((prev) => ({ ...prev, pais: event.target.value }))}
                    />
                  </label>
                </div>
                <label className="field" style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
                  <input
                    type="checkbox"
                    style={{ width: 'auto' }}
                    checked={enderecoForm.isPrincipal}
                    onChange={(event) => setEnderecoForm((prev) => ({ ...prev, isPrincipal: event.target.checked }))}
                  />
                  <span className="field-label" style={{ textTransform: 'none' }}>
                    Definir como endereço principal
                  </span>
                </label>
                <button
                  type="button"
                  className="btn btn-outline btn-sm"
                  onClick={() => void handleAddEndereco()}
                  disabled={enderecoSaving || !enderecoForm.logradouro || !enderecoForm.cidade}
                >
                  {enderecoSaving ? 'Adicionando...' : 'Adicionar endereço'}
                </button>
              </>
            )}
          </div>

          <div className="form-actions">
            <button type="button" className="btn btn-outline" onClick={() => navigate('/pessoas')}>
              CANCELAR
            </button>
            <button type="submit" className="btn btn-primary" disabled={saving}>
              {saving ? 'Salvando...' : 'SALVAR'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
