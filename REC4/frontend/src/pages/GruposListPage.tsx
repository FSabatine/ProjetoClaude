import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { listGrupos } from '../api/usuarios'
import { extractErrorMessage } from '../api/errors'
import { useBreadcrumb } from '../layout/useBreadcrumb'
import type { GrupoDto } from '../types/usuarios'

export default function GruposListPage() {
  useBreadcrumb([{ label: 'Grupos' }])

  const [grupos, setGrupos] = useState<GrupoDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    listGrupos()
      .then((data) => {
        if (!cancelled) setGrupos(data)
      })
      .catch((err) => {
        if (!cancelled) setError(extractErrorMessage(err, 'Não foi possível carregar os grupos.'))
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [])

  return (
    <div className="card">
      <div className="list-toolbar">
        <h2 className="card-title" style={{ margin: 0 }}>
          Grupos
        </h2>
        <Link to="/grupos/novo" className="btn btn-primary">
          Novo grupo
        </Link>
      </div>

      {error && <div className="error-banner">{error}</div>}

      {loading ? (
        <p className="text-muted">Carregando...</p>
      ) : grupos.length === 0 ? (
        <div className="empty-state">Nenhum grupo cadastrado.</div>
      ) : (
        <table className="data-table">
          <thead>
            <tr>
              <th>Nome</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {grupos.map((grupo) => (
              <tr key={grupo.id}>
                <td>{grupo.nome}</td>
                <td>
                  <Link to={`/grupos/${grupo.id}/editar`} className="link-btn">
                    Editar
                  </Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}
