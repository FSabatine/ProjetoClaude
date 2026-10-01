# Arquitetura

## Visão geral

Monólito modular em **Clean Architecture**, com API REST em .NET 8 e SPA React. A escolha segue o padrão já usado na Rodoxisto (projeto REC4), sem dependência de código com ele (ADR-001, ADR-002).

```
┌──────────────────────────┐        HTTPS/JSON         ┌───────────────────────────────┐
│ frontend/ (React + Vite) │ ───────────────────────▶  │ Fleet.Api (ASP.NET Core 8)    │
│ Mantine UI, React Query  │  Bearer JWT (memória)     │ controllers, auth, middleware │
└──────────────────────────┘  refresh token (cookie)   └──────────────┬────────────────┘
                                                                      │
                                     ┌────────────────────────────────▼───────────────┐
                                     │ Fleet.Application                              │
                                     │ serviços por módulo, DTOs, validators, regras  │
                                     └──────────────┬─────────────────────────────────┘
                                                    │ usa abstrações (IFleetDbContext, ICurrentUser…)
                                     ┌──────────────▼──────────────┐   ┌─────────────────────────┐
                                     │ Fleet.Domain                │   │ Fleet.Infrastructure    │
                                     │ entidades, enums, validação │◀──│ EF Core/SQL Server, JWT,│
                                     │ de documentos, permissões   │   │ hash de senha, auditoria│
                                     └─────────────────────────────┘   └─────────────────────────┘
```

## Camadas e regra de dependência

Dependências apontam **só para dentro**: `Api → Infrastructure → Application → Domain`.

| Projeto | Responsabilidade | Pode depender de |
|---|---|---|
| `Fleet.Domain` | Entidades, enums de status/tipo, value objects (`Address`), validação de documentos brasileiros (CPF, CNPJ, placa, RENAVAM, chassi, CEP), catálogo de permissões e papéis. Sem EF, sem ASP.NET. | nada |
| `Fleet.Application` | Casos de uso: um serviço por módulo (`Vehicles/VehicleService`…), DTOs de entrada/saída, validators FluentValidation, paginação/ordenação, exceções de aplicação. Define as abstrações `IFleetDbContext`, `ICurrentUser`, `IPasswordHasher`, `ITokenService`, `IClock`. | Domain, EF Core (apenas `DbSet`/LINQ) |
| `Fleet.Infrastructure` | `FleetDbContext` (mapeamentos, filtros globais de tenant e soft delete, auditoria no `SaveChanges`), migrations, JWT, hash de senha, seed de desenvolvimento. | Application, Domain |
| `Fleet.Api` | Controllers finos, autenticação/autorização, tratamento global de erros (ProblemDetails), rate limiting, headers de segurança, `ICurrentUser` a partir do `HttpContext`. | todos |
| `frontend/` | SPA. Nenhuma regra de negócio definitiva; valida para dar feedback, mas o backend é a autoridade. | API via HTTP |

### Por que o Application usa `IFleetDbContext` direto (sem repositórios)

O `DbContext` já é Unit of Work + Repository. Uma camada de repositórios genéricos só repassaria chamadas. Os serviços usam LINQ sobre `IFleetDbContext`, e os testes usam SQLite em memória (ADR-004).

## Fluxo de uma requisição

1. `Fleet.Api` autentica o JWT, e a policy `perm:<chave>` verifica a permissão (ADR-006).
2. O controller chama o serviço do módulo com um DTO.
3. O serviço valida com FluentValidation. Violações geram uma `ValidationException`, que vira 400 com os erros por campo.
4. O serviço aplica as regras de negócio (duplicidade, transições de status, autoescalonamento de privilégio etc.) e usa o `IFleetDbContext`.
5. O `FleetDbContext.SaveChangesAsync`:
   - preenche `CreatedAt/UpdatedAt/CreatedBy/UpdatedBy`;
   - converte `Remove()` em soft delete (`DeletedAt/DeletedBy`) nas entidades `ISoftDeletable`;
   - carimba o `CompanyId` do usuário corrente nas entidades `ITenantScoped`;
   - grava um `AuditLog` por entidade `IAuditable` alterada, com os valores antigos e novos.
6. Erros de aplicação viram ProblemDetails pelo `ExceptionHandler`.

## Multi-empresa (tenancy)

Isolamento **lógico** por `CompanyId` num banco compartilhado (ADR-003).

- Entidades operacionais (`Driver`, `Vehicle`, `Implement` e as futuras) implementam `ITenantScoped`. Um **filtro global do EF** restringe as consultas ao `CompanyId` do usuário autenticado, então nenhuma query de módulo precisa lembrar de filtrar.
- Na inclusão, o `CompanyId` é preenchido automaticamente e não pode ser alterado depois.
- Um ID de outra empresa retorna **404** (não 403), para não revelar que o registro existe.
- `User` pertence a uma empresa, mas **não** tem filtro global, porque o login precisa localizar o e-mail antes de conhecer o tenant. O `UserService` filtra explicitamente.
- **Super-admin da plataforma** = usuário com a permissão `companies.manage`. Ele gerencia todas as empresas e cria usuários em qualquer uma (para instalar o primeiro administrador de uma empresa nova). Os dados operacionais (veículos, motoristas…) continuam isolados na empresa do próprio usuário. A troca de empresa ativa é uma evolução futura (ver ROADMAP).

## Padrões adotados

| Padrão | Onde | Motivo |
|---|---|---|
| Clean Architecture | solução | isolar o domínio da infraestrutura e testar regras sem banco real |
| Service layer por módulo | Application | casos de uso explícitos e simples, sem CQRS/MediatR (evita cerimônia; ADR-005) |
| Unit of Work (DbContext) | Infrastructure | transação por requisição |
| Value Object | `Address` (owned type) | endereço reaproveitado por Company e Driver |
| Policy-based authorization | Api | autorização por permissão, nunca por nome de papel |
| Global query filters | Infrastructure | tenant + soft delete aplicados sempre |
| ProblemDetails (RFC 9457) | Api | contrato de erro único, com mensagens amigáveis em PT-BR |
| Options pattern | Api/Infrastructure | configuração tipada (`JwtOptions`, `AuthOptions`, `FileStorageOptions`, `DocumentExpirationJobOptions`) |
| Outbox / event log | `OperationalEvents` (Fase 2) | histórico operacional e base das notificações, gravados na mesma transação da mudança (ADR-025) |
| Strategy (storage) | `IFileStorage` → `LocalFileStorage` | trocar o disco por object/cloud storage sem tocar nos módulos (ADR-022) |
| Policy objects no Domain | `VehicleOperationalState`, `OdometerPolicy`, `DocumentExpiryPolicy`, `OccurrenceWorkflow`, `AssignmentRules`, `ChecklistSchedule` | regras puras, testáveis sem banco; os serviços só orquestram |

## Tratamento de erros

| Exceção (Application) | HTTP | Uso |
|---|---|---|
| `ValidationException` (FluentValidation) | 400 | campos inválidos, com `errors` por campo |
| `BusinessRuleException` | 422 | regra de negócio violada (ex.: "Você não pode desativar o próprio usuário") |
| `NotFoundException` | 404 | não existe **ou** pertence a outra empresa |
| `ConflictException` | 409 | duplicidade (placa, CPF, CNPJ, e-mail) |
| `ForbiddenException` | 403 | ação proibida mesmo com a permissão de base (ex.: atribuir papel com mais privilégios) |
| `AuthenticationFailedException` | 401 | credenciais inválidas, conta bloqueada ou inativa |
| qualquer outra | 500 | registrada com `traceId` e respondida com uma mensagem genérica, sem stack trace |

Toda resposta de erro inclui `traceId`, que também aparece no log, para cruzar com um relato do usuário.

## Logging e observabilidade

- `ILogger<T>` do .NET com escopos estruturados. Console legível em Development e JSON em Production (`AddJsonConsole`), pronto para ser coletado por Seq, ELK, Grafana Loki ou Application Insights.
- Middleware de correlação: cada requisição tem um `traceId` (W3C `traceparent` / `Activity`), devolvido no header `X-Trace-Id`.
- Os logs **nunca** incluem senhas, tokens ou hashes. E-mail de login só aparece em falhas de autenticação, no nível Warning.
- `GET /health` para liveness/readiness, com verificação do banco.
- Evolução: OpenTelemetry (traces e métricas) quando houver ambiente de produção definido.

## Escalabilidade

- A API é **stateless**: JWT curto e refresh token persistido no banco. Escala horizontalmente atrás de um balanceador.
- Paginação obrigatória em todas as listagens, com `pageSize` máximo de 100.
- Índices compostos começando por `CompanyId` em todas as tabelas de tenant.
- As listas operacionais (leituras, eventos, execuções, ocorrências) crescem sem limite: todas são paginadas e indexadas por `(CompanyId, dono, data)`.
- Módulos futuros (manutenção, abastecimento, rastreamento) entram como novas pastas em Application/Domain. Se algum precisar de escala própria (telemetria/GPS), pode virar um serviço separado alimentando o mesmo banco ou uma fila, sem reescrever o núcleo.

## Integrações futuras (preparação)

- Versionamento de API em `/api/v1`, para que integrações externas (ERP, rastreadores, WhatsApp) tenham um contrato estável.
- Autorização por permissão granular: uma integração pode receber um papel técnico com poucas permissões.
- Auditoria genérica: qualquer entidade nova que implemente `IAuditable` já é auditada.
- `Vehicle` e `Implement` foram mantidos enxutos. Manutenção, abastecimento, pneus, documentos, viagens, multas, sinistros e custos serão **entidades próprias** apontando para `VehicleId`, nunca colunas extras em `Vehicle`.

## Superfície da API (Fase 1)

Base `/api/v1`. Todo endpoint exige autenticação, salvo os marcados como anônimos. A permissão exigida aparece entre colchetes (`|` significa "qualquer uma").

```
POST /auth/login | /auth/refresh | /auth/logout      anônimos (login/refresh com rate limit)
GET  /auth/me                                        autenticado
POST /auth/change-password                           autenticado

GET    /vehicles?search&status&type&page&pageSize&sortBy&sortDirection   [vehicles.view]
GET    /vehicles/{id}                                                    [vehicles.view]
POST   /vehicles                                                         [vehicles.create]
PUT    /vehicles/{id}                                                    [vehicles.update]
DELETE /vehicles/{id}                                                    [vehicles.delete]  (soft delete)
       /implements ... mesmo padrão                                     [implements.*]
       /drivers    ... mesmo padrão (+ filtro licenseAlert=Expired|ExpiringSoon)  [drivers.*]

GET    /companies                     [companies.manage]
GET    /companies/current             [companies.view | companies.manage]
GET    /companies/{id}                [companies.view | companies.manage]  (a própria, salvo super-admin)
POST   /companies                     [companies.manage]
PUT    /companies/{id}                [companies.update | companies.manage]
DELETE /companies/{id}                [companies.manage]

GET    /users, /users/{id}            [users.view]
POST   /users, PUT /users/{id}, DELETE /users/{id}, POST /users/{id}/reset-password   [users.manage]
GET    /roles                         [roles.view | users.manage]
GET    /permissions                   [roles.view]
GET    /dashboard                     [dashboard.view]  (alertas só com drivers.view)
GET    /audit/{entity}/{id}           [audit.view]  (entity: Company|User|Driver|Vehicle|Implement)
GET    /health                        anônimo
```

### Fase 2 — controle operacional

```
GET  /vehicles?operationalStatus&driverId&minOdometerKm&maxOdometerKm&staleMileage&…   [vehicles.view]
GET  /drivers?licenseCategory&assignment=WithVehicle|WithoutVehicle&vehicleId&…        [drivers.view]

GET  /vehicles/{id}/assignments                                                  [assignments.view]
POST /vehicles/{id}/assignments (driverId, startedAt?, endCurrent, notes)         [assignments.manage]
GET  /drivers/{id}/assignments                                                   [assignments.view]
POST /assignments/{id}/end (endedAt?, reason)                                     [assignments.manage]

GET  /vehicles/{id}/odometer-readings?status                                     [vehicles.view]
POST /vehicles/{id}/odometer-readings (odometerKm, readAt?, notes, isCorrection)  [mileage.record | mileage.manage]  (correção exige mileage.manage, checado no serviço)
POST /odometer-readings/{id}/approve | /reject (notes)                           [mileage.manage]

GET  /document-types?ownerType&includeInactive                                   [documents.view | operations.configure]
POST /document-types, PUT|DELETE /document-types/{id}                            [operations.configure]
GET  /documents?ownerType&vehicleId&driverId&implementId&documentTypeId&status&alertsOnly&expiresFrom&expiresTo&includeReplaced   [documents.view]
GET  /documents/{id}                                                             [documents.view]
POST /documents (…, fileIds, replacesDocumentId), PUT /documents/{id}            [documents.manage]
DELETE /documents/{id}                                                           [documents.delete]

POST   /files (multipart "file", até 10 MB)   [documents.manage | checklists.execute | occurrences.create | occurrences.manage]
GET    /files/{id}                            [permissões operacionais] + permissão do registro dono, checada no serviço
DELETE /files/{id}                            [documents.manage | occurrences.manage | …] + regra do dono, checada no serviço

GET  /checklist-templates, /checklist-templates/{id}                              [checklists.view | checklists.execute | operations.configure]
POST /checklist-templates, PUT|DELETE /checklist-templates/{id}                   [operations.configure]
GET  /checklists?vehicleId&driverId&templateId&result&from&to, /checklists/{id}   [checklists.view]
GET  /checklists/pending                                                         [checklists.view | checklists.execute]
POST /checklists (vehicleId, templateId, templateVersion, driverId?, odometerKm?, answers[])   [checklists.execute]

GET  /occurrences?vehicleId&driverId&implementId&type&severity&status&openOnly&from&to, /occurrences/{id}   [occurrences.view]
POST /occurrences                                                                [occurrences.create]
PUT  /occurrences/{id}, POST /occurrences/{id}/status (status, resolution)        [occurrences.manage]

GET  /vehicles/{id}/history?from&to&type                                         [vehicles.view]
GET  /drivers/{id}/history?from&to&type                                          [drivers.view]
GET  /audit/{entity}/{id}  + VehicleAssignment, OdometerReading, DocumentType, Document, StoredFile, ChecklistTemplate, ChecklistExecution, Occurrence   [audit.view]
```

Os erros dos itens do checklist voltam como `errors["answers.{templateItemId}"]`. O conflito de alocação volta como 409, com a explicação no `title`.

## Eventos operacionais e notificações (ADR-025)

```
Serviço (Assignment, Mileage, Document, Checklist, Occurrence, Vehicle)
   └── OperationalEventLog.Record(tipo, sujeito, resumo, dados)    ← adiciona ao unit of work
         └── SaveChangesAsync: mudança + evento na MESMA transação
DocumentExpirationJob (BackgroundService, a cada Jobs:DocumentExpirationScan:IntervalMinutes)
   └── DocumentExpirationScanner: emite DocumentExpiring/DocumentExpired uma vez por mudança de estado
NotificationDispatcher futuro (Fase 9): lê OperationalEvents com PublishedAt IS NULL → e-mail/push/WhatsApp → preenche PublishedAt
```

- Produtores não conhecem consumidores. Um módulo novo acrescenta valores a `OperationalEventType`.
- O job roda dentro da API e é idempotente (`Document.LastAlertedStatus`): com várias instâncias, cada estado continua anunciado uma vez. Como não tem usuário nem tenant, ele ignora os filtros globais e grava o `CompanyId` explícito em cada evento (comentado no código).
- Configuração: `Jobs:DocumentExpirationScan:Enabled` (padrão `true`; desligado nos testes de integração) e `IntervalMinutes` (padrão 360).

## Arquivos (ADR-022)

- O `FileService` valida o tamanho (cópia limitada a 10 MB + 1 byte) e o formato pela assinatura, gera a chave `{companyId}/{aaaa}/{mm}/{guid}` e grava os bytes via `IFileStorage`. Os metadados ficam em `StoredFiles`.
- O `LocalFileStorage` confina todo caminho à raiz `Storage:LocalRootPath`, relativa ao content root da API (padrão `App_Data/files`, ignorada pelo Git). Produção com mais de uma instância precisa de um storage compartilhado, ou seja, uma nova implementação de `IFileStorage`.

As listagens retornam `{ items, page, pageSize, totalCount, totalPages }` e ordenam apenas por colunas da whitelist de cada serviço (`SortMap`).

## Frontend

```
frontend/src/
├── api/           client.ts (axios + refresh single-flight), crud.ts (createResource: list/get/save/remove com React Query), errors.ts (ProblemDetails → mensagem + erros por campo)
├── auth/          AuthContext (sessão, can()), guards (RequireAuth, RequirePermission, Can), permissions.ts (espelho do catálogo)
├── components/    AppLayout, PageHeader, DataTable, States (vazio/erro/sem resultado), forms (MaskedInput, FormSection, FormActions, guarda de alterações, confirmDelete), AddressFields, common (StatusBadge, ListToolbar, RowActions)
├── features/<m>/  <m>.ts (tipos + mapas de rótulos/cores + resource), <M>ListPage.tsx, <M>FormPage.tsx, <M>DetailPage.tsx (hub com abas)
├── features/operations/  labels.ts (rótulos/cores dos enums da Fase 2) e api.ts (tipos + hooks: alocação, hodômetro, documentos, arquivos, checklists, ocorrências, histórico)
├── features/{assignments,mileage,documents,checklists,occurrences,history}/  painéis reutilizados nos hubs + páginas próprias
├── features/maintenance/  maintenance.ts (tipos + rótulos), api.ts (hooks), páginas de oficinas/planos/solicitações/ordens de serviço e o painel de manutenção do hub do veículo
├── features/help/ Central de Ajuda (manual do usuário, ver abaixo) — content/ (dado estático por categoria), search.ts, context.ts, analytics.ts, HelpButton/HelpDrawer/HelpArticleView
├── hooks/         useListParams (busca/filtros/ordem/página na URL)
├── lib/           validators.ts (espelho do Domain), format.ts, mileage.ts (espelho do OdometerPolicy para feedback imediato), images.ts (redução das fotos antes do upload)
└── theme.ts       tema Mantine (única fonte de cores)
```

Fluxo de sessão: ao abrir o app, `POST /auth/refresh` restaura a sessão pelo cookie. Um 401 em qualquer chamada dispara um único refresh e repete a chamada. Se o refresh falhar, o usuário volta ao login com o aviso "sessão expirada".

### Central de Ajuda (manual do usuário)

Só frontend — não é um módulo do backend. O conteúdo é dado estático versionado em `features/help/content/*.ts` (um arquivo por categoria, cada um exportando `HELP_CATEGORY`/`ARTICLES`), não um CMS nem uma tabela no banco (ADR em DECISIONS.md). A busca (`search.ts`) e a ajuda contextual por rota (`context.ts`) são funções puras, sem dependência nova. É deliberadamente **separado** da documentação técnica em `docs/`: o manual é para quem usa o sistema, pt-BR e sem detalhe de implementação; `docs/` é para quem desenvolve.
