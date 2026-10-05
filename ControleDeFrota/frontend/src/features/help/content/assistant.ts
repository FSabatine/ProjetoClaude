import { PERMISSIONS } from '../../../auth/permissions';
import type { HelpArticle, HelpCategory } from '../types';

export const CATEGORY: HelpCategory = {
  id: 'assistant',
  label: 'Assistente da frota (IA)',
  description: 'Como perguntar, o que o assistente consegue analisar, de onde vêm os números, limitações e privacidade.',
  requiredPermission: PERMISSIONS.assistant.use,
};

export const ARTICLES: HelpArticle[] = [
  {
    id: 'assistant-overview',
    categoryId: 'assistant',
    title: 'Como usar o assistente da frota',
    summary: 'Clique no ícone de brilho (✦) no cabeçalho e pergunte em português, do seu jeito. O assistente responde com a conclusão, o motivo, as evidências (números) e uma sugestão do que verificar.',
    steps: [
      'Abra o assistente pelo ícone ✦ no cabeçalho — ou pelos botões "Analisar" (na página do veículo) e "Explicar os custos do mês" (no painel financeiro).',
      'Escolha uma pergunta sugerida ou escreva a sua.',
      'Leia a resposta: Resposta, Motivo, Evidências e Sugestão.',
      'Use os links "Confira em" para abrir as telas com os dados de origem.',
    ],
    example: 'Pergunta: Qual veículo está gerando mais custo?\nResposta: RDX-2B34. Nos últimos 90 dias, custo total de R$ 71.302,14, 93% acima da média da frota.\nMotivo: a maior parte do custo foi combustível.\nSugestão: abra a aba Financeiro do veículo e compare com os do mesmo tipo.',
    notes: [
      'Na página de um veículo, o assistente já sabe de qual veículo você está falando: basta perguntar "por que o custo aumentou?".',
      'Depois que uma resposta cita um veículo, as próximas perguntas da conversa continuam sobre ele.',
      'Também dá para citar a placa: "analise o ABC-1D23".',
    ],
    keywords: ['assistente', 'ia', 'inteligência artificial', 'perguntar', 'chat', 'claude'],
    relatedArticleIds: ['assistant-questions', 'assistant-how-it-works', 'assistant-limits'],
  },
  {
    id: 'assistant-questions',
    categoryId: 'assistant',
    title: 'O que posso perguntar',
    summary: 'Custos, consumo, manutenção, pneus, orçamento, alertas e análise de um veículo específico.',
    notes: [
      'Frota: "Qual veículo está mais caro?", "Quais veículos estão acima da média?", "Onde estamos gastando mais dinheiro?", "Qual é o custo por km?", "Qual veículo merece atenção?"',
      'Manutenção: "Quais veículos tiveram mais manutenções?", "Quais problemas estão se repetindo?", "Qual veículo apresenta maior custo de manutenção?"',
      'Combustível: "Quais veículos estão consumindo mais combustível?", "Existe algum consumo fora do padrão?", "Qual foi o gasto com combustível este mês?"',
      'Pneus: "Quais pneus estão próximos de substituição?", "Quais veículos possuem maior custo com pneus?", "Existe desgaste anormal?"',
      'Financeiro: "Quanto gastamos este mês?", "Estamos dentro do orçamento?", "Qual categoria aumentou mais?"',
      'Veículo: "Analise este veículo", "Por que o custo desse veículo aumentou?"',
      'O assistente não responde sobre assuntos fora da frota nem cria, altera ou exclui registros — ele só consulta e explica.',
    ],
    keywords: ['exemplos de perguntas', 'o que perguntar'],
    relatedArticleIds: ['assistant-overview'],
  },
  {
    id: 'assistant-how-it-works',
    categoryId: 'assistant',
    title: 'De onde vêm os números',
    summary: 'Todos os números são calculados pelo próprio sistema, com as mesmas regras das telas e dos relatórios. A IA não faz contas: ela só escolhe quais consultas fazer e escreve a explicação.',
    notes: [
      'Cada resposta mostra um selo: "Calculado pelo sistema" (resposta montada pelo sistema, sem IA) ou "Explicado por IA a partir dos dados do sistema".',
      'Quando a IA está ligada e não responde (falha, demora ou recusa), o sistema responde sozinho com os mesmos cálculos e avisa.',
      'Se a resposta da IA citar um número que não está nos dados consultados, aparece um aviso laranja pedindo para conferir.',
      'Os links "Confira em" levam às telas onde estão os registros que geraram a resposta.',
    ],
    keywords: ['cálculo', 'confiável', 'de onde vem', 'calculado pelo sistema'],
    relatedArticleIds: ['assistant-limits', 'assistant-privacy'],
  },
  {
    id: 'assistant-limits',
    categoryId: 'assistant',
    title: 'Limitações: quando o assistente não responde',
    summary: 'O assistente diz claramente quando não tem dado suficiente ou quando você não tem permissão — em vez de inventar uma resposta.',
    notes: [
      'Sem permissão: se você não pode ver valores financeiros, ele não responde sobre custos (e nunca mostra valores que você não veria nas telas).',
      'Sem dados: poucos abastecimentos com tanque cheio, hodômetro sem leitura antes do período ou nenhum orçamento cadastrado deixam algumas perguntas sem resposta.',
      'Pergunta fora do que ele conhece: ele mostra exemplos do que pode responder.',
      'As respostas são sugestões para apoiar sua análise. A IA pode errar na explicação — confira os números nas telas indicadas e decida com base no seu conhecimento da operação.',
      'O assistente não substitui decisões de negócio e não toma nenhuma ação sozinho.',
    ],
    keywords: ['não respondeu', 'sem dados', 'erro', 'limitação', 'pode errar'],
    relatedArticleIds: ['assistant-how-it-works', 'assistant-permissions'],
  },
  {
    id: 'assistant-permissions',
    categoryId: 'assistant',
    title: 'Assistente e permissões',
    summary: 'O assistente enxerga exatamente o que você enxerga: as mesmas permissões e a mesma empresa.',
    notes: [
      'Usar o assistente exige a permissão "Usar o assistente de análise da frota".',
      'Cada consulta que ele faz respeita as suas permissões: custos só com a permissão de valores; manutenção, combustível e pneus só se você acessa esses módulos.',
      'Dados de outras empresas nunca são consultados.',
      'Para evitar uso excessivo, há um limite de perguntas por minuto por usuário.',
    ],
    keywords: ['permissão', 'acesso', 'quem pode usar'],
    relatedArticleIds: ['assistant-limits'],
  },
  {
    id: 'assistant-privacy',
    categoryId: 'assistant',
    title: 'Privacidade: o que é enviado para a IA',
    summary: 'A IA (Claude, da Anthropic) é opcional e vem desligada. Quando o administrador a liga, só vão para ela a sua pergunta e os resultados das consultas — números já calculados que você tem permissão para ver.',
    notes: [
      'Com a IA desligada, nada sai do servidor da empresa: as respostas são montadas pelo próprio sistema.',
      'Com a IA ligada, são enviados: a pergunta, a tela de onde você perguntou e os resultados das consultas (placas, modelos, totais, médias e percentuais). Não são enviados senhas, documentos, arquivos, CPF ou dados que você não pode ver.',
      'Evite escrever dados pessoais na pergunta.',
    ],
    keywords: ['privacidade', 'lgpd', 'dados enviados', 'anthropic', 'claude'],
    relatedArticleIds: ['assistant-how-it-works'],
  },
];
