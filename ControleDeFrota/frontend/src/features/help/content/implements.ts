import { PERMISSIONS } from '../../../auth/permissions';
import type { HelpArticle, HelpCategory } from '../types';

export const CATEGORY: HelpCategory = {
  id: 'implements',
  label: 'Implementos',
  description: 'Reboques, semirreboques, tanques, baús, siders e dollies.',
  requiredPermission: PERMISSIONS.implements.view,
};

export const ARTICLES: HelpArticle[] = [
  {
    id: 'implements-overview',
    categoryId: 'implements',
    title: 'O que é um implemento',
    summary: 'Qualquer equipamento rebocado — reboque, semirreboque, tanque, baú, sider ou dolly.',
    whyItMatters: 'É cadastrado de forma parecida com um veículo (placa, RENAVAM, chassi próprios), porque no Brasil cada um desses equipamentos é um item registrado.',
  },
  {
    id: 'implements-register',
    categoryId: 'implements',
    title: 'Como cadastrar um implemento',
    summary: 'Placa, RENAVAM, chassi, tipo e capacidade (com a unidade certa: kg, litros ou m³).',
    steps: [
      'Acesse "Implementos" e clique em "Novo implemento".',
      'Escolha o tipo (reboque, semirreboque, tanque, baú, sider, dolly ou outro).',
      'Informe a capacidade e a unidade correspondente — um tanque é medido em litros, um baú em kg ou m³.',
      'Salve.',
    ],
    requiredPermission: PERMISSIONS.implements.create,
  },
  {
    id: 'implements-status',
    categoryId: 'implements',
    title: 'Situação do implemento',
    summary: 'Disponível, Em uso, Em manutenção ou Inativo.',
    notes: ['O vínculo formal entre um veículo e o implemento que ele está puxando ainda não existe nesta versão — é um item do roteiro futuro.'],
  },
];
