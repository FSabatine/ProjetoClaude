import { describe, expect, it } from 'vitest';
import { HELP_ARTICLES, HELP_CATEGORIES } from './index';

/**
 * O manual é dado estático (ver ADR em docs/DECISIONS.md): este teste é a rede de segurança contra
 * erro de digitação num id — sem ele, um artigo "relacionado" quebrado só apareceria em produção.
 */
describe('help content integrity', () => {
  it('has no duplicate category ids', () => {
    const ids = HELP_CATEGORIES.map((c) => c.id);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it('has no duplicate article ids', () => {
    const ids = HELP_ARTICLES.map((a) => a.id);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it('every article has a title and a summary', () => {
    for (const article of HELP_ARTICLES) {
      expect(article.title.trim(), `article ${article.id} has no title`).not.toBe('');
      expect(article.summary.trim(), `article ${article.id} has no summary`).not.toBe('');
    }
  });

  it('every article points to a category that exists', () => {
    const categoryIds = new Set(HELP_CATEGORIES.map((c) => c.id));
    for (const article of HELP_ARTICLES) {
      expect(categoryIds.has(article.categoryId), `article ${article.id} has an unknown categoryId "${article.categoryId}"`).toBe(true);
    }
  });

  it('every related article id points to an article that exists', () => {
    const articleIds = new Set(HELP_ARTICLES.map((a) => a.id));
    for (const article of HELP_ARTICLES) {
      for (const relatedId of article.relatedArticleIds ?? []) {
        expect(articleIds.has(relatedId), `article ${article.id} links to an unknown related article "${relatedId}"`).toBe(true);
      }
    }
  });
});
