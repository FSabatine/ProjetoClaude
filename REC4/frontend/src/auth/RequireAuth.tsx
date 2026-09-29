import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from './useAuth'

/**
 * Route guard: redirects to /login when not authenticated, preserving the
 * intended destination so LoginPage can send the user back after signing in.
 */
export default function RequireAuth() {
  const { isAuthenticated } = useAuth()
  const location = useLocation()

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location }} />
  }

  return <Outlet />
}
