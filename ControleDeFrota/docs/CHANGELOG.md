# Changelog

Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/). Datas no padrão AAAA-MM-DD.

## [0.1.0] — 2026-09-29 — Fase 1: Fundação

### Adicionado
- **Projeto**: análise inicial (pasta vazia), arquitetura definida (ADR-001 a ADR-017), documentação em `/docs`, `CLAUDE.md` e skill de projeto `fleet-development`.
- **Backend (.NET 8, Clean Architecture)**: solução `Fleet.sln` com Domain, Application, Infrastructure e Api.
- **Banco**: migration `InitialFoundation` com as tabelas Companies, Users, Roles, Permissions, RolePermissions, UserRoles, RefreshTokens, Drivers, Vehicles, Implements e AuditLogs. Inclui soft delete com índices únicos filtrados e seed de permissões e papéis do sistema.
- **Multiempresa**: filtro global por `CompanyId`, `CompanyId` carimbado pelo servidor, 404 para registros de outra empresa e super-admin via `companies.manage`.
- **Autenticação**: JWT de 15 minutos, refresh token rotativo em cookie HttpOnly com detecção de reuso, bloqueio após 5 falhas, rate limit no login, troca de senha e redefinição de senha pelo administrador.
- **Autorização**: 20 permissões granulares, 8 papéis do sistema, `[HasPermission]` em todos os endpoints, fallback policy autenticada e anti-escalonamento de privilégio.
- **Módulos**: Empresas, Usuários, Papéis (leitura), Motoristas, Veículos, Implementos, Painel (4 indicadores + alertas de CNH) e Histórico de auditoria por registro.
- **Validações** (backend e frontend espelhados): CPF, CNPJ numérico e **alfanumérico**, placa antiga e Mercosul, RENAVAM, chassi (VIN), CEP, telefone, e-mail, UF, CNH, faixas de ano e datas.
- **Auditoria**: diff automático (valor antigo → novo) de Company, User, Driver, Vehicle e Implement, inclusive troca de papéis. A senha é mascarada.
- **API**: REST em `/api/v1`, erros em ProblemDetails com mensagens pt-BR e erros por campo, headers de segurança, `X-Trace-Id`, `/health` e Swagger (somente em Development).
- **Frontend (React 18 + Mantine 7)**: login, layout responsivo com menu por permissão, tema claro/escuro, listas com busca, filtros na URL, ordenação, paginação e cards no celular, formulários com seções, máscaras e validação inline, estados de carregamento, vazio, erro e 403, confirmação de exclusão, aviso de alterações não salvas, modo somente leitura e carregamento das páginas sob demanda.
- **Testes**: 176 no backend (90 de domínio, 70 de serviços com SQLite, 16 de integração HTTP) e 35 no frontend (Vitest).
- **Seed de desenvolvimento**: duas empresas, quatro usuários de demonstração e uma frota de exemplo.

### Segurança
- Dependências atualizadas para versões sem vulnerabilidades conhecidas em produção: axios 1.20, react-router 7.18, pacotes de teste do xUnit.
- Residual aceito, somente em desenvolvimento: 1 aviso moderado no Vitest 3.x, cuja correção exige Node 20+ (ver SECURITY.md).
