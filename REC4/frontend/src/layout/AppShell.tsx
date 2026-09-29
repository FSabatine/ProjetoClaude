import { useState } from 'react'
import { Outlet } from 'react-router-dom'
import { BreadcrumbProvider } from './BreadcrumbContext'
import Breadcrumb from './Breadcrumb'
import Sidebar from './Sidebar'
import TopBar from './TopBar'

export default function AppShell() {
  const [sidebarOpen, setSidebarOpen] = useState(true)

  return (
    <BreadcrumbProvider>
      <div className="app-shell">
        <Sidebar open={sidebarOpen} />
        <div className="app-main">
          <TopBar onToggleSidebar={() => setSidebarOpen((open) => !open)} />
          <Breadcrumb />
          <main className="app-content">
            <Outlet />
          </main>
        </div>
      </div>
    </BreadcrumbProvider>
  )
}
