import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import {
  addUsuarioEmitente,
  addUsuarioEscritorio,
  createUsuario,
  getUsuario,
  listGrupos,
  removeUsuarioEmitente,
  removeUsuarioEscritorio,
  setUsuarioEscritorioPrincipal,
  setUsuarioPermissoes,
  updateUsuario,
} from '../api/usuarios'
import { listEscritorios } from '../api/escritorios'
import { listPerfis, listPessoasPorPerfil } from '../api/pessoas'
import { listPermissoes } from '../api/usuarios'
import { extractErrorMessage } from '../api/errors'
import { useBreadcrumb } from '../layout/useBreadcrumb'
import PermissionModuleGrid from './PermissionModuleGrid'
import type { EscritorioDto } from '../types/escritorios'
import type { PerfilDto, PessoaDto } from '../types/pessoas'
import type {
  GrupoDto,
  PermissaoDto,
  TipoUsuario,
  UsuarioCreateDto,
  UsuarioDetail,
  UsuarioUpdateDto,
} from '../types/usuarios'

type Tab = 'permissoes' | 'vinculos'

const COLABORADOR_NOME = 'Colaborador'
const EMITENTE_NOME = 'Emitente'

export default function UsuarioFormPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()

  useBreadcrumb([{ label: 'Usuários', to: '/usuarios' }, { label: id ? 'Editar usuário' : 'Novo usuário' }])

  const [usuario, setUsuario] = useState<UsuarioDetail | null>(null)
  const [loading, setLoading] = useState<boolean>(Boolean(id))
  const [loadError, setLoadError] = useState<string | null>(null)

  const [nome, setNome] = useState('')
  const [login, setLogin] = useState('')
  const [senha, setSenha] = useState('')
  const [grupoId, setGrupoId] = useState<number | ''>('')
  const [isActive, setIsActive] = useState(true)
  const [pessoaId, setPessoaId] = useState('')
  const [tipoUsuario, setTipoUsuario] = useState<TipoUsuario>(2)
  const [limiteDias, setLimiteDias] = useState('')

  const [grupos, setGrupos] = useState<GrupoDto[]>([])
  const [colaboradores, setColaboradores] = useState<PessoaDto[]>([])
  const [emitentesDisponiveis, setEmitentesDisponiveis] = useState<PessoaDto[]>([])
  const [allEscritorios, setAllEscritorios] = useState<EscritorioDto[]>([])
  const [allPermissoes, setAllPermissoes] = useState<PermissaoDto[]>([])

  const [activeTab, setActiveTab] = useState<Tab>('permissoes')
  const [saveError, setSaveError] = useState<string | null>(null)
  const [saveSuccess, setSaveSuccess] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [permissoesError, setPermissoesError] = useState<string | null>(null)
  const [vinculosError, setVinculosError] = useState<string | null>(null)

  const usuarioId = usuario?.id ?? id ?? null
  const hasUsuario = Boolean(usuarioId)

  useEffect(() => {
    listGrupos().then(setGrupos).catch(() => undefined)
    listPermissoes().then(setAllPermissoes).catch(() => undefined)
    listEscritorios().then(setAllEscritorios).catch(() => undefined)
    listPerfis().then((perfis: PerfilDto[]) => {
      const colaborador = perfis.find((p) => p.nome === COLABORADOR_NOME)
      const emitente = perfis.find((p) => p.nome === EMITENTE_NOME)
      if (colaborador) listPessoasPorPerfil(colaborador.id).then(setColaboradores).catch(() => undefined)
      if (emitente) listPessoasPorPerfil(emitente.id).then(setEmitentesDisponiveis).catch(() => undefined)
    })
  }, [])

  useEffect(() => {
    if (!id) {
      setLoading(false)
      return
    }
    let cancelled = false
    setLoading(true)
    getUsuario(id)
      .then((data) => {
        if (!cancelled) applyUsuario(data)
      })
      .catch((err) => {
        if (!cancelled) setLoadError(extractErrorMessage(err, 'Não foi possível carregar o usuário.'))
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [id])

  function applyUsuario(data: UsuarioDetail) {
    setUsuario(data)
    setNome(data.nome)
    setLogin(data.login)
    setGrupoId(data.grupoId)
    setIsActive(data.isActive)
    setPessoaId(data.pessoaId ?? '')
    setTipoUsuario(data.tipoUsuario)
    setLimiteDias(data.limiteDiasEdicaoFinanceiro === null ? '' : String(data.limiteDiasEdicaoFinanceiro))
  }

  async function handleSubmitBasico(event: FormEvent) {
    event.preventDefault()
    if (grupoId === '') {
      setSaveError('Selecione um grupo.')
      return
    }
    setSaveError(null)
    setSaveSuccess(null)
    setSaving(true)
    try {
      const limiteDiasValue = limiteDias.trim() === '' ? null : Number(limiteDias)
      if (usuarioId) {
        const dto: UsuarioUpdateDto = {
          nome, grupoId, isActive, pessoaId: pessoaId || null, tipoUsuario, limiteDiasEdicaoFinanceiro: limiteDiasValue,
        }
        const updated = await updateUsuario(usuarioId, dto)
        applyUsuario(updated)
        setSaveSuccess('Dados salvos com sucesso.')
      } else {
        const dto: UsuarioCreateDto = {
          nome, login, senha, grupoId, pessoaId: pessoaId || null, tipoUsuario, limiteDiasEdicaoFinanceiro: limiteDiasValue,
        }
        const created = await createUsuario(dto)
        navigate(`/usuarios/${created.id}/editar`, { replace: true })
      }
    } catch (err) {
      setSaveError(extractErrorMessage(err, 'Não foi possível salvar o usuário.'))
    } finally {
      setSaving(false)
    }
  }

  const permissoesEfetivas = usuario?.permissoesEfetivas ?? []
  const selectedPermissaoIds = new Set(allPermissoes.filter((p) => permissoesEfetivas.includes(p.chave)).map((p) => p.id))

  async function handleTogglePermissao(permissaoId: number, checked: boolean) {
    if (!usuarioId) return
    setPermissoesError(null)
    const nextIds = checked
      ? [...selectedPermissaoIds, permissaoId]
      : Array.from(selectedPermissaoIds).filter((pid) => pid !== permissaoId)
    try {
      const updated = await setUsuarioPermissoes(usuarioId, { personalizado: true, permissaoIds: nextIds })
      applyUsuario(updated)
    } catch (err) {
      setPermissoesError(extractErrorMessage(err, 'Não foi possível atualizar as permissões.'))
    }
  }

  async function handleRestaurarPermissoesDoGrupo() {
    if (!usuarioId) return
    setPermissoesError(null)
    try {
      const updated = await setUsuarioPermissoes(usuarioId, { personalizado: false, permissaoIds: [] })
      applyUsuario(updated)
    } catch (err) {
      setPermissoesError(extractErrorMessage(err, 'Não foi possível restaurar as permissões do grupo.'))
    }
  }

  const escritorios = usuario?.escritorios ?? []
  const emitentes = usuario?.emitentes ?? []
  const escritoriosDisponiveis = allEscritorios.filter((e) => !escritorios.some((ue) => ue.id === e.id))
  const emitentesParaAdicionar = emitentesDisponiveis.filter((p) => !emitentes.some((ue) => ue.pessoaId === p.id))
  const todosEscritoriosVinculados = allEscritorios.length > 0 && escritorios.length === allEscritorios.length

  async function handleAddEscritorio(escritorioId: string) {
    if (!usuarioId || !escritorioId) return
    setVinculosError(null)
    try {
      applyUsuario(await addUsuarioEscritorio(usuarioId, { escritorioId, isPrincipal: false }))
    } catch (err) {
      setVinculosError(extractErrorMessage(err, 'Não foi possível vincular o escritório.'))
    }
  }

  async function handleSetEscritorioPrincipal(escritorioId: string) {
    if (!usuarioId) return
    setVinculosError(null)
    try {
      applyUsuario(await setUsuarioEscritorioPrincipal(usuarioId, escritorioId))
    } catch (err) {
      setVinculosError(extractErrorMessage(err, 'Não foi possível definir o escritório principal.'))
    }
  }

  async function handleRemoveEscritorio(escritorioId: string) {
    if (!usuarioId) return
    setVinculosError(null)
    try {
      applyUsuario(await removeUsuarioEscritorio(usuarioId, escritorioId))
    } catch (err) {
      setVinculosError(extractErrorMessage(err, 'Não foi possível remover o escritório.'))
    }
  }

  async function handleToggleTodosEscritorios(checked: boolean) {
    if (!usuarioId) return
    setVinculosError(null)
    try {
      if (checked) {
        for (const escritorio of escritoriosDisponiveis) {
          applyUsuario(await addUsuarioEscritorio(usuarioId, { escritorioId: escritorio.id, isPrincipal: false }))
        }
      } else {
        for (const vinculo of [...escritorios]) {
          applyUsuario(await removeUsuarioEscritorio(usuarioId, vinculo.id))
        }
      }
    } catch (err) {
      setVinculosError(extractErrorMessage(err, 'Não foi possível atualizar os escritórios vinculados.'))
    }
  }

  async function handleAddEmitente(pessoaIdToAdd: string) {
    if (!usuarioId || !pessoaIdToAdd) return
    setVinculosError(null)
    try {
      applyUsuario(await addUsuarioEmitente(usuarioId, { pessoaId: pessoaIdToAdd }))
    } catch (err) {
      setVinculosError(extractErrorMessage(err, 'Não foi possível vincular o emitente.'))
    }
  }

  async function handleRemoveEmitente(pessoaIdToRemove: string) {
    if (!usuarioId) return
    setVinculosError(null)
    try {
      applyUsuario(await removeUsuarioEmitente(usuarioId, pessoaIdToRemove))
    } catch (err) {
      setVinculosError(extractErrorMessage(err, 'Não foi possível remover o emitente.'))
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
        <Link to="/usuarios" className="btn btn-outline">
          Voltar para a lista
        </Link>
      </div>
    )
  }

  return (
    <>
      <div className="card" style={{ marginBottom: 20 }}>
        <h2 className="card-title">Cadastro usuário</h2>
        {saveError && <div className="error-banner">{saveError}</div>}
        {saveSuccess && <div className="success-banner">{saveSuccess}</div>}

        <form onSubmit={handleSubmitBasico}>
          <div className="field-row">
            <label className="field">
              <span className="field-label">E-mail/login</span>
              <input type="email" value={login} onChange={(event) => setLogin(event.target.value)} disabled={hasUsuario} required />
            </label>
            {hasUsuario ? (
              <label className="field">
                <span className="field-label">Senha</span>
                <button type="button" className="btn btn-outline btn-sm" disabled title="Não implementado nesta etapa" style={{ width: 'fit-content' }}>
                  Trocar senha
                </button>
              </label>
            ) : (
              <label className="field">
                <span className="field-label">Senha login manual</span>
                <input type="password" value={senha} onChange={(event) => setSenha(event.target.value)} required />
              </label>
            )}
            <label className="field">
              <span className="field-label">Vínculo pessoa</span>
              <select value={pessoaId} onChange={(event) => setPessoaId(event.target.value)}>
                <option value="">Selecione uma pessoa</option>
                {colaboradores.map((pessoa) => (
                  <option key={pessoa.id} value={pessoa.id}>
                    {pessoa.nome}
                  </option>
                ))}
              </select>
              <Link to="/pessoas/novo" className="link-btn" style={{ marginTop: 4 }}>
                + Adicionar novo colaborador
              </Link>
            </label>
          </div>

          <div className="field-row">
            <label className="field">
              <span className="field-label">Tipo de usuário</span>
              <select value={tipoUsuario} onChange={(event) => setTipoUsuario(Number(event.target.value) as TipoUsuario)} required>
                <option value={0}>Administrador</option>
                <option value={1}>Gestor</option>
                <option value={2}>Operador</option>
              </select>
            </label>
            <label className="field">
              <span className="field-label">Limite de dias para edição financeiro</span>
              <input
                type="number"
                min={0}
                value={limiteDias}
                placeholder="Ilimitado"
                onChange={(event) => setLimiteDias(event.target.value)}
              />
            </label>
            <label className="field">
              <span className="field-label">Usuário REC</span>
              <select disabled defaultValue="" title="Campo não mapeado no backend nesta etapa (sem integração com REC1)">
                <option value="">Não mapeado</option>
              </select>
            </label>
          </div>

          {hasUsuario && (
            <label className="field" style={{ flexDirection: 'row', alignItems: 'center', gap: 8, maxWidth: 200 }}>
              <input type="checkbox" style={{ width: 'auto' }} checked={isActive} onChange={(event) => setIsActive(event.target.checked)} />
              <span className="field-label" style={{ textTransform: 'none' }}>
                Usuário ativo
              </span>
            </label>
          )}

          <label className="field" style={{ maxWidth: 320 }}>
            <span className="field-label">Nome completo</span>
            <input value={nome} onChange={(event) => setNome(event.target.value)} required />
          </label>

          <label className="field" style={{ maxWidth: 320 }}>
            <span className="field-label">Grupo</span>
            <select
              value={grupoId}
              onChange={(event) => setGrupoId(event.target.value === '' ? '' : Number(event.target.value))}
              required
            >
              <option value="" disabled>
                Selecione...
              </option>
              {grupos.map((grupo) => (
                <option key={grupo.id} value={grupo.id}>
                  {grupo.nome}
                </option>
              ))}
            </select>
          </label>

          <div className="form-actions">
            <button type="button" className="btn btn-outline" onClick={() => navigate('/usuarios')}>
              Cancelar
            </button>
            <button type="submit" className="btn btn-primary" disabled={saving}>
              {saving ? 'Salvando...' : 'Salvar'}
            </button>
          </div>
        </form>
      </div>

      {!hasUsuario ? (
        <p className="disabled-note">Salve os dados básicos primeiro para configurar permissões e vínculos.</p>
      ) : (
        <div className="card">
          <div style={{ display: 'flex', gap: 8, marginBottom: 20 }}>
            <button
              type="button"
              className={`btn ${activeTab === 'permissoes' ? 'btn-primary' : 'btn-outline'}`}
              onClick={() => setActiveTab('permissoes')}
            >
              Grupo de permissões
            </button>
            <button
              type="button"
              className={`btn ${activeTab === 'vinculos' ? 'btn-primary' : 'btn-outline'}`}
              onClick={() => setActiveTab('vinculos')}
            >
              Vínculos
            </button>
          </div>

          {activeTab === 'permissoes' && (
            <div>
              {permissoesError && <div className="error-banner">{permissoesError}</div>}
              <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 14 }}>
                {usuario?.temPermissaoPersonalizada && <span className="badge-personalizado">Personalizado</span>}
                {usuario?.temPermissaoPersonalizada && (
                  <button type="button" className="link-btn" onClick={handleRestaurarPermissoesDoGrupo}>
                    Restaurar permissões do grupo
                  </button>
                )}
              </div>
              <PermissionModuleGrid allPermissoes={allPermissoes} selectedIds={selectedPermissaoIds} onToggle={handleTogglePermissao} />
            </div>
          )}

          {activeTab === 'vinculos' && (
            <div>
              {vinculosError && <div className="error-banner">{vinculosError}</div>}
              <div className="vinculos-grid">
                <div className="vinculo-panel">
                  <h3 className="card-title" style={{ fontSize: 14 }}>
                    Escritórios
                  </h3>
                  <label className="field" style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
                    <input
                      type="checkbox"
                      style={{ width: 'auto' }}
                      checked={todosEscritoriosVinculados}
                      onChange={(event) => void handleToggleTodosEscritorios(event.target.checked)}
                    />
                    <span className="field-label" style={{ textTransform: 'none' }}>
                      Adicionar todos escritórios
                    </span>
                  </label>
                  <label className="field">
                    <span className="field-label">Selecione os escritórios</span>
                    <select
                      value=""
                      onChange={(event) => {
                        if (event.target.value) void handleAddEscritorio(event.target.value)
                      }}
                    >
                      <option value="">Pesquise ou selecione um escritório</option>
                      {escritoriosDisponiveis.map((escritorio) => (
                        <option key={escritorio.id} value={escritorio.id}>
                          {escritorio.nome}
                        </option>
                      ))}
                    </select>
                  </label>
                  {escritorios.length > 0 && (
                    <table className="data-table">
                      <thead>
                        <tr>
                          <th>Escritórios</th>
                          <th>Principal</th>
                          <th></th>
                        </tr>
                      </thead>
                      <tbody>
                        {escritorios.map((vinculo) => (
                          <tr key={vinculo.id}>
                            <td>{vinculo.nome}</td>
                            <td>
                              {vinculo.isPrincipal ? (
                                <span className="badge-principal">Principal</span>
                              ) : (
                                <button type="button" className="link-btn" onClick={() => handleSetEscritorioPrincipal(vinculo.id)}>
                                  Tornar principal
                                </button>
                              )}
                            </td>
                            <td>
                              <button type="button" className="btn btn-outline btn-sm" onClick={() => handleRemoveEscritorio(vinculo.id)}>
                                Excluir
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  )}
                </div>

                <div className="vinculo-panel">
                  <h3 className="card-title" style={{ fontSize: 14 }}>
                    Emitentes
                  </h3>
                  <label className="field">
                    <span className="field-label">Selecione os emitentes</span>
                    <select
                      value=""
                      onChange={(event) => {
                        if (event.target.value) void handleAddEmitente(event.target.value)
                      }}
                    >
                      <option value="">Pesquise ou selecione um emitente</option>
                      {emitentesParaAdicionar.map((pessoa) => (
                        <option key={pessoa.id} value={pessoa.id}>
                          {pessoa.nome}
                        </option>
                      ))}
                    </select>
                  </label>
                  {emitentes.length > 0 && (
                    <table className="data-table">
                      <thead>
                        <tr>
                          <th>Emitentes</th>
                          <th></th>
                        </tr>
                      </thead>
                      <tbody>
                        {emitentes.map((vinculo) => (
                          <tr key={vinculo.pessoaId}>
                            <td>{vinculo.nome}</td>
                            <td>
                              <button type="button" className="btn btn-outline btn-sm" onClick={() => handleRemoveEmitente(vinculo.pessoaId)}>
                                Excluir
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  )}
                </div>
              </div>
            </div>
          )}
        </div>
      )}
    </>
  )
}
