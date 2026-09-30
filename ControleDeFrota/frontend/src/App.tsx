import { lazy, type ComponentType } from 'react';
import { createBrowserRouter, Outlet, RouterProvider } from 'react-router-dom';
import { AuthProvider } from './auth/AuthContext';
import { RequireAuth, RequirePermission } from './auth/guards';
import { PERMISSIONS } from './auth/permissions';
import { AppLayout } from './components/AppLayout';
import { LoginPage } from './features/auth/LoginPage';
import { NotFoundPage, UnexpectedErrorPage } from './pages/StatusPages';

// Pages are loaded on demand (code splitting): the first screen downloads only what it needs.
const page = <K extends string>(loader: () => Promise<Record<K, ComponentType>>, name: K) =>
  lazy(() => loader().then((m) => ({ default: m[name] })));

const DashboardPage = page(() => import('./features/dashboard/DashboardPage'), 'DashboardPage');
const VehicleListPage = page(() => import('./features/vehicles/VehicleListPage'), 'VehicleListPage');
const VehicleFormPage = page(() => import('./features/vehicles/VehicleFormPage'), 'VehicleFormPage');
const ImplementListPage = page(() => import('./features/implements/ImplementListPage'), 'ImplementListPage');
const ImplementFormPage = page(() => import('./features/implements/ImplementFormPage'), 'ImplementFormPage');
const DriverListPage = page(() => import('./features/drivers/DriverListPage'), 'DriverListPage');
const DriverFormPage = page(() => import('./features/drivers/DriverFormPage'), 'DriverFormPage');
const UserListPage = page(() => import('./features/users/UserListPage'), 'UserListPage');
const UserFormPage = page(() => import('./features/users/UserFormPage'), 'UserFormPage');
const RolesPage = page(() => import('./features/roles/RolesPage'), 'RolesPage');
const CompanyListPage = page(() => import('./features/companies/CompanyListPage'), 'CompanyListPage');
const CompanyFormPage = page(() => import('./features/companies/CompanyFormPage'), 'CompanyFormPage');
const MyCompanyPage = page(() => import('./features/companies/CompanyFormPage'), 'MyCompanyPage');
const VehicleDetailPage = page(() => import('./features/vehicles/VehicleDetailPage'), 'VehicleDetailPage');
const DriverDetailPage = page(() => import('./features/drivers/DriverDetailPage'), 'DriverDetailPage');
const DocumentListPage = page(() => import('./features/documents/DocumentListPage'), 'DocumentListPage');
const DocumentTypesPage = page(() => import('./features/documents/DocumentTypesPage'), 'DocumentTypesPage');
const OccurrenceListPage = page(() => import('./features/occurrences/OccurrenceListPage'), 'OccurrenceListPage');
const OccurrenceDetailPage = page(() => import('./features/occurrences/OccurrenceDetailPage'), 'OccurrenceDetailPage');
const ChecklistListPage = page(() => import('./features/checklists/ChecklistListPage'), 'ChecklistListPage');
const ChecklistRunPage = page(() => import('./features/checklists/ChecklistRunPage'), 'ChecklistRunPage');
const ChecklistDetailPage = page(() => import('./features/checklists/ChecklistDetailPage'), 'ChecklistDetailPage');
const ChecklistTemplateListPage = page(() => import('./features/checklists/ChecklistTemplatePages'), 'ChecklistTemplateListPage');
const ChecklistTemplateFormPage = page(() => import('./features/checklists/ChecklistTemplatePages'), 'ChecklistTemplateFormPage');
const P = PERMISSIONS;

// Routes are in Portuguese (what users see in the address bar); code stays in English.
const router = createBrowserRouter([
  {
    // AuthProvider inside the router so auth code can use navigation hooks.
    element: (
      <AuthProvider>
        <Outlet />
      </AuthProvider>
    ),
    // A rendering bug must never show the router's developer page to a user.
    errorElement: <UnexpectedErrorPage />,
    children: [
      { path: '/login', element: <LoginPage /> },
      {
        element: <RequireAuth />,
        children: [
          {
            element: <AppLayout />,
            children: [
              { index: true, element: <RequirePermission permission={P.dashboard.view}><DashboardPage /></RequirePermission> },
              { path: 'veiculos', element: <RequirePermission permission={P.vehicles.view}><VehicleListPage /></RequirePermission> },
              { path: 'veiculos/novo', element: <RequirePermission permission={P.vehicles.create}><VehicleFormPage /></RequirePermission> },
              { path: 'veiculos/:id', element: <RequirePermission permission={P.vehicles.view}><VehicleDetailPage /></RequirePermission> },
              { path: 'veiculos/:id/editar', element: <RequirePermission permission={P.vehicles.view}><VehicleFormPage /></RequirePermission> },
              { path: 'implementos', element: <RequirePermission permission={P.implements.view}><ImplementListPage /></RequirePermission> },
              { path: 'implementos/novo', element: <RequirePermission permission={P.implements.create}><ImplementFormPage /></RequirePermission> },
              { path: 'implementos/:id', element: <RequirePermission permission={P.implements.view}><ImplementFormPage /></RequirePermission> },
              { path: 'motoristas', element: <RequirePermission permission={P.drivers.view}><DriverListPage /></RequirePermission> },
              { path: 'motoristas/novo', element: <RequirePermission permission={P.drivers.create}><DriverFormPage /></RequirePermission> },
              { path: 'motoristas/:id', element: <RequirePermission permission={P.drivers.view}><DriverDetailPage /></RequirePermission> },
              { path: 'motoristas/:id/editar', element: <RequirePermission permission={P.drivers.view}><DriverFormPage /></RequirePermission> },
              { path: 'documentos', element: <RequirePermission permission={P.documents.view}><DocumentListPage /></RequirePermission> },
              { path: 'ocorrencias', element: <RequirePermission permission={P.occurrences.view}><OccurrenceListPage /></RequirePermission> },
              { path: 'ocorrencias/:id', element: <RequirePermission permission={P.occurrences.view}><OccurrenceDetailPage /></RequirePermission> },
              { path: 'checklists', element: <RequirePermission permission={P.checklists.view}><ChecklistListPage /></RequirePermission> },
              { path: 'checklists/realizar', element: <RequirePermission permission={P.checklists.execute}><ChecklistRunPage /></RequirePermission> },
              { path: 'checklists/:id', element: <RequirePermission permission={P.checklists.view}><ChecklistDetailPage /></RequirePermission> },
              { path: 'configuracoes/checklists', element: <RequirePermission permission={P.operations.configure}><ChecklistTemplateListPage /></RequirePermission> },
              { path: 'configuracoes/checklists/novo', element: <RequirePermission permission={P.operations.configure}><ChecklistTemplateFormPage /></RequirePermission> },
              { path: 'configuracoes/checklists/:id', element: <RequirePermission permission={P.operations.configure}><ChecklistTemplateFormPage /></RequirePermission> },
              { path: 'configuracoes/tipos-de-documento', element: <RequirePermission permission={P.operations.configure}><DocumentTypesPage /></RequirePermission> },
              { path: 'usuarios', element: <RequirePermission permission={P.users.view}><UserListPage /></RequirePermission> },
              { path: 'usuarios/novo', element: <RequirePermission permission={P.users.manage}><UserFormPage /></RequirePermission> },
              { path: 'usuarios/:id', element: <RequirePermission permission={P.users.view}><UserFormPage /></RequirePermission> },
              { path: 'papeis', element: <RequirePermission permission={P.roles.view}><RolesPage /></RequirePermission> },
              { path: 'minha-empresa', element: <RequirePermission permission={P.companies.view}><MyCompanyPage /></RequirePermission> },
              { path: 'empresas', element: <RequirePermission permission={P.companies.manage}><CompanyListPage /></RequirePermission> },
              { path: 'empresas/nova', element: <RequirePermission permission={P.companies.manage}><CompanyFormPage /></RequirePermission> },
              { path: 'empresas/:id', element: <RequirePermission permission={P.companies.manage}><CompanyFormPage /></RequirePermission> },
              { path: '*', element: <NotFoundPage /> },
            ],
          },
        ],
      },
    ],
  },
]);

export function App() {
  return <RouterProvider router={router} />;
}
