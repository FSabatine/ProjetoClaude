import type { HelpArticle, HelpCategory } from '../types';

export const CATEGORY: HelpCategory = {
  id: 'glossary',
  label: 'Glossário',
  description: 'O significado dos termos usados no sistema.',
};

const term = (id: string, title: string, summary: string, keywords: string[] = []): HelpArticle => ({
  id: `glossary-${id}`, categoryId: 'glossary', title, summary, keywords,
});

export const ARTICLES: HelpArticle[] = [
  term('alert', 'Alerta', 'Algo que precisa de atenção, encontrado automaticamente por uma regra de automação. Tem gravidade, explicação, base numérica, sugestão e situação (novo, lido, em andamento, resolvido, descartado).'),
  term('automation-rule', 'Regra de automação', 'Configuração "quando acontecer X, então criar alerta e/ou avisar": define o que o sistema observa, o limite e quem é avisado.', ['regra', 'gatilho']),
  term('attention', 'Requer atenção', 'Bloco do painel com tudo o que precisa de alguém agora: grupos de alertas e filas de trabalho (revisões, aprovações).'),
  term('cost-per-km', 'Custo por km', 'Custo total do período dividido pelos km rodados no período, medidos pelo hodômetro. Só aparece com quilometragem confiável.', ['custo km']),
  term('tco', 'TCO (custo total de propriedade)', 'Valor de aquisição do veículo mais tudo o que ele custou desde então. É uma análise de gestão, não um cálculo contábil.'),
  term('consumption', 'Consumo (km/l)', 'Quilômetros rodados por litro, medidos de tanque cheio a tanque cheio. Quanto maior, melhor.', ['km por litro', 'média de consumo']),
  term('fleet-average', 'Média da frota / média do tipo', 'Média do indicador entre os veículos ativos (ou só os do mesmo tipo) no mesmo período, considerando apenas quem tem o dado. Serve de comparação interna.', ['benchmark']),
  term('health', 'Saúde operacional', 'Nota de 0 a 100 que resume manutenção, combustível, pneus, custos, documentos, ocorrências e hodômetro do veículo. É um resumo prático, não uma medida científica.'),
  term('insight', 'Destaque', 'Variação relevante encontrada pelo sistema comparando os últimos 30 dias com os 30 anteriores (custos, consumo, tempo parado…).', ['tendência']),
  term('recurring', 'Problema recorrente', 'O mesmo serviço corretivo feito duas ou mais vezes no mesmo veículo no período.'),
  term('downtime', 'Tempo parado', 'Tempo em que o veículo ficou indisponível por manutenção, informado nas ordens de serviço.', ['indisponibilidade']),
  term('partial', 'Totais parciais', 'Aviso de que um total não inclui todas as fontes de custo porque você não tem permissão para ver alguma delas.'),
  term('preventive', 'Manutenção preventiva e corretiva', 'Preventiva é feita por plano (km, horas ou data) para evitar falhas; corretiva é feita depois que o problema aparece.', ['corretiva']),
  term('tread', 'Sulco', 'Profundidade da banda de rodagem do pneu, em milímetros. A empresa configura o limite de aviso e o mínimo para troca.'),
  term('tracker', 'Rastreador', 'Aparelho de GPS instalado no veículo que envia a posição para o sistema usando uma chave própria.', ['gps']),
  term('assistant', 'Assistente da frota', 'Recurso que responde perguntas sobre a frota usando os cálculos do sistema; a IA, quando ligada, só redige a explicação.', ['ia']),
  term('timeline', 'Histórico (linha do tempo)', 'Lista, em ordem, de tudo o que aconteceu com um veículo, motorista ou pneu em todos os módulos, respeitando as suas permissões.'),
];
