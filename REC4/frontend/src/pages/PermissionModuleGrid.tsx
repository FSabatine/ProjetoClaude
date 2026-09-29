import type { PermissaoDto } from '../types/usuarios'

interface PermissionModuleGridProps {
  allPermissoes: PermissaoDto[]
  selectedIds: Set<number>
  onToggle: (permissaoId: number, checked: boolean) => void
}

interface ModuleRow {
  module: string
  actions: { permissao: PermissaoDto; label: string }[]
}

// Rótulos em portugues para as acoes conhecidas do catalogo (REC4.Domain/Seed/PermissoesPadrao.cs).
// Uma acao sem rotulo mapeado (ex.: futura) cai no fallback e mostra a propria palavra da chave.
const ACTION_LABELS: Record<string, string> = {
  Visualizar: 'Visualização',
  Criar: 'Cadastrar',
  Editar: 'Editar',
  Emitir: 'Emitir',
}

function groupByModule(permissoes: PermissaoDto[]): ModuleRow[] {
  const byModule = new Map<string, ModuleRow>()
  for (const permissao of permissoes) {
    const [module, action] = permissao.chave.split('.')
    if (!byModule.has(module)) byModule.set(module, { module, actions: [] })
    byModule.get(module)!.actions.push({ permissao, label: ACTION_LABELS[action] ?? action })
  }
  return Array.from(byModule.values()).sort((a, b) => a.module.localeCompare(b.module))
}

export default function PermissionModuleGrid({ allPermissoes, selectedIds, onToggle }: PermissionModuleGridProps) {
  const rows = groupByModule(allPermissoes)

  return (
    <div className="module-grid">
      {rows.map((row) => (
        <div key={row.module} className="module-row">
          <span className="module-name">{row.module}</span>
          <div className="module-actions">
            {row.actions.map(({ permissao, label }) => (
              <label key={permissao.id} className="toggle-field" title={permissao.descricao ?? undefined}>
                <span className="toggle-label">{label}</span>
                <span className="toggle-switch">
                  <input
                    type="checkbox"
                    checked={selectedIds.has(permissao.id)}
                    onChange={(event) => onToggle(permissao.id, event.target.checked)}
                  />
                  <span className="toggle-track" />
                </span>
              </label>
            ))}
          </div>
        </div>
      ))}
    </div>
  )
}
