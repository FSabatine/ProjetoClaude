import { useEffect, useState } from 'react'
import { getMe } from '../api/auth'
import { extractErrorMessage } from '../api/errors'
import { useAuth } from '../auth/useAuth'
import { useBreadcrumb } from '../layout/useBreadcrumb'
import type { MeResponse } from '../types/auth'

export default function PerfilPage() {
  useBreadcrumb([{ label: 'Meu perfil' }])

  const { usuario } = useAuth()
  const [me, setMe] = useState<MeResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    getMe()
      .then((data) => {
        if (!cancelled) setMe(data)
      })
      .catch((err) => {
        if (!cancelled) setError(extractErrorMessage(err, 'Não foi possível carregar os dados do perfil.'))
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [])

  const display = me ?? (usuario ? { ...usuario, isActive: true } : null)

  return (
    <div className="card" style={{ maxWidth: 560 }}>
      <h2 className="card-title">Meu perfil</h2>

      {error && <div className="error-banner">{error}</div>}
      {loading && !display && <p className="text-muted">Carregando...</p>}

      {display && (
        <>
          <div className="field">
            <span className="field-label">Nome</span>
            <input value={display.nome} disabled readOnly />
          </div>
          <div className="field">
            <span className="field-label">Login</span>
            <input value={display.login} disabled readOnly />
          </div>
          <div className="field">
            <span className="field-label">Grupo</span>
            <input value={display.grupo} disabled readOnly />
          </div>
          <div className="field">
            <span className="field-label">Permissões efetivas</span>
            {display.permissoes.length === 0 ? (
              <p className="text-muted">Nenhuma permissão atribuída.</p>
            ) : (
              <div className="pill-box pill-box-disabled">
                {display.permissoes.map((permissao) => (
                  <span key={permissao} className="pill">
                    {permissao}
                  </span>
                ))}
              </div>
            )}
          </div>
        </>
      )}
    </div>
  )
}
