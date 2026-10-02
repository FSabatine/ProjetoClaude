# Diretrizes de desenvolvimento (obrigatórias)

Prioridades, nesta ordem, quando houver conflito: **Clareza > Esperteza · Manutenibilidade > Complexidade · UX > Nº de funcionalidades · Segurança > Conveniência · Testabilidade > Acoplamento**.

## Fluxo de uma funcionalidade

```
Requisito → Análise → Modelo de domínio → Regras de negócio → Backend → Testes → Frontend → Revisão de UX → Validação (quality gates) → Documentação
```

Nenhuma tela é criada antes de o endpoint e os testes das regras existirem.

## Princípios

- **Clean Code**: funções curtas que fazem uma coisa só; nomes que dispensam comentário; sem números mágicos (use constantes nomeadas); sem código morto ou comentado.
- **SOLID**:
  - *S*: um serviço por módulo; controllers só traduzem HTTP ↔ serviço.
  - *O*: novos KPIs, alertas e módulos são adicionados sem editar os existentes.
  - *L*: tipos derivados respeitam o contrato da base.
  - *I*: interfaces pequenas (`ICurrentUser`, `IClock`).
  - *D*: Application depende de abstrações e Infrastructure as implementa.
- **DRY**: uma regra, um lugar. Ex.: a validação de CPF existe em `Fleet.Domain/Validation/Cpf.cs`, e o espelho no frontend (`src/lib/validators.ts`) serve só para feedback e é testado com os mesmos casos.
- **KISS/YAGNI**: nada de abstração "para o futuro". Não usamos repositório genérico, MediatR ou AutoMapper; o mapeamento é manual e explícito. Uma abstração nasce quando há um segundo uso real.
- **Separation of Concerns**: nenhuma regra de negócio em controller, componente React ou configuração do EF.

## Nomes

| Item | Convenção | Exemplo |
|---|---|---|
| Código C#/TS, tabelas, rotas | inglês | `VehicleService`, `/api/v1/vehicles` |
| Textos de interface, mensagens de erro para o usuário, docs | português (pt-BR) | "Placa já cadastrada." |
| Classes/métodos C# | PascalCase | `CreateAsync` |
| Parâmetros/locais C# | camelCase | `vehicleId` |
| Privados C# | `_camelCase` | `_db` |
| Métodos assíncronos | sufixo `Async` + `CancellationToken` | `GetByIdAsync(id, ct)` |
| DTOs | `<Entidade><Uso>Request/Response` | `VehicleCreateRequest`, `VehicleResponse` |
| Componentes React | PascalCase, 1 por arquivo | `VehicleFormPage.tsx` |
| Hooks | `useXxx` | `useVehicles` |
| Permissões | `<modulo>.<acao>` minúsculo | `vehicles.update` |

## Organização de arquivos

```
src/Fleet.Domain/<Area>/            entidades + enums do módulo (Vehicles/Vehicle.cs, Vehicles/VehicleStatus.cs)
src/Fleet.Application/<Modulo>/     <Modulo>Service.cs, <Modulo>Dtos.cs, <Modulo>Validators.cs
src/Fleet.Infrastructure/Persistence/Configurations/<Entidade>Configuration.cs
src/Fleet.Api/Controllers/<Modulo>Controller.cs
tests/Fleet.<Camada>.Tests/<Modulo>/...Tests.cs
frontend/src/features/<modulo>/     api.ts, types.ts, <X>ListPage.tsx, <X>FormPage.tsx, labels.ts
frontend/src/components/            componentes genéricos (DataTable, PageHeader, EmptyState…)
```

Por padrão, cada módulo é uma pasta vertical: tudo o que muda junto fica junto.

## Status e valores fixos

- **Proibido** comparar status com string literal. Use enums (`VehicleStatus.Available`) no C# e as union types geradas a partir de uma constante no TS (`VEHICLE_STATUS`).
- Rótulos em português e cores dos badges ficam em **um único mapa** por enum (`frontend/src/features/<modulo>/labels.ts`).

## Tratamento de erros

- Os serviços lançam exceções de aplicação tipadas (`NotFoundException`, `ConflictException`, `BusinessRuleException`, `ForbiddenException`). Nunca retornam `null` ou códigos para sinalizar erro de negócio.
- As mensagens das exceções são **para o usuário final**, em pt-BR, e dizem o que aconteceu e como resolver.
- Não se captura exceção só para logar e relançar. O `ExceptionHandler` global loga e converte.
- `catch` genérico só na borda (middleware).

## Logging

- `ILogger<T>` com templates estruturados (`_logger.LogInformation("Vehicle {VehicleId} created", id)`), nunca interpolação de string.
- Níveis:
  - `Information` para eventos de negócio relevantes (login, criação);
  - `Warning` para anomalias esperadas (login falho, bloqueio, reuso de token);
  - `Error` para falhas inesperadas.
- Nunca logar senhas, tokens, hashes ou dados pessoais de motoristas.

## Validação

- **Toda** entrada é validada no backend, com FluentValidation por DTO e regras de documento vindas de `Fleet.Domain/Validation`.
- Regras que dependem do banco (duplicidade, existência) ficam no serviço, não no validator.
- O frontend replica as validações de formato apenas para feedback imediato.
- Strings são normalizadas no serviço antes de gravar: `Trim`, documentos só com dígitos, e-mail em minúsculas, placa em maiúsculas.

## Testes

- Toda regra de negócio tem teste. **PR sem teste de regra nova não é aprovado.**
- `Fleet.Domain.Tests`: validadores de documentos, com casos válidos, inválidos e de borda.
- `Fleet.Application.Tests`: serviços contra SQLite em memória (duplicidade, isolamento de tenant, anti-escalonamento, soft delete, auditoria).
- `Fleet.Api.Tests`: integração HTTP com `WebApplicationFactory` (autenticação, 401/403/404, contrato de erro).
- `frontend`: Vitest para as funções puras (validadores, formatadores).
- Agregação (totais, médias, relatórios) é feita **no banco**. No SQLite dos testes, `decimal` vira REAL (ADR-033): some **colunas**, não expressões (`SUM(a/b)`, `SUM(x ?? 0)`, casts não traduzem). Se precisar de uma soma derivada, grave-a como coluna (snapshot) — é o que dá estabilidade histórica também.
- Regra com volume (dashboard, relatório) ganha um teste de volume com timing generoso (ex.: `FuelVolumeTests`, 12 mil registros) para pegar carregamento em memória ou consulta por linha.
- Toda consulta nova que agrega ou agrupa deve ser executada ao menos uma vez no SQL Server (LocalDB) antes de dar por pronta: o SQLite dos testes não prova a tradução do SQL Server.
- Nome do teste: `Metodo_Cenario_ResultadoEsperado`. Estrutura Arrange/Act/Assert.
- Os testes não dependem de ordem nem de relógio real (use `IClock`/`FakeClock`).

## Comentários

- Comente o **porquê**, nunca o quê. O código explica o quê.
- Decisões não óbvias levam um comentário curto que referencia o ADR (`// ADR-009: OnTrip será controlado pelo módulo de Viagens`).
- Não use comentários de "TODO" soltos: pendências vão para o ROADMAP ou para a seção de pontos em aberto do DECISIONS.

## Documentação

Mudou arquitetura, banco, regra de negócio, segurança, UX ou dependência importante? **Atualize o doc correspondente no mesmo PR** e registre em CHANGELOG.md. Uma decisão estrutural gera um ADR em DECISIONS.md.

**Funcionalidade nova visível para o usuário final?** Pergunte: *"isso precisa de um artigo novo na Central de Ajuda?"* Se sim, adicione (ou edite) o artigo em `frontend/src/features/help/content/<categoria>.ts` no mesmo PR — só documente o que já está implementado, nunca uma função planejada. A Central de Ajuda (`?` no cabeçalho) é **diferente** de `docs/`: é em pt-BR, linguagem de negócio, sem detalhe técnico.

## Code review: checklist

- [ ] O escopo é o que foi pedido, sem módulo futuro "adiantado"?
- [ ] Todo endpoint novo tem `[HasPermission]`?
- [ ] A entidade de tenant implementa `ITenantScoped`?
- [ ] Nenhuma query usa `IgnoreQueryFilters()` sem justificativa?
- [ ] Há validação no backend para toda entrada?
- [ ] Nenhuma string literal de status?
- [ ] Há testes das regras novas, e eles passam?
- [ ] A migration foi revisada (sem perda de dados), com índices e unicidade filtrados?
- [ ] As mensagens para o usuário estão em pt-BR e são acionáveis?
- [ ] A tela tem os estados de carregamento, vazio, erro, feedback e responsividade?
- [ ] Docs e CHANGELOG foram atualizados?
- [ ] Se a funcionalidade é visível para o usuário final, a Central de Ajuda ganhou (ou atualizou) um artigo?

## Segurança (resumo; detalhes em SECURITY.md)

- Autorização por permissão em todo endpoint; nunca por nome de papel.
- Nunca confiar em `CompanyId`, `UserId` ou papéis vindos do cliente.
- Sem SQL concatenado. Ordenação apenas por colunas de uma whitelist.
- Segredos nunca entram no repositório.

## Quality gates (antes de concluir qualquer entrega)

```bash
dotnet build Fleet.sln -warnaserror
dotnet test Fleet.sln
dotnet list package --vulnerable --include-transitive
dotnet ef migrations has-pending-model-changes --project src/Fleet.Infrastructure --startup-project src/Fleet.Api
cd frontend && npm run lint && npm run build && npm test && npm audit --omit=dev
```

Além dos comandos, revise a UX e a responsividade (desktop, tablet e 375px de largura).
