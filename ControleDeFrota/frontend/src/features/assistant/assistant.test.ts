import { describe, expect, it } from 'vitest';
import { contextFromPath, contextualQuestions } from './assistant';

describe('assistant context', () => {
  it('focuses on the vehicle open on screen', () => {
    const ctx = contextFromPath('/veiculos/1c303b32-0ab9-4fca-feb3-08df1e369dd5');
    expect(ctx).toEqual({ vehicleId: '1c303b32-0ab9-4fca-feb3-08df1e369dd5', page: 'vehicle' });
    expect(contextualQuestions(ctx)[0]).toBe('Analise este veículo');
  });

  it('does not treat the comparison page as a vehicle', () => {
    expect(contextFromPath('/veiculos/comparar').vehicleId).toBeNull();
  });

  it('maps module screens to their area', () => {
    expect(contextFromPath('/financeiro/despesas').page).toBe('finance');
    expect(contextFromPath('/ordens-servico/abc').page).toBe('maintenance');
    expect(contextFromPath('/abastecimentos').page).toBe('fuel');
    expect(contextFromPath('/pneus/painel').page).toBe('tires');
    expect(contextFromPath('/').page).toBe('dashboard');
  });
});
