import { Link } from 'react-router-dom'
import { useBreadcrumbContext, type BreadcrumbItem } from './BreadcrumbContext'

export default function Breadcrumb() {
  const { items } = useBreadcrumbContext()
  const allItems: BreadcrumbItem[] = [{ label: 'Home', to: '/' }, ...items]

  return (
    <nav className="breadcrumb" aria-label="breadcrumb">
      {allItems.map((item, index) => {
        const isLast = index === allItems.length - 1
        return (
          <span key={`${item.label}-${index}`} className="breadcrumb-item">
            {index > 0 && <span className="breadcrumb-sep">&gt;</span>}
            {!isLast && item.to ? (
              <Link to={item.to} className="breadcrumb-link">
                {item.label}
              </Link>
            ) : (
              <span className="breadcrumb-current">{item.label}</span>
            )}
          </span>
        )
      })}
    </nav>
  )
}
