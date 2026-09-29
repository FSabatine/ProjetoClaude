import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { listEscritorios } from '../api/escritorios'
import { extractErrorMessage } from '../api/errors'
import { useBreadcrumb } from '../layout/useBreadcrumb'
import type { EscritorioDto } from '../types/escritorios'

export default function EscritoriosListPage() {
  useBreadcrumb([{ label: 'Escritórios' }])

  const [escritorios, setEscritorios] = useState<EscritorioDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    listEscritorios()
      .then((data) => {
        if (!cancelled) setEscritorios(data)
      })
      .catch((err) => {
        if (!cancelled) setError(extractErrorMessage(err, 'Não foi possível carregar os escritórios.'))
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
          Escritórios
        </h2>
        <Link to="/escritorios/novo" className="btn btn-primary">
          Novo escritório
        </Link>
      </div>

      {error && <div className="error-banner">{error}</div>}

      {loading ? (
        <p className="text-muted">Carregando...</p>
      ) : escritorios.length === 0 ? (
        <div className="empty-state">Nenhum escritório cadastrado.</div>
      ) : (
        <table className="data-table">
          <thead>
            <tr>
              <th>Nome</th>
              <th>Status</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {escritorios.map((escritorio) => (
              <tr key={escritorio.id}>
                <td>{escritorio.nome}</td>
                <td>
                  <span className={`status-badge ${escritorio.isActive ? 'status-badge-active' : 'status-badge-inactive'}`}>
                    {escritorio.isActive ? 'Ativo' : 'Inativo'}
                  </span>
                </td>
                <td>
                  <Link to={`/escritorios/${escritorio.id}/editar`} className="link-btn">
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
