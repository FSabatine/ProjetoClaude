import type { HelpArticle } from './types';

/** Lowercase, accent-insensitive — "não conforme" matches "nao conforme". */
const normalize = (value: string) => value.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase();

const FIELD_WEIGHT = { title: 5, summary: 3, keywords: 3, steps: 1, example: 1 } as const;

/**
 * Simple client-side relevance search (sem lib externa — o conteúdo é pequeno e estático). Cada palavra da busca
 * precisa aparecer em algum campo do artigo; o ranking soma o peso do campo onde cada palavra foi encontrada.
 */
export function searchArticles(query: string, articles: HelpArticle[]): HelpArticle[] {
  const words = normalize(query).split(/\s+/).filter(Boolean);
  if (words.length === 0) return [];

  const scored = articles
    .map((article) => {
      const fields: Record<keyof typeof FIELD_WEIGHT, string> = {
        title: article.title,
        summary: article.summary,
        keywords: (article.keywords ?? []).join(' '),
        steps: (article.steps ?? []).join(' '),
        example: article.example ?? '',
      };
      const normalizedFields = Object.fromEntries(
        Object.entries(fields).map(([key, value]) => [key, normalize(value)]),
      ) as Record<keyof typeof FIELD_WEIGHT, string>;

      let score = 0;
      for (const word of words) {
        let matchedWord = false;
        for (const [field, weight] of Object.entries(FIELD_WEIGHT) as [keyof typeof FIELD_WEIGHT, number][]) {
          if (normalizedFields[field].includes(word)) {
            score += weight;
            matchedWord = true;
          }
        }
        if (!matchedWord) return { article, score: -1 }; // every word must match somewhere
      }
      return { article, score };
    })
    .filter((r) => r.score >= 0)
    .sort((a, b) => b.score - a.score);

  return scored.map((r) => r.article);
}
