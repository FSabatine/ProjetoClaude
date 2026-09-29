import { Link } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import { useBreadcrumbContext } from './BreadcrumbContext'
import { ChevronDownIcon, MenuIcon } from './icons'

interface TopBarProps {
  onToggleSidebar: () => void
}

export default function TopBar({ onToggleSidebar }: TopBarProps) {
  const { usuario } = useAuth()
  const { items } = useBreadcrumbContext()
  const pageTitle = items.length > 0 ? items[items.length - 1].label : ''

  return (
    <header className="topbar">
      <button type="button" className="topbar-menu-btn" onClick={onToggleSidebar} aria-label="Alternar menu">
        <MenuIcon />
      </button>
      <div className="topbar-brand">
        <span className="wordmark">
          REC<span className="accent">4</span>
        </span>
        {pageTitle && <span className="page-title">{pageTitle}</span>}
      </div>
      <div className="topbar-spacer" />
      <Link to="/perfil" className="topbar-user">
        <span className="avatar-placeholder" aria-hidden="true" />
        <span className="user-name">{usuario?.nome ?? ''}</span>
        <ChevronDownIcon />
      </Link>
    </header>
  )
}
