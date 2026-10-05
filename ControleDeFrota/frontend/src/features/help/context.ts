/**
 * Ajuda contextual (seção 24 do pedido): qual categoria priorizar na tela inicial da Central de Ajuda,
 * a partir da rota atual. Regras em ordem — a primeira que casar vence, então regras mais específicas
 * (aba de um hub) vêm antes das genéricas (a seção inteira).
 */
const RULES: { test: (pathname: string, aba: string | null) => boolean; categoryId: string }[] = [
  // Hub do veículo: a aba ativa manda mais que a seção "Veículos" em si.
  { test: (p, aba) => p.startsWith('/veiculos/') && aba === 'combustivel', categoryId: 'fuel' },
  { test: (p, aba) => p.startsWith('/veiculos/') && aba === 'pneus', categoryId: 'tires' },
  { test: (p, aba) => p.startsWith('/veiculos/') && aba === 'financeiro', categoryId: 'financial' },
  { test: (p, aba) => p.startsWith('/veiculos/') && aba === 'manutencao', categoryId: 'maintenance' },
  { test: (p, aba) => p.startsWith('/veiculos/') && aba === 'documentos', categoryId: 'documents' },
  { test: (p, aba) => p.startsWith('/veiculos/') && aba === 'checklists', categoryId: 'checklists' },
  { test: (p, aba) => p.startsWith('/veiculos/') && aba === 'ocorrencias', categoryId: 'occurrences' },
  { test: (p, aba) => p.startsWith('/veiculos/') && aba === 'quilometragem', categoryId: 'mileage' },
  { test: (p, aba) => p.startsWith('/veiculos/') && aba === 'motorista', categoryId: 'assignments' },
  { test: (p) => p.startsWith('/veiculos'), categoryId: 'vehicles' },

  { test: (p, aba) => p.startsWith('/motoristas/') && aba === 'documentos', categoryId: 'documents' },
  { test: (p, aba) => p.startsWith('/motoristas/') && aba === 'checklists', categoryId: 'checklists' },
  { test: (p, aba) => p.startsWith('/motoristas/') && aba === 'ocorrencias', categoryId: 'occurrences' },
  { test: (p, aba) => p.startsWith('/motoristas/') && aba === 'veiculos', categoryId: 'assignments' },
  { test: (p) => p.startsWith('/motoristas'), categoryId: 'drivers' },

  { test: (p) => p.startsWith('/implementos'), categoryId: 'implements' },
  { test: (p) => p.startsWith('/documentos') || p.startsWith('/configuracoes/tipos-de-documento'), categoryId: 'documents' },
  { test: (p) => p.startsWith('/checklists') || p.startsWith('/configuracoes/checklists'), categoryId: 'checklists' },
  { test: (p) => p.startsWith('/ocorrencias'), categoryId: 'occurrences' },

  { test: (p) => p.startsWith('/oficinas') || p.startsWith('/planos-manutencao') ||
      p.startsWith('/solicitacoes-manutencao') || p.startsWith('/ordens-servico'), categoryId: 'maintenance' },

  { test: (p) => p.startsWith('/combustivel') || p.startsWith('/abastecimentos') || p.startsWith('/postos') ||
      p.startsWith('/configuracoes/combustivel'), categoryId: 'fuel' },

  { test: (p) => p.startsWith('/pneus') || p.startsWith('/configuracoes/pneus'), categoryId: 'tires' },

  { test: (p) => p.startsWith('/financeiro'), categoryId: 'financial' },

  { test: (p) => p.startsWith('/alertas') || p.startsWith('/configuracoes/automacoes'), categoryId: 'alerts' },

  { test: (p) => p.startsWith('/usuarios'), categoryId: 'users' },
  { test: (p) => p.startsWith('/papeis'), categoryId: 'rolesPermissions' },
  { test: (p) => p.startsWith('/minha-empresa') || p.startsWith('/empresas'), categoryId: 'companies' },
  { test: (p) => p === '/', categoryId: 'dashboard' },
];

/** Returns the category id most relevant to the current screen, or null when nothing specific applies. */
export function resolveContextualCategory(pathname: string, search: string): string | null {
  const aba = new URLSearchParams(search).get('aba');
  return RULES.find((rule) => rule.test(pathname, aba))?.categoryId ?? null;
}
