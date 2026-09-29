import { useEffect } from 'react'
import { useBreadcrumbContext, type BreadcrumbItem } from './BreadcrumbContext'

/**
 * Lets a page declare its breadcrumb trail (rendered by AppShell/Breadcrumb).
 * The last item also becomes the bold page title shown in the top bar.
 */
export function useBreadcrumb(items: BreadcrumbItem[]): void {
  const { setItems } = useBreadcrumbContext()
  const key = JSON.stringify(items)

  useEffect(() => {
    setItems(items)
    return () => setItems([])
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key])
}
