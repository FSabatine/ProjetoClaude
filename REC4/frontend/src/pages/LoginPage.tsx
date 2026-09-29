import { useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { extractErrorMessage } from '../api/errors'
import { useAuth } from '../auth/useAuth'

interface LocationState {
  from?: { pathname: string }
}

export default function LoginPage() {
  const [loginValue, setLoginValue] = useState('')
  const [senha, setSenha] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const { login, isAuthenticated } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()

  const state = location.state as LocationState | null
  const from = state?.from?.pathname ?? '/'

  if (isAuthenticated) {
    return <Navigate to={from} replace />
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      await login(loginValue, senha)
      navigate(from, { replace: true })
    } catch (err) {
      setError(extractErrorMessage(err, 'Não foi possível entrar. Verifique login e senha.'))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="auth-page">
      <div className="auth-card">
        <h1 className="auth-title">
          REC<span className="accent">4</span>
        </h1>
        <p className="auth-subtitle">Acesse sua conta</p>

        {error && <div className="error-banner">{error}</div>}

        <form onSubmit={handleSubmit}>
          <label className="field">
            <span className="field-label">Login</span>
            <input
              value={loginValue}
              onChange={(event) => setLoginValue(event.target.value)}
              autoFocus
              autoComplete="username"
              required
            />
          </label>
          <label className="field">
            <span className="field-label">Senha</span>
            <input
              type="password"
              value={senha}
              onChange={(event) => setSenha(event.target.value)}
              autoComplete="current-password"
              required
            />
          </label>
          <button type="submit" className="btn btn-primary btn-block" disabled={submitting}>
            {submitting ? 'Entrando...' : 'Entrar'}
          </button>
        </form>
      </div>
    </div>
  )
}
