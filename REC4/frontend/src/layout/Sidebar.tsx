import { useState, type ReactNode } from 'react'
import { NavLink, useLocation } from 'react-router-dom'
import { usePermission } from '../auth/usePermission'
import {
  ChartIcon,
  ChevronDownIcon,
  DollarIcon,
  GearIcon,
  PersonPlusIcon,
  ShieldIcon,
  TruckIcon,
  WrenchIcon,
} from './icons'

interface SidebarProps {
  open: boolean
}

interface NavItem {
  label: string
  to: string
}

interface NavGroup {
  key: string
  label: string
  icon: ReactNode
  items: NavItem[]
}

const NOT_IMPLEMENTED_TITLE = 'Não implementado nesta etapa'

export default function Sidebar({ open }: SidebarProps) {
  const location = useLocation()
  const canViewUsuarios = usePermission('Usuario.Visualizar')
  const canViewEscritorios = usePermission('Escritorio.Visualizar')

  const cadastroItems: NavItem[] = [{ label: 'Pessoas', to: '/pessoas' }]
  if (canViewEscritorios) cadastroItems.push({ label: 'Escritórios', to: '/escritorios' })

  const groups: NavGroup[] = [
    { key: 'cadastro', label: 'Cadastro', icon: <PersonPlusIcon />, items: cadastroItems },
    { key: 'comercial', label: 'Comercial', icon: <ChartIcon />, items: [] },
    { key: 'transporte', label: 'Transporte', icon: <TruckIcon />, items: [] },
    { key: 'financeiro', label: 'Financeiro', icon: <DollarIcon />, items: [] },
    { key: 'ferramentas', label: 'Ferramentas', icon: <WrenchIcon />, items: [] },
    {
      key: 'permissoes',
      label: 'Permissões',
      icon: <ShieldIcon />,
      items: canViewUsuarios ? [{ label: 'Grupos', to: '/grupos' }, { label: 'Usuários', to: '/usuarios' }] : [],
    },
  ]

  const groupContainingCurrentPath = groups.find((group) => group.items.some((item) => location.pathname.startsWith(item.to)))
  const [expandedKey, setExpandedKey] = useState<string | null>(groupContainingCurrentPath?.key ?? 'cadastro')

  return (
    <aside className={`sidebar${open ? '' : ' sidebar-collapsed'}`}>
      <nav className="sidebar-nav">
        {groups.map((group) => {
          const implemented = group.items.length > 0
          const expanded = expandedKey === group.key && implemented
          return (
            <div key={group.key} className="sidebar-group">
              <button
                type="button"
                className={`sidebar-group-header${implemented ? '' : ' sidebar-group-disabled'}`}
                disabled={!implemented}
                title={implemented ? undefined : NOT_IMPLEMENTED_TITLE}
                onClick={() => setExpandedKey((prev) => (prev === group.key ? null : group.key))}
              >
                <span className="sidebar-group-icon">{group.icon}</span>
                <span className="sidebar-group-label">{group.label}</span>
                {implemented && (
                  <span className={`sidebar-chevron${expanded ? ' sidebar-chevron-open' : ''}`}>
                    <ChevronDownIcon />
                  </span>
                )}
              </button>
              {expanded && (
                <div className="sidebar-subitems">
                  {group.items.map((item) => (
                    <NavLink
                      key={item.to}
                      to={item.to}
                      className={({ isActive }) => `sidebar-subitem${isActive ? ' sidebar-subitem-active' : ''}`}
                    >
                      {item.label}
                    </NavLink>
                  ))}
                </div>
              )}
            </div>
          )
        })}
      </nav>
      <div className="sidebar-footer">
        <button type="button" className="sidebar-group-header sidebar-group-disabled" title={NOT_IMPLEMENTED_TITLE} disabled>
          <span className="sidebar-group-icon">
            <GearIcon />
          </span>
          <span className="sidebar-group-label">Configurações</span>
        </button>
      </div>
    </aside>
  )
}
