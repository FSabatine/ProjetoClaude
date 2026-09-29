# CLAUDE.md

Guia rápido para agentes de IA neste repositório. **A fonte da verdade é `docs/`**: leia o documento da área antes de alterá-la e atualize-o no mesmo trabalho. Em qualquer implementação, siga a skill de projeto `fleet-development` (`.claude/skills/fleet-development/SKILL.md`).

Controle de Frota é um sistema de gestão de frotas multiempresa em .NET 8 + React. A fase atual e o escopo estão em `docs/ROADMAP.md` (Fase 1 — Fundação). **Não implemente módulos de fases futuras** (manutenção, combustível, pneus, viagens, rastreamento…); apenas deixe o ponto de extensão.

## Comandos (a partir de `ControleDeFrota/`)

```bash
dotnet build Fleet.sln -warnaserror
dotnet test Fleet.sln                                           # Domain + Application (SQLite) + Api (WebApplicationFactory)
dotnet test tests/Fleet.Application.Tests --filter "FullyQualifiedName~VehicleServiceTests"
dotnet run --project src/Fleet.Api --launch-profile http        # http://localhost:5080, Swagger em /swagger; em Development aplica migrations e seed

dotnet ef migrations add <Nome> --project src/Fleet.Infrastructure --startup-project src/Fleet.Api --output-dir Persistence/Migrations
dotnet ef migrations has-pending-model-changes --project src/Fleet.Infrastructure --startup-project src/Fleet.Api

cd frontend
npm run dev          # http://localhost:5173 (proxy /api → 5080). Porta ocupada (ex.: REC4)? npx vite --port 5174
npm run build        # tsc -b + vite build
npm run lint
npm test             # vitest (validadores e formatadores)
```

Login de desenvolvimento: `admin@frota.local` / `FrotaDev!2026`. Os demais usuários estão em `docs/README.md`.

## Arquitetura em uma tela

`Fleet.Api → Fleet.Infrastructure → Fleet.Application → Fleet.Domain` (dependências só para dentro).

- **Domain**: entidades, enums, `Validation/` (CPF, CNPJ alfanumérico, placa, RENAVAM, chassi, CEP…) e `Authorization/` (catálogo de permissões e papéis do sistema, usados no seed).
- **Application**: um `XxxService` por módulo, com DTOs e validators FluentValidation. Usa `IFleetDbContext` direto, sem repositórios, MediatR ou AutoMapper. Todo serviço novo é registrado em `Fleet.Application/DependencyInjection.cs`.
- **Infrastructure**: o `FleetDbContext` aplica **filtro de tenant (`ITenantScoped`) e soft delete (`ISoftDeletable`) em toda query**. No `SaveChangesAsync`, ele carimba o `CompanyId`, converte `Remove()` em soft delete e grava `AuditLogs` (via `AuditTrailBuilder`).
- **Api**: controllers finos, com `[HasPermission(...)]` em todo endpoint. Os erros viram ProblemDetails em `Infrastructure/AppExceptionHandler.cs`.
- **frontend/**: `src/features/<modulo>/` (tipos, labels e páginas), `src/components/` (DataTable, forms, States, AppLayout), `src/api/` (axios com refresh automático) e `src/auth/`.

## Regras que não são óbvias pelo código

- Autorização **só por permissão**, nunca pelo nome do papel. Anti-escalonamento: ninguém atribui um papel com permissão que ele próprio não tem.
- ID de outra empresa responde **404**, nunca 403. Não use `IgnoreQueryFilters()` sem um comentário justificando.
- `User` **não** é `ITenantScoped`, porque o login precisa encontrá-lo antes de saber o tenant. O `UserService.ScopedUsers()` filtra manualmente.
- Nunca preencha `CompanyId`, `CreatedAt`, `UpdatedAt` ou `DeletedAt` à mão.
- Índices únicos são filtrados por `[DeletedAt] IS NULL`. A placa é única por empresa **entre veículos e implementos** (`RegisteredAssetRules`).
- Enums são gravados como texto, datas com hora usam `DateTime` UTC, e não se ordena por `decimal` (limitação do SQLite usado nos testes).
- No frontend, o access token fica só em memória. O refresh token é um cookie HttpOnly rotativo: se um token antigo for reutilizado, todas as sessões do usuário são revogadas.
- Os validadores do frontend (`src/lib/validators.ts`) espelham `Fleet.Domain/Validation`. Ao mudar um, mude o outro e os testes dos dois.
- Status e labels em pt-BR ficam num único mapa por módulo (`features/<modulo>/<modulo>.ts`). Nada de string de status solta.
- Decisões pendentes ficam em "Pontos em aberto" de `docs/DECISIONS.md`; não decida em silêncio.
- A revisão visual automatizada com Edge/Chromium headless travou esta máquina (2026-09-29). Não rode browser headless aqui sem combinar antes com o usuário.
