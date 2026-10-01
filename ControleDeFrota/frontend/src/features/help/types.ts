import type { Permission } from '../../auth/permissions';

/**
 * Help content lives as static TypeScript data (ADR em DECISIONS.md), não banco/CMS: o manual evolui
 * junto do código, no mesmo PR que muda o comportamento que ele descreve.
 */
export interface HelpArticle {
  id: string;
  categoryId: string;
  title: string;
  /** Shown in lists and search results — one or two sentences. */
  summary: string;
  whyItMatters?: string;
  steps?: string[];
  /** A short, concrete example (plain text; "\n" breaks lines). */
  example?: string;
  notes?: string[];
  /** Extra search terms not already present in title/summary (synonyms, technical names users might type). */
  keywords?: string[];
  relatedArticleIds?: string[];
  /** Hidden from users who lack this permission — relevance, not a security boundary (seção 41). */
  requiredPermission?: Permission;
}

export interface HelpCategory {
  id: string;
  label: string;
  description: string;
  requiredPermission?: Permission;
}

/** A real, already-shipped change — never a planned/future feature (seção 32 do pedido). */
export interface HelpWhatsNewEntry {
  id: string;
  title: string;
  description: string;
  /** "outubro de 2026", already in pt-BR, free text so it reads naturally. */
  date: string;
}

export type HelpAnalyticsEvent =
  | { type: 'HelpArticleViewed'; articleId: string }
  | { type: 'HelpSearchPerformed'; query: string; resultCount: number }
  | { type: 'HelpSearchNoResult'; query: string };
