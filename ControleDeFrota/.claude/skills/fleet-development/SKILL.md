---
name: fleet-development
description: Padrões obrigatórios do projeto ControleDeFrota (gestão de frotas .NET 8 + React/Mantine). Use SEMPRE que for criar, alterar ou revisar código, banco, testes, telas ou documentação do ControleDeFrota — novos módulos (manutenção, abastecimento, viagens…), entidades, endpoints, migrations, permissões, formulários, listas ou regras de negócio de veículos, motoristas, implementos, empresas e usuários.
---

# fleet-development

Regras para manter o ControleDeFrota consistente. A fonte da verdade é `ControleDeFrota/docs/`: **leia o doc da área antes de mexer nela** e atualize-o no mesmo trabalho.

Prioridades: Clareza > Esperteza · Manutenibilidade > Complexidade · UX > Nº de features · Segurança > Conveniência · Testabilidade > Acoplamento.

## Antes de começar

1. Confirme que a tarefa pertence à fase atual (`docs/ROADMAP.md`). Não adiante módulos futuros; apenas deixe o ponto de extensão.
2. Se a decisão afeta arquitetura, modelo de dados de forma difícil de reverter, segurança ou regra de negócio não especificada, **pare e pergunte**. Decisões pequenas e reversíveis: siga a boa prática e registre-as.
3. Siga o fluxo: Requisito → Domínio → Regras → Backend → Testes → Frontend → Revisão de UX → Quality gates → Docs.

## Arquitetura

- Camadas: `Fleet.Api → Fleet.Infrastructure → Fleet.Application → Fleet.Domain`. As dependências só apontam para dentro. O Domain não referencia EF nem ASP.NET.
- Um módulo = uma pasta vertical em cada camada: `Domain/<Area>/`, `Application/<Modulo>/{Service,Dtos,Validators}.cs`, `Infrastructure/Persistence/Configurations/<Entidade>Configuration.cs`, `Api/Controllers/<Modulo>Controller.cs`, `frontend/src/features/<modulo>/`.
- Os serviços usam `IFleetDbContext` direto, sem repositórios, MediatR ou AutoMapper. O mapeamento é manual (`ToResponse()`).
- Controllers finos: recebem o DTO, chamam o serviço e devolvem o resultado. Toda regra fica no serviço.
- Serviço novo: registre em `Fleet.Application/DependencyInjection.cs`.
- Entidade nova ligada ao veículo (manutenção, abastecimento…) é **uma entidade própria com `VehicleId`**, nunca colunas extras em `Vehicle`.
- Regra de negócio pura vai para um *policy object* no Domain (`OdometerPolicy`, `DocumentExpiryPolicy`, `OccurrenceWorkflow`…). O serviço orquestra e o controller só traduz HTTP.
- Fato operacional novo (ex.: "manutenção aberta", "abastecimento registrado"): acrescente um valor a `OperationalEventType` e chame `OperationalEventLog.Record(...)` no serviço, antes do `SaveChangesAsync` (ADR-025). Isso alimenta o histórico do veículo e as notificações futuras sem acoplar módulos.
- Histórico nunca é sobrescrito nem apagado: use vigência (`StartedAt/EndedAt`), estados finais (cancelado/rejeitado) ou snapshots. Status que depende de data é **calculado** (não gravado).
- Arquivos: `FileService` + `IFileStorage`; nunca `byte[]` em entidade.

## Código

- Código, rotas e tabelas em **inglês**. Textos de UI e mensagens para o usuário em **pt-BR**.
- Status e tipos são sempre `enum` (C#) ou constantes/union types (TS). **Nunca compare string literal de status.** Os rótulos pt-BR ficam em `frontend/src/features/<modulo>/labels.ts`.
- Métodos async têm sufixo `Async` e recebem `CancellationToken`.
- Erros: lance `NotFoundException` (404, inclusive cross-tenant), `ConflictException` (409), `BusinessRuleException` (422), `ForbiddenException` (403) ou `ValidationException` (400). A mensagem deve dizer o que aconteceu e como resolver, em pt-BR. Não capture exceção só para logar.
- Logging estruturado (`LogInformation("... {VehicleId}", id)`). Nunca logue senha, token, hash ou dados pessoais.
- Comente o porquê, não o quê. Referencie o ADR quando relevante.

## Banco

- Toda entidade de negócio herda `AuditableEntity` (Guid `Id`, `CreatedAt/By`, `UpdatedAt/By`) e normalmente implementa `ISoftDeletable`, `IAuditable` e, se pertencer a uma empresa, `ITenantScoped`.
- **Nunca** setar `DeletedAt`, `CreatedAt` ou `CompanyId` à mão: o `FleetDbContext.SaveChangesAsync` faz isso. Para excluir, use `db.X.Remove(entity)`.
- Índices únicos de entidades soft-deletáveis são **filtrados**: `.HasFilter("[DeletedAt] IS NULL")`. Índices de tenant começam por `CompanyId`.
- Enums: `.HasConversion<string>().HasMaxLength(n)`. Datas com hora: `DateTime` UTC. Datas puras: `DateOnly`. Não ordene por `decimal` (o SQLite dos testes não suporta).
- Documentos são gravados normalizados (só dígitos; placa em maiúsculas sem hífen).
- Migration: `dotnet ef migrations add <Nome> --project src/Fleet.Infrastructure --startup-project src/Fleet.Api`. Revise o SQL e nunca edite migration já aplicada.
- `IgnoreQueryFilters()` só com comentário justificando (quebra o isolamento de tenant/soft delete).

## Segurança

- Todo endpoint tem `[HasPermission(Permissions.<Modulo>.<Acao>)]`, com exceção de login/refresh/logout, que são `[AllowAnonymous]`.
- Autorização **só por permissão**, nunca por nome de papel. Permissão nova: constante em `Fleet.Domain/Authorization/Permissions.cs` + mapeamento em `SystemRoles.cs` + migration + uso no endpoint + tabela em `docs/DOMAIN.md`.
- Nunca aceite `CompanyId`, `UserId` ou permissões vindos do cliente como verdade. Use `ICurrentUser`.
- Valide toda entrada no backend (FluentValidation + `Fleet.Domain/Validation`). Ordenação apenas por uma whitelist de colunas.
- Anti-escalonamento: não se concede permissão que o próprio usuário não tem.
- Regra que depende do **alvo** (ex.: correção só com `mileage.manage`, download conforme o dono do arquivo) é checada no serviço com `ICurrentUser.HasPermission`, além do `[HasPermission]` da rota.

## Testes

- Toda regra de negócio nova tem teste. Nome no formato `Metodo_Cenario_Resultado`, estrutura AAA.
- Validação de documento: `tests/Fleet.Domain.Tests`. Serviço (duplicidade, tenant, soft delete, auditoria, permissões): `tests/Fleet.Application.Tests` com `TestDb` (SQLite em memória). HTTP (401/403/404, contrato de erro): `tests/Fleet.Api.Tests` com `FleetApiFactory`.
- Sempre inclua um teste de **isolamento de tenant** para uma entidade nova de tenant.
- Cenários operacionais: use `Scenario.SignedInAsync/VehicleAsync/DriverAsync` (`tests/Fleet.Application.Tests/TestSupport/Scenario.cs`) e `Services.<Modulo>(t)`. Arquivos em teste usam `InMemoryFileStorage`.
- Frontend: Vitest para funções puras (`src/lib`).

## UX/UI

Detalhes em `docs/UX_UI.md`. O mínimo obrigatório de toda tela:

- `PageHeader` com título, descrição e **uma** ação primária.
- Estados: carregando (`Skeleton`), vazio com CTA (`EmptyState`), filtro sem resultado, erro com "Tentar novamente".
- Listas usam `DataTable`: busca com debounce, filtros na URL, ordenação no cabeçalho, paginação e cards no celular.
- Formulários: seções com título, `withAsterisk` nos obrigatórios, validação inline, máscaras (`MaskedInput`), `autoComplete`/`inputMode` corretos, defaults úteis e confirmação ao sair com alterações não salvas.
- Feedback: botão em `loading` ao salvar, notificação de sucesso ("… salvo com sucesso."), erro amigável com instrução (use `notifyError`). Nunca exiba "Error 500".
- Exclusão sempre com modal de confirmação que nomeia o registro.
- Responsivo (desktop, tablet, 375px), alvos de toque ≥ 40px, cores apenas do `theme.ts` e badges de status com texto.
- Registro com vida operacional ganha uma página **hub** com `DetailTabs` (aba na URL `?aba=`) e cabeçalho com `HeaderFact`; a edição do cadastro fica em `/:id/editar`.
- Seleção de veículo/motorista: `VehiclePicker`/`DriverPicker` (busca no servidor). Anexos: `UploadButton` (com `camera` no celular) + `AttachmentList`.
- Telas usadas em campo (checklist) são mobile-first: botões ≥ 48px, "marcar todos", foto pela câmera, barra de envio fixa e erro rolando até o item.

## Quality gates (antes de dizer "pronto")

```bash
dotnet build Fleet.sln -warnaserror
dotnet test Fleet.sln
dotnet ef migrations has-pending-model-changes --project src/Fleet.Infrastructure --startup-project src/Fleet.Api
dotnet list package --vulnerable --include-transitive
cd frontend && npm run lint && npm run build && npm test
```

Depois dos comandos, revise UX e responsividade e atualize `docs/` e `docs/CHANGELOG.md`. "Funciona" não é critério de pronto.
