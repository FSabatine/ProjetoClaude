import { describe, expect, it } from 'vitest';
import { resolveContextualCategory } from './context';

describe('resolveContextualCategory', () => {
  it('prioritizes the active tab of the vehicle hub over the vehicles section itself', () => {
    expect(resolveContextualCategory('/veiculos/123', '?aba=manutencao')).toBe('maintenance');
    expect(resolveContextualCategory('/veiculos/123', '?aba=documentos')).toBe('documents');
    expect(resolveContextualCategory('/veiculos/123', '')).toBe('vehicles');
  });

  it('prioritizes the active tab of the driver hub over the drivers section itself', () => {
    expect(resolveContextualCategory('/motoristas/123', '?aba=veiculos')).toBe('assignments');
    expect(resolveContextualCategory('/motoristas/123', '')).toBe('drivers');
  });

  it('resolves maintenance routes', () => {
    expect(resolveContextualCategory('/ordens-servico/1', '')).toBe('maintenance');
    expect(resolveContextualCategory('/solicitacoes-manutencao', '')).toBe('maintenance');
    expect(resolveContextualCategory('/oficinas', '')).toBe('maintenance');
  });

  it('resolves fuel routes and the fuel tab of the vehicle hub', () => {
    expect(resolveContextualCategory('/veiculos/123', '?aba=combustivel')).toBe('fuel');
    expect(resolveContextualCategory('/abastecimentos/novo', '')).toBe('fuel');
    expect(resolveContextualCategory('/combustivel/relatorios', '')).toBe('fuel');
    expect(resolveContextualCategory('/postos/1', '')).toBe('fuel');
    expect(resolveContextualCategory('/configuracoes/combustivel', '')).toBe('fuel');
  });

  it('resolves the dashboard route exactly, not by prefix', () => {
    expect(resolveContextualCategory('/', '')).toBe('dashboard');
    expect(resolveContextualCategory('/veiculos', '')).not.toBe('dashboard');
  });

  it('returns null for a route with no specific category', () => {
    expect(resolveContextualCategory('/alguma-rota-desconhecida', '')).toBeNull();
  });
});
