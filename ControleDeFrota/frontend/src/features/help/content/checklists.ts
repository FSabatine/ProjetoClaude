import { PERMISSIONS } from '../../../auth/permissions';
import type { HelpArticle, HelpCategory } from '../types';

export const CATEGORY: HelpCategory = {
  id: 'checklists',
  label: 'Checklists',
  description: 'Inspeções do veículo, com itens configuráveis e respostas no celular.',
  requiredPermission: PERMISSIONS.checklists.view,
};

export const ARTICLES: HelpArticle[] = [
  {
    id: 'checklists-overview',
    categoryId: 'checklists',
    title: 'O que são os checklists de veículo',
    summary: 'Uma inspeção rápida (ex.: antes de uma viagem) com itens configuráveis como pneus, luzes e retrovisores.',
    whyItMatters: 'Um item reprovado vira automaticamente uma ocorrência — ninguém precisa digitar o mesmo problema duas vezes.',
    relatedArticleIds: ['occurrences-overview'],
  },
  {
    id: 'checklists-run',
    categoryId: 'checklists',
    title: 'Como realizar um checklist',
    summary: 'Escolha o veículo e o modelo de checklist, responda cada item e envie.',
    example: 'Antes de iniciar a viagem:\n✓ Pneus\n✓ Luzes\n✓ Retrovisores\n✗ Problema no freio\n→ Gera uma ocorrência automaticamente',
    steps: [
      'Acesse "Checklists" e clique em "Realizar checklist".',
      'Escolha o veículo (o motorista padrão é o alocado a ele) e o modelo de checklist.',
      'Responda cada item: Conforme, Não conforme ou Não se aplica (ou um número/texto, dependendo do item).',
      'Se o item exigir foto em caso de reprovação, tire a foto pela câmera do celular.',
      'Informe o hodômetro, se o checklist pedir.',
      'Envie — todo item obrigatório precisa estar respondido.',
    ],
    requiredPermission: PERMISSIONS.checklists.execute,
  },
  {
    id: 'checklists-after-submit',
    categoryId: 'checklists',
    title: 'O que acontece depois de enviar um checklist',
    summary: 'Cada item "Não conforme" vira uma ocorrência automaticamente, já com as fotos anexadas.',
    whyItMatters: 'Um checklist enviado é imutável — ele fica congelado exatamente como foi respondido, mesmo que o modelo mude depois.',
    relatedArticleIds: ['occurrences-overview'],
  },
  {
    id: 'checklists-pending',
    categoryId: 'checklists',
    title: 'Checklists pendentes',
    summary: 'A lista de pendentes mostra os veículos em operação que ainda não fizeram o checklist diário ou semanal no período atual.',
    notes: ['Um veículo "de pátio", sem motorista alocado, não entra na lista de pendentes.'],
  },
];
