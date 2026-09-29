# Banco de dados

## Tecnologia

- **SQL Server** (produção) / **SQL Server LocalDB** (desenvolvimento, `(localdb)\MSSQLLocalDB`, banco `ControleDeFrota`).
- **EF Core 8**, com o modelo definido em código (code-first). Uma `IEntityTypeConfiguration<T>` por entidade, em `Fleet.Infrastructure/Persistence/Configurations/`.
- Os testes automatizados usam **SQLite em memória** (ADR-004). Por isso o modelo evita recursos que o SQLite não traduz: não há `DateTimeOffset` (usamos `DateTime` UTC) e não há ordenação por colunas `decimal`.

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
- Aplicado a: `Companies`, `Users`, `Drivers`, `Vehicles`, `Implements`. Não se aplica a catálogos, `RefreshTokens` e `AuditLogs`.
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

- Nesta fase, o histórico de alterações é o próprio `AuditLogs`.
- Histórico **de negócio** (leituras de hodômetro, vínculos veículo-implemento, alocação motorista-veículo) virá em tabelas próprias, com vigência (`StartedAt`/`EndedAt`), nas fases seguintes. O valor atual (`Vehicle.CurrentOdometerKm`) continua em `Vehicles` como leitura rápida.

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
