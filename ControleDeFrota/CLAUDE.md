# CLAUDE.md

Guia rápido para agentes de IA neste repositório. **A fonte da verdade é `docs/`**: leia o documento da área antes de alterá-la e atualize-o no mesmo trabalho. Em qualquer implementação, siga a skill de projeto `fleet-development` (`.claude/skills/fleet-development/SKILL.md`).

Controle de Frota é um sistema de gestão de frotas multiempresa em .NET 8 + React. A fase atual e o escopo estão em `docs/ROADMAP.md` (Fases 1 — Fundação, 2 — Controle operacional e 3 — Manutenção concluídas; a Fase 2.5 — Viagens e composição foi conscientemente adiada por decisão do usuário em 2026-10-01, ver ROADMAP.md e ADR-026). **Não implemente módulos de fases futuras** (combustível, pneus, viagens, rastreamento…); apenas deixe o ponto de extensão (normalmente um novo valor em `OperationalEventType`).

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

## Controle operacional (Fase 2) em uma tela

- Módulos: `Assignments`, `Mileage`, `Documents`, `Files`, `Checklists`, `Occurrences`, `Operations` (eventos + histórico) em Domain e Application; controllers em `Api/Controllers/OperationsControllers.cs`; mapeamentos em `Persistence/Configurations/OperationsConfigurations.cs`.
- As regras puras ficam no Domain e são as únicas fontes: `VehicleOperationalState` (Alocado é **derivado**, ADR-018), `AssignmentRules`, `OdometerPolicy`, `DocumentExpiryPolicy`, `OccurrenceWorkflow`, `ChecklistSchedule`, `FileRules`. Quando existe forma SQL de uma regra (`VehicleService.WhereOperationalStatus`, `DocumentQueries.WhereStatus`), há teste garantindo que as duas concordam.
- **Todo fato operacional** é registrado com `OperationalEventLog.Record(...)` antes do `SaveChangesAsync`: ele alimenta o histórico do veículo/motorista e é o outbox das notificações futuras (ADR-025). Não chame um módulo a partir de outro para "avisar".
- `Vehicle.CurrentOdometerKm` só muda via `MileageService` (a edição do veículo recusa). Leitura suspeita fica `PendingReview` e **não** é aplicada.
- Arquivos: bytes em `IFileStorage` (`LocalFileStorage`, `Storage:LocalRootPath`), nunca no banco. Um upload só é vinculado pelo próprio autor (`FileService.AttachAsync`), e o download checa a permissão do dono.
- Histórico nunca é apagado: alocação é encerrada, leitura é rejeitada/corrigida, ocorrência é cancelada, execução de checklist é imutável (snapshot dos itens).
- Transação com vários `SaveChanges` (ex.: encerrar e abrir alocação sob índice único filtrado): `IFleetDbContext.InTransactionAsync`.
- Frontend: hubs `/veiculos/:id` e `/motoristas/:id` com abas na URL (`?aba=`); edição em `/:id/editar`. Tipos e hooks da Fase 2 ficam em `features/operations/api.ts`, e os rótulos/cores em `features/operations/labels.ts`. Mutations operacionais invalidam todo o cache do React Query.
- Mantine `NumberInput` com `thousandSeparator="."` exige `decimalSeparator=","` (sem isso, a tela quebra).

## Manutenção (Fase 3) em uma tela

- Módulos: `Workshops`, `MaintenancePlans`/`MaintenanceSchedules`, `HourMeter` (histórico do horímetro, espelha `Mileage`/ADR-019), `MaintenanceRequests`, `WorkOrders` em `Fleet.Domain/Maintenance` e `Fleet.Application/Maintenance`; controllers em `Api/Controllers/MaintenanceControllers.cs`; mapeamentos em `Persistence/Configurations/MaintenanceConfigurations.cs`.
- Regras puras no Domain: `MaintenanceSchedulePolicy` (status Scheduled/DueSoon/Due/Overdue, o eixo mais urgente entre km/data/horas vence), `MaintenancePlanResolver` (veículo específico > tipo de veículo > padrão da empresa), `HourMeterPolicy` (mesma forma do `OdometerPolicy`), `WorkOrderWorkflow`/`MaintenanceRequestWorkflow` (máquinas de estado, mesmo molde do `OccurrenceWorkflow`).
- **`Vehicle.Status = UnderMaintenance` é controlado pelo `WorkOrderService`** (ADR-028), não existe `VehicleStatusService`: ao entrar em `InProgress`/`WaitingParts` o veículo vira `UnderMaintenance` (se estava `Available`); ao sair dessas situações, só volta a `Available` se **nenhuma outra OS ativa** restar e o status ainda for `UnderMaintenance` (nunca sobrescreve uma mudança manual, ex. `Unavailable`). Veículo `OnTrip`/`Inactive` recusa iniciar manutenção.
- **Aprovar uma `MaintenanceRequest` cria a `WorkOrder` (já `Approved`) na mesma transação** — não existe um estado "aprovada, sem OS ainda". Ocorrência → solicitação é sempre uma ação manual (botão), nunca automática: nem toda ocorrência vira manutenção.
- `WorkOrder.Number` (`OS-000001`) é `Sequence` por empresa, calculado por `MAX(Sequence)+1` — sem contador atômico dedicado (ver "Pontos em aberto da Fase 3" em `DECISIONS.md`).
- Custo (`PartsCost`/`LaborCost`/`OtherCost`/`TotalCost`, `UnitCost` de peça, `HourlyRate`) só aparece na resposta para quem tem `maintenance.viewcosts`; sem a permissão os campos voltam `null`/zerados — o frontend não deve renderizar a seção de custo nesse caso.
- `MaintenanceSchedule` só existe (linha gravada) depois da primeira manutenção feita naquele item; antes disso, `MaintenanceScheduleService` calcula a "linha de base" a partir do cadastro do veículo (mesma ideia do `MileageService.BaselineAsync`). Sem técnico (`Mechanic`) ou inventário de peças nesta fase — ver DECISIONS.md.
- Frontend: `features/maintenance/` (tipos/labels em `maintenance.ts`, hooks em `api.ts`); nova aba **"Manutenção"** no hub do veículo; bloco "Manutenção" no dashboard. Igual à Fase 2, `nextStatuses` da API decide os botões de transição — nunca hardcode regras de workflow no componente.

## Regras que não são óbvias pelo código

- Autorização **só por permissão**, nunca pelo nome do papel. Anti-escalonamento: ninguém atribui um papel com permissão que ele próprio não tem.
- ID de outra empresa responde **404**, nunca 403. Não use `IgnoreQueryFilters()` sem um comentário justificando.
- `User` **não** é `ITenantScoped`, porque o login precisa encontrá-lo antes de saber o tenant. O `UserService.ScopedUsers()` filtra manualmente.
- Nunca preencha `CompanyId`, `CreatedAt`, `UpdatedAt` ou `DeletedAt` à mão.
- Índices únicos são filtrados por `[DeletedAt] IS NULL`. A placa é única por empresa **entre veículos e implementos** (`RegisteredAssetRules`).
- Enums são gravados como texto, datas com hora usam `DateTime` UTC, e não se ordena por `decimal` (limitação do SQLite usado nos testes).
- `Id` (GUID sequencial) **não é um desempate de ordenação confiável** nos testes: o gerador é sequencial para a ordenação especial do SQL Server, mas o SQLite dos testes compara os bytes em ordem simples. Quando duas linhas empatam na coluna principal (ex. dois registros no mesmo instante), desempate por algo com significado de negócio (ex. "ativo primeiro"), não por `Id`.
- No frontend, o access token fica só em memória. O refresh token é um cookie HttpOnly rotativo: se um token antigo for reutilizado, todas as sessões do usuário são revogadas.
- Os validadores do frontend (`src/lib/validators.ts`) espelham `Fleet.Domain/Validation`. Ao mudar um, mude o outro e os testes dos dois.
- Status e labels em pt-BR ficam num único mapa por módulo (`features/<modulo>/<modulo>.ts`). Nada de string de status solta.
- Decisões pendentes ficam em "Pontos em aberto" de `docs/DECISIONS.md`; não decida em silêncio.
- A revisão visual automatizada com Edge/Chromium headless travou esta máquina (2026-09-29). Não rode browser headless aqui sem combinar antes com o usuário.
