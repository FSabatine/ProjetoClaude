import { describe, expect, it } from 'vitest';
import { searchArticles } from './search';
import type { HelpArticle } from './types';

const article = (overrides: Partial<HelpArticle>): HelpArticle => ({
  id: 'x', categoryId: 'vehicles', title: 'Como cadastrar um veículo', summary: 'Placa, RENAVAM e chassi.', ...overrides,
});

describe('searchArticles', () => {
  const articles: HelpArticle[] = [
    article({ id: 'a', title: 'Como cadastrar um veículo', summary: 'Placa, RENAVAM e chassi.' }),
    article({ id: 'b', title: 'Como alocar um motorista', summary: 'Escolha o motorista e confirme.', keywords: ['atribuir', 'designar'] }),
    article({ id: 'c', title: 'Documentos vencidos', summary: 'Veja o status Vencido de um documento.' }),
  ];

  it('returns articles matching every word of the query, ignoring accents and case', () => {
    expect(searchArticles('CADASTRAR veiculo', articles).map((a) => a.id)).toEqual(['a']);
    expect(searchArticles('documento vencido', articles).map((a) => a.id)).toEqual(['c']);
  });

  it('matches a synonym stored only in keywords', () => {
    expect(searchArticles('atribuir', articles).map((a) => a.id)).toEqual(['b']);
  });

  it('requires every word to match somewhere (AND, not OR)', () => {
    expect(searchArticles('motorista chassi', articles)).toHaveLength(0);
  });

  it('ranks a title match above a match only in the summary', () => {
    const results = searchArticles('veiculo', [
      article({ id: 'title-match', title: 'Cadastro de veículo', summary: 'x' }),
      article({ id: 'summary-match', title: 'Outra coisa', summary: 'Fale sobre o veículo aqui' }),
    ]);
    expect(results.map((r) => r.id)).toEqual(['title-match', 'summary-match']);
  });

  it('returns nothing for a blank query', () => {
    expect(searchArticles('   ', articles)).toHaveLength(0);
  });
});
