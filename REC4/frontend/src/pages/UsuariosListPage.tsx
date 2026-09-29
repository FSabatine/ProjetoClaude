import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { listUsuarios } from '../api/usuarios'
import { extractErrorMessage } from '../api/errors'
import { useBreadcrumb } from '../layout/useBreadcrumb'
import type { UsuarioListItem } from '../types/usuarios'

export default function UsuariosListPage() {
  useBreadcrumb([{ label: 'Usuários' }])

  const [usuarios, setUsuarios] = useState<UsuarioListItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    listUsuarios()
      .then((data) => {
        if (!cancelled) setUsuarios(data)
      })
      .catch((err) => {
        if (!cancelled) setError(extractErrorMessage(err, 'Não foi possível carregar os usuários.'))
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
          Usuários
        </h2>
        <Link to="/usuarios/novo" className="btn btn-primary">
          Novo usuário
        </Link>
      </div>

      {error && <div className="error-banner">{error}</div>}

      {loading ? (
        <p className="text-muted">Carregando...</p>
      ) : usuarios.length === 0 ? (
        <div className="empty-state">Nenhum usuário cadastrado.</div>
      ) : (
        <table className="data-table">
          <thead>
            <tr>
              <th>Nome</th>
              <th>Login</th>
              <th>Grupo</th>
              <th>Status</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {usuarios.map((usuario) => (
              <tr key={usuario.id}>
                <td>{usuario.nome}</td>
                <td>{usuario.login}</td>
                <td>{usuario.grupoNome}</td>
                <td>
                  <span className={`status-badge ${usuario.isActive ? 'status-badge-active' : 'status-badge-inactive'}`}>
                    {usuario.isActive ? 'Ativo' : 'Inativo'}
                  </span>
                </td>
                <td>
                  <Link to={`/usuarios/${usuario.id}/editar`} className="link-btn">
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
