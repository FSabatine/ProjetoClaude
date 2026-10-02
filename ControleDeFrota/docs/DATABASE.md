# Banco de dados

## Tecnologia

- **SQL Server** (produção) / **SQL Server LocalDB** (desenvolvimento, `(localdb)\MSSQLLocalDB`, banco `ControleDeFrota`).
- **EF Core 8**, com o modelo definido em código (code-first). Uma `IEntityTypeConfiguration<T>` por entidade, em `Fleet.Infrastructure/Persistence/Configurations/`.
- Os testes automatizados usam **SQLite em memória** (ADR-004). Por isso o modelo evita recursos que o SQLite não traduz: não há `DateTimeOffset` (usamos `DateTime` UTC) e não há ordenação por colunas `decimal`. Desde a Fase 4, `decimal` é gravado como REAL **só no SQLite** para permitir agregações no banco (ADR-033) — some colunas, não expressões calculadas.

## Estratégia de migrations

- Migrations em `Fleet.Infrastructure/Persistence/Migrations`, geradas com:
  ```bash
  dotnet ef migrations add <NomeEmPascalCase> --project src/Fleet.Infrastructure --startup-project src/Fleet.Api
  ```
- **Development**: a API aplica as migrations pendentes e executa o seed de desenvolvimento ao subir (`Database:MigrateOnStartup=true`, somente em `appsettings.Development.json`).
- **Produção**: migrations **nunca** rodam no startup. Aplicar com `dotnet ef database update` ou com um bundle (`dotnet ef migrations bundle`), num passo explícito do deploy e com backup antes.
- Uma migration aplicada em ambiente compartilhado nunca é editada. Correções entram numa nova migration.
- Todo PR com migration deve conferir o SQL gerado (`dotnet ef migrations script`) para evitar perda de dados.

## Convenções de nomes

| Item | Convenção | Exemplo |
|---|---|---|
| Tabela | PascalCase, plural, em inglês | `Vehicles`, `RolePermissions` |
| Coluna | PascalCase, em inglês | `LicensePlate`, `CreatedAt` |
| PK | `Id` | |
| FK | `<Entidade>Id` | `CompanyId` |
| Índice | `IX_<Tabela>_<Colunas>` (padrão EF) | `IX_Vehicles_CompanyId_LicensePlate` |
| Booleano | prefixo `Is`/`Has` ou verbo | `IsActive`, `PerformsPaidActivity` |
| Datas UTC com hora | sufixo `At` | `CreatedAt`, `LockoutEndAt` |
| Datas sem hora | sufixo `On` ou nome do evento (`date`) | `LicenseExpiresOn`, `BirthDate` |
| Enums | gravados como **texto** (`nvarchar`) | `Status = 'UnderMaintenance'` |

**Enums como texto** (ADR-008): o banco fica legível para relatórios e suporte, e reordenar valores no código não corrompe dados. O custo de espaço é irrelevante no volume esperado.

**Documentos brasileiros** (CPF, CNPJ, CEP, telefone, placa, RENAVAM) são gravados **normalizados**, só com dígitos ou em caixa alta sem separadores. A máscara é aplicada apenas na interface.

## Tipos de chave

- **Entidades de negócio**: `uniqueidentifier` (GUID), gerado pelo EF de forma sequencial (`SequentialGuidValueGenerator`, amigável ao índice clusterizado do SQL Server). IDs não sequenciais-adivinháveis dificultam enumeração (IDOR).
- **Catálogos** (`Roles`, `Permissions`): `int`, com valores fixos no seed.
- **AuditLogs**: `bigint identity`, por ser de alto volume e apenas inserção.

## Colunas padrão

Toda entidade de negócio herda de `AuditableEntity`:

| Coluna | Tipo | Preenchida por |
|---|---|---|
| `CreatedAt` | `datetime2` (UTC) | `SaveChanges` |
| `CreatedBy` | `uniqueidentifier NULL` | `SaveChanges` (usuário corrente; nulo para seed/sistema) |
| `UpdatedAt` | `datetime2 NULL` (UTC) | `SaveChanges` em toda alteração |
| `UpdatedBy` | `uniqueidentifier NULL` | `SaveChanges` |

Entidades com soft delete (`ISoftDeletable`) têm também `DeletedAt` e `DeletedBy`.

## Soft delete

- Implementado com `DeletedAt IS NULL` = registro vivo.
- Um `Remove()` em entidade `ISoftDeletable` é convertido em `UPDATE` pelo `SaveChanges`. **Nenhum código de módulo seta `DeletedAt` à mão.**
- Um filtro global do EF oculta os excluídos em todas as consultas. `IgnoreQueryFilters()` só pode ser usado em código administrativo/auditoria, com comentário explicando o motivo.
- Os **índices únicos são filtrados** (`WHERE [DeletedAt] IS NULL`). Assim, uma placa ou CPF de um registro excluído pode ser cadastrado de novo.
- Aplicado a: `Companies`, `Users`, `Drivers`, `Vehicles`, `Implements` e, na Fase 2, `DocumentTypes`, `Documents`, `StoredFiles` e `ChecklistTemplates`. Não se aplica a catálogos, `RefreshTokens`, `AuditLogs` nem aos **registros históricos** (alocações, leituras, execuções de checklist, ocorrências, eventos), que nunca são excluídos: são encerrados, rejeitados ou cancelados.
- Excluir ≠ inativar. **Inativar** (status) é o fluxo normal de negócio: o registro continua visível e reportável. **Excluir** é para cadastro indevido ou duplicado.

## Auditoria

Tabela `AuditLogs`, preenchida automaticamente pelo `SaveChanges` para entidades `IAuditable`:

| Coluna | Descrição |
|---|---|
| `Id` | bigint identity |
| `CompanyId` | tenant do registro (nulo para `Company`) |
| `UserId` | quem alterou (nulo = sistema/seed) |
| `EntityName` | ex.: `Vehicle` |
| `EntityId` | id do registro alterado |
| `Action` | `Created`, `Updated`, `Deleted` |
| `Changes` | JSON `{ "Campo": { "old": x, "new": y } }` com apenas os campos alterados |
| `OccurredAt` | UTC |
| `TraceId` | correlação com o log da requisição |

Responde a *quem, o quê, quando, valor anterior e valor novo*. Campos sensíveis (`PasswordHash`) nunca entram no JSON: aparece apenas `"PasswordHash": "***"` quando mudam. `AuditLogs` não tem FKs de propósito, para que o histórico sobreviva à exclusão física eventual de dados.

## Histórico de dados

- `AuditLogs` guarda o histórico técnico de alterações (quem mudou qual campo).
- O histórico **de negócio** vive em tabelas próprias desde a Fase 2: `VehicleAssignments` (vigência `StartedAt`/`EndedAt`), `OdometerReadings`, `ChecklistExecutions` e `Occurrences`. A linha do tempo consolidada é a tabela `OperationalEvents` (ADR-025). O valor atual (`Vehicle.CurrentOdometerKm`) continua em `Vehicles` como leitura rápida.
- O vínculo veículo ↔ implemento com vigência continua planejado para uma fase futura.

## Entidades e relacionamentos (Fase 1)

```
Companies 1───N Users N───N Roles N───N Permissions
    │             (UserRoles)      (RolePermissions)
    │           Users 1───N RefreshTokens
    ├──1───N Drivers
    ├──1───N Vehicles
    └──1───N Implements
AuditLogs (sem FKs)
```

### Companies
| Coluna | Tipo | Regras |
|---|---|---|
| Id | uniqueidentifier PK | |
| LegalName | nvarchar(200) | obrigatório (razão social) |
| TradeName | nvarchar(200) NULL | nome fantasia |
| Cnpj | char(14) | obrigatório, dígito verificador válido (formato numérico **e alfanumérico**, IN RFB 2.229/2024), único entre não excluídas |
| StateRegistration | nvarchar(20) NULL | IE ou `ISENTO` |
| Email | nvarchar(254) NULL | |
| Phone | varchar(11) NULL | 10 ou 11 dígitos |
| Street, Number, Complement, Neighborhood, City | nvarchar | endereço (value object `Address`) |
| State | char(2) | UF válida |
| ZipCode | char(8) | CEP |
| IsActive | bit | uma empresa inativa bloqueia o login dos seus usuários |
| auditoria + soft delete | | |

### Users
| Coluna | Tipo | Regras |
|---|---|---|
| Id | uniqueidentifier PK | |
| CompanyId | FK → Companies | obrigatório, `Restrict` |
| Name | nvarchar(150) | |
| Email | nvarchar(254) | normalizado em minúsculas, **único global** entre não excluídos (login) |
| PasswordHash | nvarchar(500) | PBKDF2 (ASP.NET Core Identity v3) |
| Status | nvarchar(20) | `Active`, `Inactive` |
| FailedLoginCount | int | bloqueio após N falhas |
| LockoutEndAt | datetime2 NULL | |
| LastLoginAt | datetime2 NULL | |
| PasswordChangedAt | datetime2 NULL | |
| auditoria + soft delete | | |

### Roles / Permissions / RolePermissions / UserRoles
- `Roles(Id int, Key varchar(50) único, Name, Description, IsSystem bit, CompanyId NULL)`. `CompanyId` nulo = papel do sistema. A coluna existe para suportar papéis personalizados por empresa no futuro, sem migration de ruptura.
- `Permissions(Id int, Key varchar(100) único, Module varchar(50), Description)`.
- `RolePermissions(RoleId, PermissionId)` com PK composta. `UserRoles(UserId, RoleId)` com PK composta.
- Catálogo e papéis do sistema são semeados via `HasData`, a partir de `Fleet.Domain/Authorization`.

### RefreshTokens
`Id, UserId (FK Cascade), TokenHash char(64) único (SHA-256), CreatedAt, ExpiresAt, RevokedAt NULL, ReplacedByTokenHash NULL, CreatedByIp`. O token em si nunca é gravado, apenas o hash.

### Drivers
| Coluna | Tipo | Regras |
|---|---|---|
| CompanyId | FK | tenant |
| FullName | nvarchar(150) | obrigatório |
| Cpf | char(11) | DV válido; único por empresa |
| Rg | nvarchar(20) NULL | |
| BirthDate | date | idade entre 18 e 100 anos |
| Phone, Email | NULL | |
| Endereço | (Address) NULL | opcional |
| LicenseNumber | varchar(11) | CNH (registro, 11 dígitos); único por empresa |
| LicenseCategory | nvarchar(2) | `A, B, C, D, E, AB, AC, AD, AE` |
| LicenseExpiresOn | date | validade da CNH |
| PerformsPaidActivity | bit | EAR, "Exerce Atividade Remunerada" |
| Status | nvarchar(20) | `Active`, `OnLeave`, `Inactive` |
| Notes | nvarchar(2000) NULL | |

### Vehicles
| Coluna | Tipo | Regras |
|---|---|---|
| CompanyId | FK | tenant |
| LicensePlate | char(7) | formato antigo `ABC1234` ou Mercosul `ABC1D23`; único por empresa (inclusive frente a `Implements`) |
| Renavam | char(11) | DV válido; único por empresa |
| Chassis | varchar(17) | VIN com 17 caracteres, sem I/O/Q; único por empresa |
| Manufacturer, Model | nvarchar | obrigatórios |
| ManufacturingYear, ModelYear | smallint | modelo = fabricação ou fabricação + 1 |
| Color | nvarchar(30) NULL | |
| Type | nvarchar(30) | `Truck, TruckTractor, Van, Pickup, Car, Motorcycle, Bus, Other` |
| Category | nvarchar(30) NULL | `Light, Medium, SemiHeavy, Heavy` (ver DECISIONS, ponto em aberto) |
| FuelType | nvarchar(20) | `DieselS10, DieselS500, Gasoline, Ethanol, Flex, Cng, Electric, Hybrid, Other` |
| CargoCapacityKg, TareWeightKg | decimal(12,2) NULL | ≥ 0 |
| CurrentOdometerKm | int | ≥ 0 |
| HourMeter | decimal(10,1) NULL | ≥ 0 |
| Status | nvarchar(30) | `Available, OnTrip, UnderMaintenance, Inactive` (ADR-009) |
| AcquisitionDate | date NULL | não futura |
| AcquisitionValue | decimal(18,2) NULL | ≥ 0 |
| Notes | nvarchar(2000) NULL | |

### Implements
Como `Vehicles` (placa, RENAVAM, chassi, fabricante, modelo, anos), com:
`Type` (`Trailer, SemiTrailer, Tanker, BoxBody, Sider, Dolly, Other`), `Capacity decimal(12,2) NULL` + `CapacityUnit` (`Kg, Liters, CubicMeters`), `TareWeightKg`, `Status` (`Available, InUse, UnderMaintenance, Inactive`), `Notes`.

## Índices importantes

| Tabela | Índice | Tipo |
|---|---|---|
| Companies | `Cnpj` | único filtrado |
| Users | `Email` | único filtrado |
| Users | `CompanyId` | |
| Drivers | `(CompanyId, Cpf)`, `(CompanyId, LicenseNumber)` | únicos filtrados |
| Drivers | `(CompanyId, Status)`, `(CompanyId, LicenseExpiresOn)` | alertas de CNH |
| Vehicles | `(CompanyId, LicensePlate)`, `(CompanyId, Renavam)`, `(CompanyId, Chassis)` | únicos filtrados |
| Vehicles | `(CompanyId, Status)` | dashboard |
| Implements | `(CompanyId, LicensePlate)`, `(CompanyId, Renavam)`, `(CompanyId, Chassis)` | únicos filtrados |
| Implements | `(CompanyId, Status)` | |
| RefreshTokens | `TokenHash` único, `UserId` | |
| AuditLogs | `(EntityName, EntityId, OccurredAt)`, `(CompanyId, OccurredAt)` | histórico |

## O que fica no banco e o que fica no código

- **No banco**: integridade estrutural, ou seja, PK, FK, NOT NULL, tamanho de colunas e unicidade (última linha de defesa contra concorrência).
- **No código (Application)**: regras de negócio, como formatos e dígitos verificadores, faixas de datas, transições de status, "placa não pode existir em outro veículo nem implemento" e permissões. O serviço também checa duplicidade antes de gravar para devolver uma mensagem amigável (409). O índice único cobre a corrida entre duas requisições simultâneas.


## Entidades e relacionamentos (Fase 2 — migration `OperationalControl`)

```
Vehicles 1───N VehicleAssignments N───1 Drivers      (índice único filtrado: 1 ativa por veículo e por motorista)
Vehicles 1───N OdometerReadings N───0..1 ChecklistExecutions
Companies 1───N DocumentTypes 1───N Documents ──0..1 Vehicles | Drivers | Implements   (CK_Documents_Owner)
Companies 1───N ChecklistTemplates 1───N ChecklistTemplateItems (cascade)
Vehicles 1───N ChecklistExecutions 1───N ChecklistAnswers (cascade, snapshot) ──0..1 Occurrences
Vehicles/Drivers/Implements 1───N Occurrences N───0..1 ChecklistExecutions
StoredFiles (OwnerType + OwnerId → Document | Occurrence | ChecklistAnswer; sem FK polimórfica)
OperationalEvents (sem FKs, como AuditLogs)
```

Todas as tabelas novas (salvo itens de modelo e respostas, que pertencem ao pai) têm `CompanyId` com FK `Restrict` para `Companies`, filtro global de tenant e índices começando por `CompanyId`. As FKs para veículo, motorista e implemento são `Restrict`: histórico nunca some em cascata.

### Vehicles (alterações)
| Coluna | Tipo | Regras |
|---|---|---|
| Status | nvarchar(30) | + `Unavailable` (ADR-018) |
| OdometerUpdatedAt | datetime2 NULL | data da última leitura aplicada (ADR-019). Índice `(CompanyId, OdometerUpdatedAt)` para "sem leitura recente" |

### VehicleAssignments
| Coluna | Tipo | Regras |
|---|---|---|
| VehicleId, DriverId | FK Restrict | |
| StartedAt | datetime2 | não futura |
| EndedAt | datetime2 NULL | nulo = ativa; ≥ StartedAt |
| Notes, EndReason | nvarchar(500) NULL | |
| auditoria | | sem soft delete (alocação errada é encerrada) |

Índices: `UX_VehicleAssignments_ActiveVehicle (VehicleId) WHERE EndedAt IS NULL` e `UX_VehicleAssignments_ActiveDriver (DriverId) WHERE EndedAt IS NULL` (únicos); `(CompanyId, VehicleId, StartedAt)` e `(CompanyId, DriverId, StartedAt)` para o histórico.

### OdometerReadings
| Coluna | Tipo | Regras |
|---|---|---|
| VehicleId | FK Restrict | |
| OdometerKm | int | 0 a 9.999.999 |
| ReadAt | datetime2 | não futura; ≥ última leitura (exceto correção) |
| Source | nvarchar(20) | `Registration, Manual, Checklist, Correction` |
| Status | nvarchar(20) | `Valid, PendingReview, Rejected` |
| Anomaly | nvarchar(300) NULL | motivo da suspeita |
| Notes, ReviewNotes | nvarchar(500) NULL | motivo da correção / justificativa da revisão |
| ReviewedAt, ReviewedBy | NULL | |
| ChecklistExecutionId | FK NULL | leitura feita no checklist |

Índices: `(CompanyId, VehicleId, ReadAt)` (histórico e linha de base), `(CompanyId, Status)` (pendentes de revisão). Append-only: nunca editada nem excluída.

### DocumentTypes
`Name nvarchar(80)`, `OwnerType nvarchar(20)`, `HasExpiration bit`, `AlertDaysBefore int (0–365)`, `IsActive bit`, auditoria + soft delete. Único filtrado `(CompanyId, OwnerType, Name)`.

### Documents
| Coluna | Tipo | Regras |
|---|---|---|
| DocumentTypeId | FK Restrict | |
| OwnerType | nvarchar(20) | `Vehicle, Driver, Implement, Company` |
| VehicleId / DriverId / ImplementId | FK NULL Restrict | exatamente a do `OwnerType` (check `CK_Documents_Owner`); nenhuma para `Company` |
| Number | nvarchar(60) NULL | |
| IssuedOn, ExpiresOn | date NULL | ver regras em DOMAIN.md |
| AlertStartsOn | date NULL | `ExpiresOn − AlertDaysBefore` do tipo (derivado, fora da auditoria) |
| Notes | nvarchar(1000) NULL | |
| ReplacedAt, ReplacedByDocumentId | NULL | renovação |
| LastAlertedStatus | nvarchar(20) NULL | controle do job de vencimentos (fora da auditoria) |
| auditoria + soft delete | | |

Índices: `(CompanyId, AlertStartsOn)` (alertas), `(CompanyId, ExpiresOn)` (ordenação), `(CompanyId, VehicleId|DriverId|ImplementId|DocumentTypeId)`.

### StoredFiles
`FileName nvarchar(200)` (sanitizado), `ContentType varchar(100)` (detectado pelo conteúdo), `SizeBytes bigint`, `StorageKey varchar(200)` único (gerado pelo servidor), `OwnerType nvarchar(30) NULL`, `OwnerId uniqueidentifier NULL`, auditoria + soft delete. Índice `(CompanyId, OwnerType, OwnerId)`. **Os bytes não ficam no banco** (ADR-022).

### ChecklistTemplates / ChecklistTemplateItems
- Templates: `Name nvarchar(100)` (único filtrado por empresa), `Description`, `Frequency`, `IsActive`, `Version int`, auditoria + soft delete.
- Itens: `TemplateId` (cascade), `Position`, `Section nvarchar(60)`, `Label nvarchar(200)`, `ResponseType`, `IsRequired`, `Unit nvarchar(20)`, `RequiresPhotoOnFail`, `FailureOccurrenceType`, `FailureSeverity`. A mudança nos itens aparece na auditoria como a nova `Version` do modelo.

### ChecklistExecutions / ChecklistAnswers
- Execuções: `VehicleId`, `DriverId NULL`, `TemplateId` (FK Restrict) + snapshot `TemplateName`, `TemplateVersion`, `Frequency`; `PerformedAt` (UTC) e `PerformedOn` (data de negócio, para "feito hoje?" sem conta de fuso na consulta), `OdometerKm NULL`, `Result`, `FailedItems`, `Location`, `Notes`, auditoria. Imutáveis.
- Respostas: `ExecutionId` (cascade), `TemplateItemId` + **snapshot** (`Position, Section, Label, ResponseType, IsRequired, Unit`), `Choice`, `NumberValue decimal(12,2)`, `TextValue nvarchar(500)`, `Comment`, `Severity`, `OccurrenceId` (FK NULL).
- Índices: `(CompanyId, VehicleId, PerformedAt)`, `(CompanyId, DriverId, PerformedAt)`, `(CompanyId, TemplateId, PerformedOn)` (pendentes), `(CompanyId, PerformedAt)`.

### Occurrences
`VehicleId/DriverId/ImplementId` (FK NULL, ao menos um), `Type`, `Severity`, `OccurredAt`, `Location nvarchar(200)`, `Description nvarchar(2000)`, `Status`, `Resolution nvarchar(2000)`, `ClosedAt/ClosedBy`, `Source` (`Manual`/`Checklist`), `ChecklistExecutionId` (FK NULL), auditoria. Sem soft delete (registro indevido é cancelado). Índices: `(CompanyId, Status, OccurredAt)`, `(CompanyId, VehicleId, OccurredAt)`, `(CompanyId, DriverId, OccurredAt)`, `(CompanyId, OccurredAt)`.

### OperationalEvents (ADR-025)
| Coluna | Tipo | Descrição |
|---|---|---|
| Id | bigint identity | ordem de inserção (outbox) |
| CompanyId | uniqueidentifier | tenant |
| Type | nvarchar(40) | catálogo em DOMAIN.md |
| OccurredAt | datetime2 | UTC |
| UserId | NULL | nulo = sistema (job) |
| VehicleId, DriverId, ImplementId | NULL | timelines onde o evento aparece |
| SubjectType, SubjectId | varchar(50), uniqueidentifier | registro de origem |
| Summary | nvarchar(300) | frase pt-BR congelada |
| Data | nvarchar(max) | JSON (ids e valores, sem dados pessoais além de ids) |
| PublishedAt | datetime2 NULL | outbox das notificações futuras |

Sem FKs (o histórico sobrevive a qualquer limpeza). Índices: `(CompanyId, VehicleId, OccurredAt)`, `(CompanyId, DriverId, OccurredAt)`, `(CompanyId, Type, OccurredAt)` e `IX_OperationalEvents_Unpublished (Id) WHERE PublishedAt IS NULL`.

### Seed
- Permissões 100–150 e o mapeamento dos papéis via `HasData` (a migration faz `InsertData` em `Permissions`/`RolePermissions` e atualiza as descrições dos papéis).
- Tipos de documento padrão: criados com cada empresa nova (`CompanyService`) e, para empresas antigas, na primeira leitura (`DocumentTypeService.EnsureDefaultsAsync`). Não há `InsertData` por empresa na migration.
- Desenvolvimento: `DevDataSeeder.SeedOperationsAsync` (idempotente) adiciona usuários de operação e manutenção, o modelo "Inspeção diária", alocações, leituras (uma delas suspeita), documentos em todos os estados e uma ocorrência aberta.

### Revisão da migration `OperationalControl`
O `Up` é **somente aditivo**: 11 tabelas novas, a coluna `Vehicles.OdometerUpdatedAt` (nullable), um índice em `Vehicles` e o seed de permissões. Não há alteração nem remoção de coluna existente. O aviso "may result in the loss of data" do `dotnet ef` refere-se ao `Down`, que remove as tabelas novas.

## Entidades e relacionamentos (Fase 3 — migration `Maintenance`)

```
Companies 1───N Workshops
Companies 1───N MaintenancePlans 1───N MaintenancePlanItems (cascade)
Vehicles 1───N HourMeterReadings                              (append-only, como OdometerReadings)
Vehicles 1───N MaintenanceSchedules N───1 MaintenancePlanItems (única por VehicleId+MaintenancePlanItemId)
Vehicles 1───N MaintenanceRequests N───0..1 Occurrences, N───0..1 WorkOrders
Vehicles 1───N WorkOrders N───0..1 (Implements | MaintenanceRequests | Workshops)
WorkOrders 1───N WorkOrderItems (cascade) N───0..1 MaintenancePlanItems
WorkOrders 1───N WorkOrderParts (cascade)
WorkOrders 1───N WorkOrderLabor (cascade)
```

Todas as tabelas novas têm `CompanyId` com FK `Restrict` para `Companies` (exceto as filhas de agregado, que não têm `CompanyId` próprio), filtro global de tenant onde aplicável e índices começando por `CompanyId`. FKs para veículo/implemento/oficina/ocorrência/solicitação são `Restrict` — histórico nunca some em cascata; só as coleções realmente filhas (`MaintenancePlanItems`, `WorkOrderItems/Parts/Labor`) são `Cascade`.

### Vehicles (alterações)
| Coluna | Tipo | Regras |
|---|---|---|
| HourMeterUpdatedAt | datetime2 NULL | data da última leitura de horímetro aplicada (como `OdometerUpdatedAt`) |

### Workshops
`Name nvarchar(150)`, `Document varchar(20) NULL`, `Phone varchar(11) NULL`, `Email nvarchar(254) NULL`, endereço (`Address`, owned type), `Specialties nvarchar(300) NULL`, `Status nvarchar(20)`, `Notes nvarchar(2000) NULL`, auditoria + soft delete.

### MaintenancePlans / MaintenancePlanItems
- Planos: `Name nvarchar(150)`, `VehicleId NULL` (FK Restrict), `VehicleType nvarchar(30) NULL`, `IsActive bit`, auditoria + soft delete. `VehicleId` e `VehicleType` nulos juntos = plano padrão da empresa.
- Itens (cascade): `ServiceName nvarchar(150)`, `IntervalKm/GraceKm int NULL`, `IntervalMonths/GraceDays int NULL`, `IntervalHours/GraceHours decimal(10,1) NULL`, `Priority nvarchar(20)`, `EstimatedDurationMinutes int NULL`, `EstimatedCost decimal(18,2) NULL`, `IsRequired bit`, `Notes nvarchar(1000) NULL`.

### HourMeterReadings
Mesmas colunas de `OdometerReadings`, trocando `OdometerKm int` por `Hours decimal(10,1)`. Índices: `(CompanyId, VehicleId, ReadAt)`, `(CompanyId, Status)`. Append-only.

### MaintenanceSchedules
`VehicleId`, `MaintenancePlanItemId` (FK Restrict), `LastPerformedOn date NULL`, `LastPerformedKm int NULL`, `LastPerformedHours decimal(10,1) NULL`, `LastWorkOrderId NULL` (FK Restrict), `NextDueOn date NULL`, `NextDueKm int NULL`, `NextDueHours decimal(10,1) NULL`. Índice único `(VehicleId, MaintenancePlanItemId)`; índices `(CompanyId, NextDueOn)` e `(CompanyId, NextDueKm)` para os contadores do dashboard.

### MaintenanceRequests
`VehicleId`, `DriverId NULL`, `Source/MaintenanceType/Priority nvarchar(20)`, `Description nvarchar(2000)`, `ReportedAt datetime2`, `OdometerKm int NULL`, `HourMeter decimal(10,1) NULL`, `OccurrenceId NULL` (FK Restrict), `Status nvarchar(20)`, `ReviewedAt/By NULL`, `RejectionReason nvarchar(1000) NULL`, `WorkOrderId NULL` (FK Restrict). Índices: `(CompanyId, Status, ReportedAt)`, `(CompanyId, VehicleId, ReportedAt)`.

### WorkOrders
`Sequence int` (gera o `Number` exibido, `OS-{Sequence:D6}`, calculado em memória — não é coluna), `VehicleId`, `ImplementId NULL`, `MaintenanceRequestId NULL`, `WorkshopId NULL` (todos FK Restrict), `Type/Priority/Status nvarchar(20)`, `OpenedAt/ScheduledAt/StartedAt/CompletedAt datetime2 NULL` (exceto `OpenedAt`, obrigatório), `OdometerKm int NULL`, `HourMeter decimal(10,1) NULL`, `Description/Diagnosis/Resolution/Notes nvarchar(2000)`, `CompletedBy NULL`, `PartsCost/LaborCost/OtherCost/TotalCost decimal(18,2)`, `DowntimeMinutes int NULL`. Índice único `(CompanyId, Sequence)`; índices `(CompanyId, Status, Priority)`, `(CompanyId, VehicleId, OpenedAt)`, `(CompanyId, OpenedAt)`.

### WorkOrderItems / WorkOrderParts / WorkOrderLabor
Filhas em cascade de `WorkOrders` (sem `CompanyId` próprio). Itens: `Description nvarchar(300)`, `MaintenancePlanItemId NULL` (FK Restrict), `IsRequired bit`, `Status nvarchar(20)`, `Notes nvarchar(500)`. Peças: `PartName nvarchar(150)`, `PartNumber nvarchar(60) NULL`, `Quantity/UnitCost decimal`, `Supplier nvarchar(150) NULL`, `Notes nvarchar(500) NULL` (`TotalCost` calculado em memória, não gravado). Mão de obra: `TechnicianName nvarchar(150)`, `Hours decimal(10,2)`, `HourlyRate decimal(18,2)`, `Description nvarchar(500) NULL` (`TotalCost` calculado).

### Seed
Permissões 160–165 e o mapeamento dos papéis via `InsertData`/`UpdateData` na própria migration `Maintenance` (sem `DevDataSeeder` dedicado ainda).

### Revisão da migration `Maintenance`
O `Up` é **somente aditivo**: 10 tabelas novas, a coluna `Vehicles.HourMeterUpdatedAt` (nullable) e o seed de permissões/papéis. Nenhuma coluna existente foi alterada ou removida. O aviso "may result in the loss of data" refere-se só ao `Down`.

## Entidades e relacionamentos (Fase 4 — migration `FuelManagement`)

```
Companies 1───N FuelTypes                (único CompanyId+Code e CompanyId+Name, filtrados)
Companies 1───N FuelStations 1───N FuelPrices N───1 FuelTypes
Companies 1───0..1 FuelSettings          (único CompanyId)
Vehicles  1───N Fuelings N───0..1 Drivers, N───0..1 FuelStations, N───1 FuelTypes
Fuelings  1───N FuelingAnomalies (cascade)
Fuelings  1───N FuelingCorrections (cascade)
Fuelings  1───N OdometerReadings (FuelingId NULL, FK Restrict)
Fuelings  1───N StoredFiles (OwnerType = 'Fueling', sem FK — mesmo padrão dos outros donos)
```

Todas as tabelas têm `CompanyId` com FK `Restrict` para `Companies` e filtro global de tenant. `Fuelings` **não é soft-deletável** (nunca é excluído: é cancelado); `FuelTypes`, `FuelStations` e `FuelPrices` são.

### Vehicles (alterações)
| Coluna | Tipo | Regras |
|---|---|---|
| FuelTankCapacity | decimal(10,2) NULL | ≥ 0, ≤ 10.000 (na unidade do combustível) |
| SecondaryFuelTankCapacity | decimal(10,2) NULL | ≥ 0, ≤ 10.000 |
| ExpectedConsumption | decimal(10,2) NULL | > 0, ≤ 100 (km por unidade) |

A coluna `Vehicles.FuelType` **não mudou** (o enum foi renomeado só no C# para `VehicleFuelType`; valores gravados como texto).

### OdometerReadings (alterações)
`FuelingId uniqueidentifier NULL` (FK Restrict para `Fuelings`) + índice. `Source` ganhou o valor `Fueling`.

### FuelTypes
`Name nvarchar(60)`, `Code varchar(20)` (maiúsculas), `Category nvarchar(20)`, `Unit nvarchar(20)`, `IsActive bit`, `Description nvarchar(300) NULL`, auditoria + soft delete. Únicos filtrados `(CompanyId, Code)` e `(CompanyId, Name)`.

### FuelStations
`Name nvarchar(150)`, `Cnpj varchar(14) NULL` (normalizado), endereço (`Address`, owned type, todo opcional), `Phone varchar(11) NULL`, `ContactName nvarchar(100) NULL`, `IsInternal bit`, `IsActive bit`, `Notes nvarchar(2000) NULL`, auditoria + soft delete. Índices: `(CompanyId, Name)`; único `(CompanyId, Cnpj)` filtrado por `[DeletedAt] IS NULL AND [Cnpj] IS NOT NULL`.

### FuelPrices
`FuelStationId`, `FuelTypeId` (FK Restrict), `Price decimal(10,4)`, `EffectiveFrom date`, `Notes nvarchar(300) NULL`, auditoria + soft delete. Índice `(CompanyId, FuelStationId, FuelTypeId, EffectiveFrom)` — "preço vigente" = maior `EffectiveFrom` ≤ data.

### FuelSettings
`TankTolerancePercent`, `PriceDeviationPercent`, `ConsumptionDeviationPercent`, `MinHoursBetweenFuelings` (int), `RequireDriver bit`, auditoria. Único `CompanyId`. Ausente = padrões do Domain.

### Fuelings
| Coluna | Tipo | Observação |
|---|---|---|
| VehicleId / DriverId / FuelStationId / FuelTypeId | uniqueidentifier | FKs Restrict (motorista e posto opcionais) |
| FueledAt | datetime2 | UTC, segundos inteiros |
| FueledOn | date | data de negócio (Brasil) — filtros de período e agrupamentos sem aritmética de fuso no SQL |
| OdometerKm | int | |
| Quantity | decimal(12,3) | |
| UnitPrice | decimal(10,4) | preço **pago** (nunca recalculado) |
| TotalAmount | decimal(14,2) | sempre calculado pelo servidor |
| IsFullTank | bit | |
| PaymentMethod / Source / Status | nvarchar(20) | enums como texto |
| ReceiptNumber | nvarchar(60) NULL | |
| Notes | nvarchar(1000) NULL | |
| ReviewedAt/By, ReviewNotes | NULL | revisão |
| CancelledAt/By, CancellationReason | NULL | cancelamento |
| ConsumptionResult | nvarchar(20) | `PartialFill/FirstFullTank/Calculated/NotReliable` |
| SegmentDistanceKm | int NULL | **snapshot do trecho** — só preenchidos quando `Calculated` |
| SegmentQuantity | decimal(12,3) NULL | |
| SegmentCost | decimal(14,2) NULL | |
| Consumption | decimal(10,2) NULL | km/unidade |
| ExpectedConsumption | decimal(10,2) NULL | esperado usado no cálculo |
| SegmentExpectedQuantity | decimal(12,3) NULL | distância ÷ esperado (soma direta nos relatórios) |
| BaselineSource | nvarchar(30) NULL | |
| ConsumptionDeviationPercent | decimal(8,1) NULL | |

Índices (escolhidos pelas consultas reais, não um por coluna):

| Índice | Consulta que atende |
|---|---|
| `(CompanyId, VehicleId, FueledAt)` | cadeia de trechos do veículo, aba Combustível, regra de frequência |
| `(CompanyId, FueledOn)` | listas por período, painel, relatórios |
| `(CompanyId, Status)` | fila "requer revisão" e contador do painel |
| `(CompanyId, FuelTypeId, FueledOn)` | preço de referência (média 30 dias por combustível), relatório de preços |
| `(CompanyId, FuelStationId, FueledOn)` | histórico do posto, relatório de postos |
| `(CompanyId, DriverId, FueledOn)` | filtro e custo por motorista |

Quantidade, preço, total e hodômetro **não** são indexados: são filtros secundários aplicados sobre um período ou veículo já restrito pelos índices acima.

### FuelingAnomalies / FuelingCorrections
- Alertas (cascade): `Type nvarchar(30)`, `Message nvarchar(400)`, `ExpectedValue/ActualValue decimal(14,4) NULL`, `DetectedAt`, `ReviewedAt/By NULL`. Índice `(CompanyId, Type)`.
- Correções (cascade, append-only): `CorrectedAt`, `CorrectedBy`, `Reason nvarchar(1000)`, `Changes nvarchar(4000)` (JSON `[{field,label,from,to}]`).

### Agregações e o SQLite dos testes (ADR-033)
- Totais e relatórios são agregados **no banco** (`GROUP BY` + `SUM` só de colunas simples). As colunas de trecho ficam `NULL` quando não há trecho medido, então `SUM` já devolve só os trechos medidos, sem `CASE`. Somas "só litros" agrupam também por unidade e consolidam as ≤ 3 linhas em memória.
- O SQLite não tem `decimal` e recusa `SUM/AVG/MIN/MAX` sobre ele. **Só quando o provedor é SQLite** (testes), o `FleetDbContext` grava `decimal` como `REAL`; o SQL Server mantém `decimal(p,s)` exato. Expressões calculadas (`SUM(a / b)`, `SUM(x ?? 0)`, casts) continuam não traduzindo no SQLite — some colunas, não expressões.
- Teste de volume: 12.000 abastecimentos de 300 veículos — painel em ~130 ms e as quatro consultas analíticas em ~180 ms no SQLite em memória (`FuelVolumeTests`). Todas as consultas foram executadas também no SQL Server (LocalDB) com os dados de desenvolvimento.

### Seed
Permissões 170–177 e o mapeamento dos papéis via `InsertData` na migration. O catálogo padrão de combustíveis é criado na primeira leitura de cada empresa (como os tipos de documento). Em desenvolvimento, o `DevFuelSeeder` cria 3 postos (um tanque próprio), preços de referência e ~4 meses de abastecimentos dos veículos de exemplo, com a leitura de hodômetro de cada um e dois alertas para revisão.

### Revisão da migration `FuelManagement`
O `Up` é **somente aditivo**: 7 tabelas novas, 3 colunas nullable em `Vehicles`, `OdometerReadings.FuelingId` (nullable) + índice, o seed de permissões/papéis e a atualização das descrições de três papéis. Nenhuma coluna existente foi alterada ou removida. O aviso "may result in the loss of data" refere-se só ao `Down`.

### Retenção
Abastecimentos, alertas, correções e leituras geradas são histórico operacional: não há exclusão nem expurgo nesta fase (o cancelado permanece). Arquivos anexados seguem a política geral (bytes no storage até existir o job de limpeza).
