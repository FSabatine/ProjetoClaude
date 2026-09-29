import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { createGrupo, getGrupo, listPermissoes, updateGrupo } from '../api/usuarios'
import { extractErrorMessage } from '../api/errors'
import { useBreadcrumb } from '../layout/useBreadcrumb'
import PermissionModuleGrid from './PermissionModuleGrid'
import type { PermissaoDto } from '../types/usuarios'

export default function GrupoFormPage() {
  const { id } = useParams<{ id: string }>()
  const grupoId = id ? Number(id) : null
  const navigate = useNavigate()

  useBreadcrumb([{ label: 'Grupos', to: '/grupos' }, { label: grupoId ? 'Editar grupo' : 'Novo grupo' }])

  const [nome, setNome] = useState('')
  const [allPermissoes, setAllPermissoes] = useState<PermissaoDto[]>([])
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set())

  const [loading, setLoading] = useState<boolean>(Boolean(grupoId))
  const [loadError, setLoadError] = useState<string | null>(null)
  const [saveError, setSaveError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    listPermissoes()
      .then(setAllPermissoes)
      .catch((err) => setLoadError(extractErrorMessage(err, 'Não foi possível carregar as permissões.')))
  }, [])

  useEffect(() => {
    if (!grupoId) {
      setLoading(false)
      return
    }
    let cancelled = false
    setLoading(true)
    getGrupo(grupoId)
      .then((data) => {
        if (cancelled) return
        setNome(data.nome)
        setSelectedIds(new Set(data.permissoes.map((p) => p.id)))
      })
      .catch((err) => {
        if (!cancelled) setLoadError(extractErrorMessage(err, 'Não foi possível carregar o grupo.'))
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [grupoId])

  function handleTogglePermissao(permissaoId: number, checked: boolean) {
    setSelectedIds((prev) => {
      const next = new Set(prev)
      if (checked) next.add(permissaoId)
      else next.delete(permissaoId)
      return next
    })
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSaveError(null)
    setSaving(true)
    try {
      const permissaoIds = Array.from(selectedIds)
      if (grupoId) {
        await updateGrupo(grupoId, { nome, permissaoIds })
      } else {
        await createGrupo({ nome, permissaoIds })
      }
      navigate('/grupos')
    } catch (err) {
      setSaveError(extractErrorMessage(err, 'Não foi possível salvar o grupo.'))
    } finally {
      setSaving(false)
    }
  }

  if (loading) {
    return (
      <div className="card">
        <p className="text-muted">Carregando...</p>
      </div>
    )
  }

  if (loadError && allPermissoes.length === 0) {
    return (
      <div className="card">
        <div className="error-banner">{loadError}</div>
        <Link to="/grupos" className="btn btn-outline">
          Voltar para a lista
        </Link>
      </div>
    )
  }

  return (
    <div className="card">
      <h2 className="card-title">{grupoId ? 'Editar grupo' : 'Novo grupo'}</h2>

      {saveError && <div className="error-banner">{saveError}</div>}

      <form onSubmit={handleSubmit}>
        <label className="field" style={{ maxWidth: 360 }}>
          <span className="field-label">Nome</span>
          <input value={nome} onChange={(event) => setNome(event.target.value)} required />
        </label>

        <h3 className="mt-16" style={{ marginBottom: 12 }}>
          Módulos
        </h3>
        <PermissionModuleGrid allPermissoes={allPermissoes} selectedIds={selectedIds} onToggle={handleTogglePermissao} />

        <div className="form-actions">
          <button type="button" className="btn btn-outline" onClick={() => navigate('/grupos')}>
            Cancelar
          </button>
          <button type="submit" className="btn btn-primary" disabled={saving || !nome}>
            {saving ? 'Salvando...' : 'Salvar'}
          </button>
        </div>
      </form>
    </div>
  )
}
