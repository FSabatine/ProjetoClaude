# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

REC4 é a reconstrução de um sistema OutSystems em .NET 8 + React. Até agora existe só a fundação: Auth, Usuários, Grupos, Permissões, Escritórios e Pessoas. Financeiro, Conciliação, Pedidos e Documentos (CT-e/MDF-e) **ainda não existem**. [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) é a referência detalhada (modelo de dados, superfície da API, seed de permissões, TODOs de negócio em aberto) — leia antes de mexer em auth/permissões e mantenha-o atualizado quando mudar essas áreas.

## Comandos

Todos a partir de `REC4/`.

```bash
# Backend
dotnet build REC4.sln
dotnet run --project src/REC4.Api --launch-profile http     # http://localhost:5078, Swagger em /swagger
dotnet test REC4.sln
dotnet test tests/REC4.Application.Tests --filter "FullyQualifiedName~UsuarioServiceTests"   # uma classe/teste

# Migrations (EF Core, dotnet-ef 8 instalado globalmente) — NÃO são aplicadas automaticamente no startup
dotnet ef migrations add <Nome> --project src/REC4.Infrastructure --startup-project src/REC4.Api --output-dir Data/Migrations
dotnet ef database update --project src/REC4.Infrastructure --startup-project src/REC4.Api

# Frontend (em frontend/)
npm run dev      # http://localhost:5173, proxy /api -> http://localhost:5078
npm run build    # tsc -b + vite build (é o typecheck)
npm run lint
```

Em Development a API usa `(localdb)\MSSQLLocalDB`, banco `REC4`, e cria 3 usuários de teste se `Usuarios` estiver vazia (`DevDataSeeder`): `admin@rec4.local` / `Rec4!Admin123` (Controladoria, todas as permissões). Os demais estão no ARCHITECTURE.md. Não há testes de frontend.

## Arquitetura

Clean Architecture, dependências só para dentro: `Api → Infrastructure → Application → Domain`.

- **Domain**: entidades/enums puros + catálogo seed de permissões e grupos (`Seed/PermissoesPadrao.cs`, `Seed/GruposPadrao.cs`, aplicados via `HasData` nas migrations).
- **Application**: um serviço por módulo (`Pessoas/`, `Usuarios/`, `Grupos/`, `Escritorios/`, ...) com DTOs, validators FluentValidation e mappings. Serviços usam `IRec4DbContext` direto (sem repositórios). Abstrações `IRec4DbContext`, `IPasswordHasher`, `ITokenService` ficam em `Common/` e são implementadas em Infrastructure.
- **Infrastructure**: `Rec4DbContext` + `Data/Configurations/` (uma `IEntityTypeConfiguration` por entidade), segurança (JWT, hash PBKDF2). **Todo serviço novo é registrado em `DependencyInjection.cs`** (`AddRec4Infrastructure`).
- **Api**: controllers finos; erros viram status HTTP em `Middleware/ExceptionHandlingMiddleware.cs` — serviços lançam exceções de `Application/Exceptions` (`NotFoundException` → 404, `Duplicate*` → 409, `ValidationException` → 400). Nova exceção de domínio precisa de um case nesse `Map`.
- **frontend/**: SPA Vite + React + TS. `src/api/` (axios compartilhado em `client.ts`), `src/auth/` (contexto, `RequireAuth`, `RequirePermission`, `usePermission`), `src/pages/`, `src/types/`.
- `src/REC4.Web` é um projeto Blazor que **não está no `REC4.sln`**; o frontend atual é `frontend/`.

## Regras que não são óbvias pelo código

- **Autorização é só por permissão, nunca por nome de grupo ou `TipoUsuario`** (exigência explícita do cliente). Endpoints usam `[Authorize(Policy = "Modulo.Acao")]`; `PermissionPolicyProvider` aceita qualquer string como chave de permissão, então não há `AddPolicy` a registrar. Permissão nova = entrada no `PermissoesPadrao` + migration + atributo no endpoint.
- As permissões efetivas vão no JWT (claim `perm`, um por permissão). `MapInboundClaims = false`, então os claims mantêm os nomes `sub`/`perm`/`grupo` (`login`/`grupo`/`perm` têm constantes em `Common/Rec4Claims.cs`).
- `Common/PermissaoEfetivaResolver.cs` é a **única** fonte da regra grupo vs. permissões personalizadas (`TemPermissaoPersonalizada` substitui, não soma). Use-o em vez de reimplementar.
- Regra "no máximo um principal por dono" (endereço/conta bancária de Pessoa, escritório de Usuário) = índice único filtrado + `Common/PrincipalSwapHelper.cs`. Reutilize para casos novos.
- `Pessoa` tem soft delete via `IsActive` + query filter (as entidades filhas filtram por `Pessoa.IsActive`); `Usuario` **não** — inativo continua listado.
- Frontend: token só em memória (nunca `localStorage`/`sessionStorage`); F5 força novo login, de propósito. Esconder coisas na UI é só UX; a autorização real é no backend.
- `PermissionModuleGrid.tsx` monta o grid a partir do catálogo real (módulo = prefixo antes do `.`), não de colunas fixas.
- Testes de Application usam SQLite in-memory (`TestDbContextFactory`, `EnsureCreated`), não SQL Server — comportamentos específicos do SQL Server (ex. índices filtrados) podem divergir.
- Decisões de negócio ainda não confirmadas ficam listadas na seção TODO do ARCHITECTURE.md; quando tiver que fazer uma escolha provisória, registre-a lá em vez de decidir em silêncio.
