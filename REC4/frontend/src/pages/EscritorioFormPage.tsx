import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { createEscritorio, getEscritorio, updateEscritorio } from '../api/escritorios'
import { extractErrorMessage } from '../api/errors'
import { useBreadcrumb } from '../layout/useBreadcrumb'

export default function EscritorioFormPage() {
  const { id } = useParams<{ id: string }>()
  const escritorioId = id ?? null
  const navigate = useNavigate()

  useBreadcrumb([
    { label: 'Escritórios', to: '/escritorios' },
    { label: escritorioId ? 'Editar escritório' : 'Novo escritório' },
  ])

  const [nome, setNome] = useState('')
  const [isActive, setIsActive] = useState(true)

  const [loading, setLoading] = useState<boolean>(Boolean(escritorioId))
  const [loadError, setLoadError] = useState<string | null>(null)
  const [saveError, setSaveError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    if (!escritorioId) {
      setLoading(false)
      return
    }
    let cancelled = false
    setLoading(true)
    getEscritorio(escritorioId)
      .then((data) => {
        if (cancelled) return
        setNome(data.nome)
        setIsActive(data.isActive)
      })
      .catch((err) => {
        if (!cancelled) setLoadError(extractErrorMessage(err, 'Não foi possível carregar o escritório.'))
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [escritorioId])

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSaveError(null)
    setSaving(true)
    try {
      if (escritorioId) {
        await updateEscritorio(escritorioId, { nome, isActive })
      } else {
        await createEscritorio({ nome })
      }
      navigate('/escritorios')
    } catch (err) {
      setSaveError(extractErrorMessage(err, 'Não foi possível salvar o escritório.'))
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

  if (loadError) {
    return (
      <div className="card">
        <div className="error-banner">{loadError}</div>
        <Link to="/escritorios" className="btn btn-outline">
          Voltar para a lista
        </Link>
      </div>
    )
  }

  return (
    <div className="card" style={{ maxWidth: 480 }}>
      <h2 className="card-title">{escritorioId ? 'Editar escritório' : 'Novo escritório'}</h2>

      {saveError && <div className="error-banner">{saveError}</div>}

      <form onSubmit={handleSubmit}>
        <label className="field">
          <span className="field-label">Nome</span>
          <input value={nome} onChange={(event) => setNome(event.target.value)} required />
        </label>

        {escritorioId && (
          <label className="field" style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
            <input
              type="checkbox"
              style={{ width: 'auto' }}
              checked={isActive}
              onChange={(event) => setIsActive(event.target.checked)}
            />
            <span className="field-label" style={{ textTransform: 'none' }}>
              Ativo
            </span>
          </label>
        )}

        <div className="form-actions">
          <button type="button" className="btn btn-outline" onClick={() => navigate('/escritorios')}>
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
