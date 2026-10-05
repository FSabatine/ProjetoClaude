import type { HelpArticle, HelpCategory } from '../types';
import * as assignments from './assignments';
import * as checklists from './checklists';
import * as companies from './companies';
import * as dashboard from './dashboard';
import * as documents from './documents';
import * as drivers from './drivers';
import * as faq from './faq';
import * as financial from './financial';
import * as fuel from './fuel';
import * as gettingStarted from './gettingStarted';
import * as implementsContent from './implements';
import * as maintenance from './maintenance';
import * as mileage from './mileage';
import * as occurrences from './occurrences';
import * as rolesPermissions from './rolesPermissions';
import * as searchAndFilters from './searchAndFilters';
import * as tires from './tires';
import * as users from './users';
import * as vehicles from './vehicles';

export { WHATS_NEW } from './whatsNew';

/** Order here is the order categories appear in the Help Center home (seção 27 do pedido). */
const MODULES = [
  gettingStarted,
  dashboard,
  companies,
  users,
  rolesPermissions,
  drivers,
  vehicles,
  implementsContent,
  assignments,
  mileage,
  documents,
  checklists,
  occurrences,
  maintenance,
  fuel,
  tires,
  financial,
  searchAndFilters,
  faq,
];

export const HELP_CATEGORIES: HelpCategory[] = MODULES.map((m) => m.CATEGORY);
export const HELP_ARTICLES: HelpArticle[] = MODULES.flatMap((m) => m.ARTICLES);

const ARTICLES_BY_ID = new Map(HELP_ARTICLES.map((a) => [a.id, a]));
const CATEGORIES_BY_ID = new Map(HELP_CATEGORIES.map((c) => [c.id, c]));

export const getArticleById = (id: string): HelpArticle | undefined => ARTICLES_BY_ID.get(id);
export const getCategoryById = (id: string): HelpCategory | undefined => CATEGORIES_BY_ID.get(id);
export const getArticlesForCategory = (categoryId: string): HelpArticle[] =>
  HELP_ARTICLES.filter((a) => a.categoryId === categoryId);
