import { Navigate, Route, Routes } from 'react-router-dom'
import RequireAuth from './auth/RequireAuth'
import RequirePermission from './auth/RequirePermission'
import AppShell from './layout/AppShell'
import EscritorioFormPage from './pages/EscritorioFormPage'
import EscritoriosListPage from './pages/EscritoriosListPage'
import GrupoFormPage from './pages/GrupoFormPage'
import GruposListPage from './pages/GruposListPage'
import LoginPage from './pages/LoginPage'
import PerfilPage from './pages/PerfilPage'
import PessoaFormPage from './pages/PessoaFormPage'
import PessoasListPage from './pages/PessoasListPage'
import UnauthorizedPage from './pages/UnauthorizedPage'
import UsuarioFormPage from './pages/UsuarioFormPage'
import UsuariosListPage from './pages/UsuariosListPage'

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/unauthorized" element={<UnauthorizedPage />} />

      <Route element={<RequireAuth />}>
        <Route element={<AppShell />}>
          <Route path="/" element={<Navigate to="/pessoas" replace />} />
          <Route path="/pessoas" element={<PessoasListPage />} />
          <Route path="/pessoas/novo" element={<PessoaFormPage />} />
          <Route path="/pessoas/:id/editar" element={<PessoaFormPage />} />

          <Route path="/perfil" element={<PerfilPage />} />

          <Route element={<RequirePermission permissao="Escritorio.Visualizar" />}>
            <Route path="/escritorios" element={<EscritoriosListPage />} />
            <Route path="/escritorios/novo" element={<EscritorioFormPage />} />
            <Route path="/escritorios/:id/editar" element={<EscritorioFormPage />} />
          </Route>

          <Route element={<RequirePermission permissao="Usuario.Visualizar" />}>
            <Route path="/usuarios" element={<UsuariosListPage />} />
            <Route path="/usuarios/novo" element={<UsuarioFormPage />} />
            <Route path="/usuarios/:id/editar" element={<UsuarioFormPage />} />
            <Route path="/grupos" element={<GruposListPage />} />
            <Route path="/grupos/novo" element={<GrupoFormPage />} />
            <Route path="/grupos/:id/editar" element={<GrupoFormPage />} />
          </Route>
        </Route>
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
