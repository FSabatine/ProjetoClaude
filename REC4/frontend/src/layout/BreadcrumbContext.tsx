import { createContext, useContext, useState, type ReactNode } from 'react'

export interface BreadcrumbItem {
  label: string
  to?: string
}

interface BreadcrumbContextValue {
  items: BreadcrumbItem[]
  setItems: (items: BreadcrumbItem[]) => void
}

const BreadcrumbContext = createContext<BreadcrumbContextValue | undefined>(undefined)

export function BreadcrumbProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<BreadcrumbItem[]>([])
  return <BreadcrumbContext.Provider value={{ items, setItems }}>{children}</BreadcrumbContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useBreadcrumbContext(): BreadcrumbContextValue {
  const ctx = useContext(BreadcrumbContext)
  if (!ctx) {
    throw new Error('useBreadcrumbContext must be used within a BreadcrumbProvider')
  }
  return ctx
}
