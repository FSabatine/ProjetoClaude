import axios from 'axios'

type TokenGetter = () => string | null
type UnauthorizedHandler = () => void

let getToken: TokenGetter = () => null
let onUnauthorized: UnauthorizedHandler = () => {}

/**
 * Wires the shared axios instance to the in-memory auth state.
 * Called once from AuthContext so the client never touches localStorage/sessionStorage.
 */
export function configureApiClient(tokenGetter: TokenGetter, unauthorizedHandler: UnauthorizedHandler): void {
  getToken = tokenGetter
  onUnauthorized = unauthorizedHandler
}

const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
})

apiClient.interceptors.request.use((config) => {
  const token = getToken()
  if (token) {
    config.headers.set('Authorization', `Bearer ${token}`)
  }
  return config
})

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (axios.isAxiosError(error) && error.response?.status === 401) {
      onUnauthorized()
    }
    return Promise.reject(error)
  },
)

export default apiClient
