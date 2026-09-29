import { Link } from 'react-router-dom'

export default function UnauthorizedPage() {
  return (
    <div className="auth-page">
      <div className="auth-card">
        <h1 className="auth-title">Acesso negado</h1>
        <p className="auth-subtitle">Você não tem permissão para acessar esta página.</p>
        <Link to="/" className="btn btn-primary btn-block">
          Voltar para o início
        </Link>
      </div>
    </div>
  )
}
