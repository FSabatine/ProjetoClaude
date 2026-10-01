import { PERMISSIONS } from '../../../auth/permissions';
import type { HelpArticle, HelpCategory } from '../types';

export const CATEGORY: HelpCategory = {
  id: 'vehicles',
  label: 'Veículos',
  description: 'Cadastro, situação, hub e histórico dos veículos da frota.',
  requiredPermission: PERMISSIONS.vehicles.view,
};

export const ARTICLES: HelpArticle[] = [
  {
    id: 'vehicles-register',
    categoryId: 'vehicles',
    title: 'Como cadastrar um veículo',
    summary: 'Placa, RENAVAM, chassi, fabricante/modelo e o hodômetro inicial são obrigatórios.',
    steps: [
      'Acesse "Veículos" e clique em "Novo veículo".',
      'Informe a placa (padrão antigo ou Mercosul), RENAVAM e chassi — os três precisam ser únicos na empresa.',
      'Preencha fabricante, modelo, ano de fabricação e ano do modelo.',
      'Informe o hodômetro inicial (0 para veículo novo) — essa é a única vez que ele é digitado diretamente.',
      'Salve.',
    ],
    notes: ['A placa não pode se repetir nem entre veículos e implementos — no Brasil, a placa identifica um único veículo registrado.'],
    requiredPermission: PERMISSIONS.vehicles.create,
    relatedArticleIds: ['mileage-update'],
  },
  {
    id: 'vehicles-status',
    categoryId: 'vehicles',
    title: 'Condição do veículo e situação operacional — qual a diferença',
    summary: 'A condição é o que você define (Disponível, Em manutenção…); "Alocado" é calculado automaticamente a partir de quem está dirigindo.',
    example:
      'Disponível + com motorista alocado → aparece como "Alocado"\n' +
      'Disponível + sem motorista → aparece como "Disponível"\n' +
      'Em viagem, Em manutenção, Indisponível ou Inativo → aparece exatamente assim, independente do motorista',
    whyItMatters:
      '"Alocado" nunca é um valor que você escolhe — é sempre a combinação entre a condição do veículo e a alocação ' +
      'ativa. Por isso um veículo alocado que entra em manutenção some da lista de "Alocados" e volta sozinho quando a manutenção termina.',
    notes: [
      'Condições possíveis: Disponível, Em viagem, Em manutenção, Indisponível (fora de uso por outro motivo) e Inativo.',
      'Um veículo com motorista alocado não pode ser inativado — encerre a alocação antes.',
    ],
    relatedArticleIds: ['assignments-assign', 'maintenance-workorders'],
  },
  {
    id: 'vehicles-hub',
    categoryId: 'vehicles',
    title: 'A tela de detalhe do veículo (hub)',
    summary: 'Ao clicar num veículo, você vê abas: Visão geral, Motorista, Quilometragem, Documentos, Checklists, Ocorrências, Manutenção e Histórico.',
    steps: [
      'Clique num veículo na lista para abrir o hub.',
      'As abas que você vê dependem das suas permissões — cada módulo só aparece se você tiver acesso a ele.',
      'O botão "Editar" leva ao formulário de cadastro; o hub em si é sempre a tela de consulta.',
    ],
    relatedArticleIds: ['vehicles-history'],
  },
  {
    id: 'vehicles-history',
    categoryId: 'vehicles',
    title: 'Histórico do veículo',
    summary: 'A aba "Histórico" reúne alocações, leituras de hodômetro, documentos, checklists, ocorrências e manutenção numa única linha do tempo.',
    whyItMatters: 'É o primeiro lugar a olhar quando alguém pergunta "o que aconteceu com esse veículo ultimamente".',
  },
];
