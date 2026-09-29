import { Navigate, Outlet } from 'react-router-dom'
import { usePermission } from './usePermission'

interface RequirePermissionProps {
  permissao: string
}

/**
 * Route guard: redirects to /unauthorized when the logged in user lacks the
 * given permission key. UX convenience only - the backend enforces the real rule.
 */
export default function RequirePermission({ permissao }: RequirePermissionProps) {
  const hasPermission = usePermission(permissao)

  if (!hasPermission) {
    return <Navigate to="/unauthorized" replace />
  }

  return <Outlet />
}
