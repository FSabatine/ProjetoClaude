import { PERMISSIONS } from '../../../auth/permissions';
import type { HelpArticle, HelpCategory } from '../types';

export const CATEGORY: HelpCategory = {
  id: 'tracking',
  label: 'Rastreamento e integrações',
  description: 'Mapa da frota, localização e rota do veículo, cadastro de rastreadores (GPS) e a página de integrações.',
  requiredPermission: PERMISSIONS.tracking.view,
};

export const ARTICLES: HelpArticle[] = [
  {
    id: 'tracking-map',
    categoryId: 'tracking',
    title: 'Mapa da frota',
    summary: 'Em "Mapa da frota" aparecem os veículos com rastreador, na última posição enviada. A cor do ponto e o texto ao lado indicam se o rastreador está transmitindo.',
    steps: [
      'Abra "Mapa da frota" no menu.',
      'Clique em um veículo da lista (ou no ponto do mapa) para centralizar e ver placa, situação, motorista, velocidade, ignição e horário da última posição.',
      'Use os filtros Todos / Transmitindo / 24 h / Sem sinal.',
      'Clique na placa dentro do balão para abrir a aba Localização do veículo.',
    ],
    notes: [
      'Transmitindo = posição recebida nos últimos 10 minutos; 24 h = visto no último dia; Sem sinal = mais de 24 horas sem posição.',
      'O mapa atualiza sozinho a cada minuto.',
      'Mapa base © colaboradores do OpenStreetMap.',
    ],
    keywords: ['mapa', 'gps', 'onde está', 'localização', 'rastreamento'],
    relatedArticleIds: ['tracking-vehicle', 'tracking-devices'],
  },
  {
    id: 'tracking-vehicle',
    categoryId: 'tracking',
    title: 'Localização e rota do veículo',
    summary: 'Na página do veículo, a aba "Localização" mostra o caminho percorrido nas últimas 24 horas, 3 dias ou 7 dias, a última posição, a velocidade máxima e a ignição.',
    notes: [
      'A linha liga as posições enviadas pelo rastreador em ordem; não é o traçado exato da estrada.',
      'Períodos muito longos são simplificados para o mapa continuar rápido (o número total de posições continua aparecendo).',
      'O hodômetro informado pelo rastreador é guardado, mas não altera o hodômetro do veículo — as leituras continuam vindo dos registros do sistema.',
    ],
    keywords: ['rota', 'trajeto', 'percurso', 'histórico de posições'],
    relatedArticleIds: ['tracking-map'],
  },
  {
    id: 'tracking-devices',
    categoryId: 'tracking',
    title: 'Cadastrar um rastreador e instalar em um veículo',
    summary: 'Em "Configurações › Rastreadores": cadastre o provedor, depois o rastreador. O sistema gera uma chave, que deve ser configurada no aparelho ou na plataforma do provedor para enviar as posições.',
    steps: [
      'Cadastre o provedor (a empresa ou plataforma de rastreamento).',
      'Clique em "Novo rastreador", escolha o provedor e informe o identificador (IMEI ou número de série).',
      'Copie a chave mostrada — ela aparece uma única vez — e passe ao responsável pela configuração do aparelho, junto com o exemplo de envio.',
      'No menu ⋯ do rastreador, use "Instalar em um veículo".',
    ],
    notes: [
      'Perdeu a chave ou suspeita que vazou? Use "Gerar nova chave": a antiga para de funcionar na hora.',
      'Um veículo tem no máximo um rastreador por vez (instalar outro desinstala o anterior); o histórico de posições é mantido.',
      'Rastreador ou provedor inativo: as posições enviadas são recusadas.',
      'Exige a permissão "Configurar rastreadores e integrações".',
    ],
    keywords: ['rastreador', 'gps', 'imei', 'chave', 'instalar rastreador', 'api'],
    relatedArticleIds: ['tracking-map', 'integrations-overview'],
    requiredPermission: PERMISSIONS.tracking.manage,
  },
  {
    id: 'integrations-overview',
    categoryId: 'tracking',
    title: 'Página de integrações',
    summary: 'Em "Configurações › Integrações" você vê com o que o sistema se conecta: rastreamento, assistente com IA, notificações, armazenamento de arquivos, exportação e o que ainda está previsto.',
    notes: [
      'Em uso = funcionando; Disponível = pronto para usar, falta cadastrar; Não configurado = precisa de configuração no servidor; Previsto = ainda não existe.',
      'Integrações com ERP, cartão combustível, oficinas e consultas externas ainda não estão implementadas.',
    ],
    keywords: ['integração', 'erp', 'api', 'conectar'],
    relatedArticleIds: ['tracking-devices'],
    requiredPermission: PERMISSIONS.tracking.manage,
  },
];
