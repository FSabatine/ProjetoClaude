import { PERMISSIONS } from '../../../auth/permissions';
import type { HelpArticle, HelpCategory } from '../types';

export const CATEGORY: HelpCategory = {
  id: 'maintenance',
  label: 'Manutenção',
  description: 'Planos preventivos, solicitações, ordens de serviço e oficinas.',
  requiredPermission: PERMISSIONS.maintenance.view,
};

export const ARTICLES: HelpArticle[] = [
  {
    id: 'maintenance-overview',
    categoryId: 'maintenance',
    title: 'Manutenção preventiva, corretiva e inspeção',
    summary: 'Preventiva segue um plano (por km, tempo ou horas); corretiva atende um problema já identificado; inspeção é uma verificação pontual.',
    whyItMatters: 'O tipo da ordem de serviço é só uma classificação — o fluxo de aprovação e execução é o mesmo para os três.',
    relatedArticleIds: ['maintenance-plans', 'maintenance-requests'],
  },
  {
    id: 'maintenance-plans',
    categoryId: 'maintenance',
    title: 'Planos de manutenção preventiva',
    summary: 'Um plano define os serviços e a cada quanto cada um deve ser feito — por km, por mês ou por hora de motor.',
    example: 'Troca de óleo\nA cada 20.000 km ou 6 meses (o que vencer primeiro)\nCarência: 1.000 km',
    whyItMatters: 'Quando mais de um intervalo é configurado no mesmo item, o que vencer primeiro é o que dispara a manutenção.',
    steps: [
      'Acesse "Planos de manutenção" e clique em "Novo plano".',
      'Escolha se o plano vale para a empresa toda, para um tipo de veículo, ou para um veículo específico.',
      'Adicione os itens, cada um com pelo menos um intervalo (km, meses ou horas) e, se quiser, uma carência.',
      'Salve.',
    ],
    notes: [
      'Quando mais de um plano se aplica ao mesmo veículo, o plano daquele veículo específico vale mais que o do tipo, que vale mais que o padrão da empresa.',
      'A carência funciona nos dois sentidos: avisa um pouco antes do vencimento e tolera um pouco depois.',
    ],
    requiredPermission: PERMISSIONS.maintenance.manageplans,
    relatedArticleIds: ['maintenance-schedule'],
  },
  {
    id: 'maintenance-schedule',
    categoryId: 'maintenance',
    title: 'Como ler a agenda de manutenção de um veículo',
    summary: 'Na aba "Manutenção" do veículo, cada item do plano mostra quando foi feito pela última vez e quando vence de novo.',
    example: 'Veículo ABC-1234\nTroca de óleo\nÚltima: 119.000 km\nPróxima: 139.000 km\nHodômetro atual: 138.400 km → "Vencendo"',
    whyItMatters: 'Um veículo que nunca passou por aquele item de manutenção ainda aparece na agenda — o sistema usa a data e o hodômetro do cadastro como ponto de partida.',
    relatedArticleIds: ['maintenance-alerts'],
  },
  {
    id: 'maintenance-hourmeter',
    categoryId: 'maintenance',
    title: 'Horímetro: para que serve e como atualizar',
    summary: 'O horímetro mede as horas de uso do motor — usado em planos de manutenção baseados em horas, não em quilometragem.',
    whyItMatters: 'Assim como o hodômetro, o horímetro só muda através de uma leitura registrada (não dá mais para editar direto no cadastro do veículo), e uma leitura com um salto grande demais fica pendente de revisão antes de valer.',
    relatedArticleIds: ['mileage-update'],
  },
  {
    id: 'maintenance-requests',
    categoryId: 'maintenance',
    title: 'Solicitações de manutenção',
    summary: 'O primeiro passo antes de existir uma ordem de serviço: alguém pede, o gestor aprova ou rejeita.',
    example: 'Motorista relata barulho no freio\n→ Solicitação de manutenção (prioridade Alta)\n→ Gestor aprova\n→ Ordem de serviço aberta automaticamente',
    steps: [
      'Acesse "Solicitações de manutenção" e clique em "Solicitar manutenção" (ou use o botão numa ocorrência).',
      'Escolha o veículo, o tipo e a prioridade.',
      'Descreva o que precisa ser verificado.',
      'Envie — um gestor de manutenção vai aprovar ou rejeitar.',
    ],
    whyItMatters: 'Aprovar uma solicitação já abre a ordem de serviço na hora, pronta para ser agendada — não existe uma etapa extra entre aprovar e ter a ordem criada.',
    notes: ['Rejeitar uma solicitação exige escrever o motivo.'],
    requiredPermission: PERMISSIONS.maintenance.createrequest,
    relatedArticleIds: ['maintenance-workorders'],
  },
  {
    id: 'maintenance-workorders',
    categoryId: 'maintenance',
    title: 'O ciclo de vida de uma ordem de serviço',
    summary: 'Rascunho → Aprovada → Agendada → Em andamento (podendo pausar Aguardando peças) → Concluída.',
    example: 'OS-000123\nVeículo: ABC-1234\nTipo: Corretiva\nSituação: Em andamento',
    whyItMatters: 'A qualquer momento, só algumas transições fazem sentido — os botões de ação na tela da ordem mostram só o que é permitido a partir da situação atual.',
    notes: [
      'Cancelar ou rejeitar são saídas possíveis a partir de quase qualquer situação não concluída, mas sempre exigem o motivo.',
      'Concluir exige escrever a resolução, e todo item obrigatório da ordem precisa estar marcado como concluído ou dispensado.',
    ],
    requiredPermission: PERMISSIONS.maintenance.manageworkorders,
    relatedArticleIds: ['maintenance-workorders-execution'],
  },
  {
    id: 'maintenance-workorders-execution',
    categoryId: 'maintenance',
    title: 'Como registrar itens, peças e mão de obra numa ordem de serviço',
    summary: 'Durante a execução, marque cada item como concluído ou dispensado, e adicione as peças e a mão de obra usadas.',
    steps: [
      'Abra a ordem de serviço e inicie a execução.',
      'Para cada item da ordem, marque "Concluir" quando terminar (itens obrigatórios não podem ser só dispensados).',
      'Em "Peças", adicione cada peça usada com quantidade e custo unitário.',
      'Em "Mão de obra", adicione o técnico, as horas trabalhadas e o valor da hora.',
      'Quando tudo estiver pronto, conclua a ordem informando a resolução.',
    ],
    whyItMatters: 'O veículo fica com a situação "Em manutenção" assim que a ordem é iniciada, e só volta a "Disponível" quando a última ordem de serviço ativa dele é concluída ou cancelada — a não ser que alguém tenha mudado a situação dele manualmente nesse meio tempo, nesse caso o sistema não sobrescreve.',
    notes: ['O custo total só aparece para quem tem a permissão de visualizar custos — quem está executando vê o que precisa fazer, mesmo sem ver o valor.'],
    requiredPermission: PERMISSIONS.maintenance.manageworkorders,
  },
  {
    id: 'maintenance-workshops',
    categoryId: 'maintenance',
    title: 'Oficinas',
    summary: 'Cadastre as oficinas internas e externas que executam as ordens de serviço.',
    steps: ['Acesse "Oficinas" e clique em "Nova oficina".', 'Informe nome, contato e especialidades.', 'Salve.'],
    requiredPermission: PERMISSIONS.maintenance.manageworkshops,
  },
  {
    id: 'maintenance-alerts',
    categoryId: 'maintenance',
    title: 'O que significam os alertas de manutenção',
    summary: 'Vencendo, Vence agora e Atrasada — cada um indica o quão urgente é agir.',
    example:
      'Vencendo\nFaltam poucos km, dias ou horas para o próximo vencimento.\n\n' +
      'Vence agora\nO vencimento chegou, ainda dentro da carência.\n\n' +
      'Atrasada\nPassou do vencimento e da carência.',
    whyItMatters: 'Se um item aparece como Atrasado, abra a aba de manutenção do veículo e crie uma solicitação (ou uma ordem de serviço direta) para resolver.',
    relatedArticleIds: ['maintenance-schedule', 'maintenance-requests'],
  },
];
