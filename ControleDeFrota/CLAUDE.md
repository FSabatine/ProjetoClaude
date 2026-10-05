# CLAUDE.md

Guia rápido para agentes de IA neste repositório. **A fonte da verdade é `docs/`**: leia o documento da área antes de alterá-la e atualize-o no mesmo trabalho. Em qualquer implementação, siga a skill de projeto `fleet-development` (`.claude/skills/fleet-development/SKILL.md`).

Controle de Frota é um sistema de gestão de frotas multiempresa em .NET 8 + React. A fase atual e o escopo estão em `docs/ROADMAP.md` (Fases 1 — Fundação, 2 — Controle operacional, 3 — Manutenção, 4 — Combustível, 5 — Pneus e 6 — Financeiro concluídas; **fase final em andamento, em etapas A–E** — A (alertas/automação) e B (relatórios/análises) concluídas, ver ROADMAP; a Fase 2.5 — Viagens e composição foi adiada por decisão do usuário, ADR-026/ADR-031/ADR-035, e é a próxima recomendada). **Não implemente módulos de fases futuras** (viagens, almoxarifado, rastreamento…); apenas deixe o ponto de extensão (normalmente um novo valor em `OperationalEventType`).

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

## Combustível (Fase 4) em uma tela

- Módulo: `Fleet.Domain/Fuel` (entidades + `FuelRules.cs`), `Fleet.Application/Fuel` (`FuelCatalogServices`, `FuelingService`, `FuelConsumptionService`, `FuelAnalyticsService`), `Api/Controllers/FuelControllers.cs`, `Persistence/Configurations/FuelConfigurations.cs`, `Persistence/DevFuelSeeder.cs`; frontend em `features/fuel/` (+ `lib/fuel.ts`, `components/ColumnChart.tsx`).
- `FuelType` é o **catálogo** de produtos (por empresa); o enum do motor do veículo é `VehicleFuelType` (ADR-031). Não confunda os dois.
- Regras puras no Domain e únicas fontes: `FuelingAmounts` (total = round(qtd × preço, 2) — o total do cliente só é conferido), `FuelingWorkflow`, `ConsumptionCalculator` (tanque cheio a tanque cheio), `ConsumptionBaseline` (configurado > histórico do veículo > tipo), `FuelAnomalyRules`, `FuelCompatibility`. O frontend espelha só o que precisa para feedback (`lib/fuel.ts`).
- **Hodômetro tem fonte única**: o abastecimento passa pelo `MileageService` (`AddReadingAsync` com `Source=Fueling`/`FuelingId`; `EnsureFitsHistoryAsync` para lançamento tardio; `ApprovePendingAsync`/`RejectPending` dentro da transação). Nunca escreva `Vehicle.CurrentOdometerKm` a partir do combustível.
- O consumo é um **snapshot** gravado no abastecimento que fecha o trecho (ADR-032). `FuelConsumptionService.RecalculateAsync` roda **depois** do `SaveChanges` (lê a cadeia do banco), dentro de `InTransactionAsync`, e só recalcula os dois primeiros tanques cheios a partir do ponto alterado. Colunas de trecho ficam `NULL` fora de `Calculated`.
- Status do abastecimento é derivado dos alertas (`Fueling.RefreshStatus`): algum alerta sem revisão = `PendingReview`. Nada é excluído: corrigir (com `FuelingCorrection`) ou cancelar.
- Dinheiro só com `fuel.viewcosts` **ou** sendo o autor do registro (ADR-034), decidido no serviço. Resumos de eventos e mensagens de alerta de preço não levam R$.
- `FueledAt` é gravado em segundos inteiros (o valor faz ida e volta pelo JSON/JS a cada correção); `FueledOn` é a data de negócio usada em filtros e agrupamentos.

## Pneus (Fase 5) em uma tela

- Módulo: `Fleet.Domain/Tires` (`TireCatalog.cs`, `Tire.cs`, `TireRules.cs`), `Fleet.Application/Tires` (`TireLifecycle` é o apoio comum de toda operação; `TireOperationsService` instala/remove/substitui/transfere/rodízio/baixa; `TireInspectionService`; `TireServiceOrderService` consertos/recapagens/custos; `TireService` cadastro e leituras; `TireAnalyticsService`), `Api/Controllers/TireControllers.cs`, `Persistence/Configurations/TireConfigurations.cs`, `Persistence/DevTireSeeder.cs`; frontend em `features/tires/` (+ `lib/tires.ts`). Testes: `TireServices` (builders com contexto opcional) e `TireTestBase`.
- **Posições não são tabela**: são geradas da configuração de eixos (`TirePositions.For`; espelho `lib/tires.previewPositions`). A vigência guarda código + rótulo da posição (snapshot).
- **Um pneu, uma posição — no banco**: índices únicos filtrados em `TireInstallations` e token `Tires.Version` (incremente com `TireLifecycle.Touch`). Liberar e ocupar posição na mesma operação = dois `SaveChanges` em `TireLifecycle.RunAsync` (transação + tradução para 409).
- Km do pneu vem do `MileageService` (`TireLifecycle.OdometerAsync`); salto suspeito recusa a operação. Estepe soma 0; implemento = km desconhecido (`HasUnmeasuredDistance`), e o custo/km some (`TireCostPolicy`, mínimo 5.000 km).
- Medição nunca sobrescreve: toda medição é uma `TireInspection` (inclusive na remoção e no retorno da recapagem); os campos rápidos do pneu seguem só a mais recente (`TireMonitoring.RecordAsync`).
- Alertas são calculados (`TireAlertPolicy` + forma SQL em `TireService.WhereAlert` — mude as duas juntas); "requer revisão" é gravado (`TireAnomaly`, um aberto por tipo). Textos: "configurado pela empresa", nunca "legal", nunca causa.
- Valores em R$ só com `tires.viewcosts`; digitar valor também exige a permissão. Eventos `Tire*` levam `TireId` (linha do tempo do pneu) e não levam R$.

## Financeiro (Fase 6) em uma tela

- Módulo: `Fleet.Domain/Finance` (entidades + `FinanceRules.cs`: `ExpensePaymentPolicy`, `RecurringExpensePolicy`, `VehicleCostPolicy`, `BudgetAnalysis`), `Fleet.Application/Finance` (`FinanceCatalogServices` — centros de custo e categorias; `ExpenseService`; `RecurringExpenseService` + `RecurringExpenseGenerationScanner`; `BudgetService`; `CostAggregationService`; `FinanceAnalyticsService`), `Api/Controllers/FinanceControllers.cs`, `Api/Infrastructure/RecurringExpenseGenerationJob.cs`, `Persistence/Configurations/FinanceConfigurations.cs`; frontend em `features/finance/` (rotas `/financeiro/...`: Despesas, Categorias, Centros de custo, Recorrentes, Orçamentos, Ranking de veículos, Relatórios) + aba "Financeiro" no hub do veículo.
- **`Expense` nunca recebe custo de combustível/manutenção/pneu** — `CostAggregationService` lê `Fuelings.TotalAmount`/`WorkOrders.TotalCost`/`TireCosts.Amount` direto, agrupados no banco. As três categorias de sistema (`ExpenseCategory.IsSystemCategory`, `CostAggregationKey` `Fuel`/`Maintenance`/`Tires`) só rotulam essas fatias nos relatórios — `ExpenseService` recusa lançamento manual nelas (ADR-040).
- `PaymentStatus` nunca é gravado: `ExpensePaymentPolicy.Evaluate` calcula na leitura (`Cancelled` > `Paid` > `PartiallyPaid` > `Overdue` > `Scheduled` > `Pending`). **Despesa nunca é excluída, só cancelada** (motivo obrigatório, estado final); reduzir o valor já pago é recusado.
- `RecurringExpenseGenerationScanner` (job em background, mesmo molde do `DocumentExpirationScanner`) gera despesas com até **30 dias de antecedência** do vencimento, avançando o cursor `LastGeneratedDueDate`; idempotente por índice único `(RecurringExpenseId, DueDate)`.
- **`finance.viewcosts` controla qualquer tela 100% monetária do módulo**; um total que combina módulos (painel, custo do veículo) exige essa permissão **e** o `*.viewcosts` de cada fonte somada — faltando uma, a fatia zera e a resposta marca `IsPartial` (ADR-043/044).
- Custo/km exige ≥ 50 km no período (`VehicleCostPolicy.MinKmForCostPerKm`) com leitura de hodômetro cobrindo as duas pontas (`MileageService.OdometerAtAsync`); sem isso, `null` com motivo — nunca um número enganoso.

## Alertas e automação (fase final, etapa A) em uma tela

- Módulo: `Fleet.Domain/Intelligence` (`FleetAlert`, `AutomationRule`, `AutomationExecution`, `UserNotification`, `FleetAlertWorkflow`, `AlertPriority`, `AutomationTriggerCatalog`, `AlertAudiences`), `Fleet.Application/Intelligence` (`AlertDetectors.cs` — um `IAlertDetector` por gatilho; `AutomationEngine`; `AutomationRuleService`; `FleetAlertService` + `NotificationService`; `AttentionService`), `Api/Controllers/IntelligenceControllers.cs`, `Api/Infrastructure/AutomationJob.cs` (+ `AutomationRunner`), `Api/Authorization/SystemAwareCurrentUser.cs`; frontend em `features/alerts/` (central `/alertas`, detalhe, regras `/configuracoes/automacoes`, `NotificationBell`, `AttentionPanel`).
- **Alerta novo = gatilho + detector, nunca um job ou bloco de painel novo** (ADR-045). O detector reaproveita a política/serviço do módulo e devolve `AlertCandidate` com chave de deduplicação estável, público (`AlertAudience`), explicação, base numérica e sugestão. O motor cuida de criar/atualizar/encerrar, recorrência, silêncio pós-descarte e notificações.
- **Público do alerta** decide quem vê (filtro no SQL). Texto com R$ → `AlertAudience.FleetCosts`. Notificação nunca leva R$.
- Jobs que precisam de serviços completos agem como sistema de uma empresa por vez: escopo novo + `SystemExecutionContext.ActAsSystemFor(companyId)` (ADR-046). Nunca ative isso numa requisição.
- O outbox `OperationalEvents` agora tem consumidor: o motor marca `PublishedAt`. Canal novo (e-mail/WhatsApp) = ação nova da regra, não outro leitor.
- `FleetAlert` não é auditável (atualizado a cada verificação); o rastro fica nos campos `ReadBy/AssignedToUserId/ClosedBy`.

## Análises cruzadas (fase final, etapa B) em uma tela

- `Fleet.Application/Analytics`: **`VehicleMetricsService` é a única fonte das métricas por veículo** (km, consumo, custos por fatia, OS, tempo parado, trocas de pneu) usadas por relatórios, comparação, médias e destaques (ADR-047). Métrica nova por veículo = campo novo lá, nunca um cálculo paralelo numa tela.
- Campos de módulos sem permissão = `null`; custo/km só com total completo e km confiável (leitura antes do período). Tendência = `TrendAnalysis` (base e variação mínimas). Saúde = `VehicleHealthPolicy`.
- Exportação: `ReportTable exportAs={{ title, load: pagedLoader(url, params, select?) }}` — mesmo endpoint e filtros da tela (ADR-048). Colunas sem `exportValue` exportam o texto da célula.
- Histórico (`OperationalHistoryService`) filtra cada evento pela permissão do seu módulo (`EventAudience`, ADR-049): tipo de evento novo precisa cair no público certo.
- Busca global (`GlobalSearchService`): tipo novo pesquisável = bloco novo com a permissão da lista do módulo.

## Regras que não são óbvias pelo código

- Autorização **só por permissão**, nunca pelo nome do papel. Anti-escalonamento: ninguém atribui um papel com permissão que ele próprio não tem.
- ID de outra empresa responde **404**, nunca 403. Não use `IgnoreQueryFilters()` sem um comentário justificando.
- `User` **não** é `ITenantScoped`, porque o login precisa encontrá-lo antes de saber o tenant. O `UserService.ScopedUsers()` filtra manualmente.
- Nunca preencha `CompanyId`, `CreatedAt`, `UpdatedAt` ou `DeletedAt` à mão.
- Índices únicos são filtrados por `[DeletedAt] IS NULL`. A placa é única por empresa **entre veículos e implementos** (`RegisteredAssetRules`).
- Enums são gravados como texto e datas com hora usam `DateTime` UTC. No SQLite dos testes, `decimal` é gravado como REAL (ADR-033) para permitir `SUM/MIN/MAX` no banco — mas **some colunas, não expressões** (`SUM(a/b)`, `SUM(x ?? 0)` e casts não traduzem no SQLite). Consulta agregada nova: rode uma vez no SQL Server (LocalDB, com um banco temporário via `ConnectionStrings__Fleet`) antes de dar por pronta.
- `Id` (GUID sequencial) **não é um desempate de ordenação confiável** nos testes: o gerador é sequencial para a ordenação especial do SQL Server, mas o SQLite dos testes compara os bytes em ordem simples. Quando duas linhas empatam na coluna principal (ex. dois registros no mesmo instante), desempate por algo com significado de negócio (ex. "ativo primeiro"), não por `Id`.
- No frontend, o access token fica só em memória. O refresh token é um cookie HttpOnly rotativo: se um token antigo for reutilizado, todas as sessões do usuário são revogadas.
- Os validadores do frontend (`src/lib/validators.ts`) espelham `Fleet.Domain/Validation`. Ao mudar um, mude o outro e os testes dos dois.
- Status e labels em pt-BR ficam num único mapa por módulo (`features/<modulo>/<modulo>.ts`). Nada de string de status solta.
- Decisões pendentes ficam em "Pontos em aberto" de `docs/DECISIONS.md`; não decida em silêncio.
- A revisão visual automatizada com Edge/Chromium headless travou esta máquina (2026-09-29). Não rode browser headless aqui sem combinar antes com o usuário.
