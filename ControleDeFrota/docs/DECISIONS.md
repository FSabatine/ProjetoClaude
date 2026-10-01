# Registro de decisões (ADR)

Formato: **Problema · Alternativas · Decisão · Motivo · Impacto**. Um ADR não é apagado. Se for revisto, ganha status "Substituído por ADR-xxx".

---

## ADR-001 — Stack tecnológica
- **Status**: aceito (decisão do usuário, 2026-09-29)
- **Problema**: pasta vazia; é preciso escolher a stack de uma aplicação corporativa de médio/grande porte.
- **Alternativas**: (a) .NET 8 + EF Core + SQL Server + React/Vite/TS, a mesma do REC4; (b) .NET 8 + PostgreSQL; (c) NestJS + Prisma + React.
- **Decisão**: (a).
- **Motivo**: o time já domina a stack e as ferramentas estão instaladas (.NET SDK 8, `dotnet-ef` 8, LocalDB, Node 18). Isso permite reaproveitar padrões validados no REC4.
- **Impacto**: o Node 18 limita o frontend ao Vite 5. Atualizar para Node 20+ é recomendável, mas não bloqueia nada.

## ADR-002 — Sistema independente do REC4
- **Status**: aceito (decisão do usuário)
- **Decisão**: solução, banco e autenticação próprios. Só **padrões** do REC4 são reaproveitados, sem referência de código.
- **Impacto**: os dois sistemas evoluem sem acoplamento. Um SSO futuro entra como integração (Fase 10).

## ADR-003 — Multi-empresa: isolamento por `CompanyId` + super-admin
- **Status**: aceito (decisão do usuário)
- **Alternativas**: (a) banco compartilhado com filtro por `CompanyId`; (b) schema/banco por empresa; (c) usuário N:N empresas com seletor.
- **Decisão**: (a), com um super-admin da plataforma identificado pela **permissão** `companies.manage`.
- **Motivo**: é o mais simples de operar e de migrar, adequado ao volume esperado. O filtro global do EF garante o isolamento sem depender da disciplina de cada query.
- **Impacto**: todas as tabelas operacionais têm `CompanyId` e índices começando por ele. A opção (c) continua possível no futuro, adicionando `UserCompanies` e um claim de "empresa ativa".

## ADR-004 — Application acessa `IFleetDbContext` direto; testes com SQLite em memória
- **Alternativas**: repositório genérico + UoW; repositórios específicos; DbContext direto.
- **Decisão**: `IFleetDbContext` direto nos serviços.
- **Motivo**: o DbContext já é UoW/Repository, e uma camada extra só repassaria chamadas (KISS). O SQLite em memória executa SQL real, inclusive índices únicos e filtros, com mais fidelidade que o provider InMemory.
- **Impacto**: as divergências do SQLite (ex.: não traduz `DateTimeOffset` nem ordenação por `decimal`) moldam o modelo: `DateTime` UTC e nenhuma ordenação por `decimal`.

## ADR-005 — Serviços por módulo, sem CQRS/MediatR/AutoMapper
- **Decisão**: uma classe `XxxService` por módulo e mapeamento manual (`ToResponse()`).
- **Motivo**: menos indireção e rastreabilidade direta (F12 leva ao código). CQRS entra se um módulo tiver leitura e escrita com necessidades muito diferentes (ex.: relatórios da Fase 8).

## ADR-006 — Autorização por permissão com policy provider dinâmico
- **Decisão**: `[HasPermission("vehicles.update")]` → policy `perm:vehicles.update` → handler que verifica o claim `perm`. Uma `FallbackPolicy` exige autenticação em todo endpoint sem atributo explícito.
- **Motivo**: evita `if (role == ...)`, e papéis passam a ser só dados. Uma permissão nova não exige registrar policy.
- **Impacto**: as permissões vão no JWT, e uma mudança reflete em até 15 minutos (duração do access token).

## ADR-007 — Access token em memória + refresh token rotativo em cookie HttpOnly
- **Alternativas**: (a) token em `localStorage`; (b) token só em memória (como no REC4; F5 força novo login); (c) memória + refresh em cookie HttpOnly.
- **Decisão**: (c).
- **Motivo**: (a) é vulnerável a XSS e (b) prejudica a UX (perde a sessão ao recarregar e a cada hora). Com (c), o JavaScript nunca lê o refresh token, e a rotação com detecção de reuso limita o dano de um roubo.
- **Impacto**: tabela `RefreshTokens`, endpoint `/auth/refresh` e um interceptor no frontend que renova o token num 401.

## ADR-008 — Enums gravados como texto
- **Decisão**: `HasConversion<string>()` em todos os enums.
- **Motivo**: banco legível para BI e suporte; reordenar ou incluir valores não corrompe dados.
- **Impacto**: mais alguns bytes por linha, sem relevância no volume esperado.

## ADR-009 — Estados do veículo
- **Problema**: a sugestão inicial (`Active, Inactive, UnderMaintenance, Available, OnTrip`) mistura dois eixos: estar na frota (ativo/inativo) e estar disponível (disponível/em viagem/em manutenção). Um veículo "Active" e "Available" ao mesmo tempo seria ambíguo.
- **Decisão**: um único eixo, `Available | OnTrip | UnderMaintenance | Inactive`. **"Ativo" é derivado** (≠ `Inactive`).
- **Motivo**: um campo só, sem combinações inválidas. Implementos seguem o mesmo modelo (`InUse` no lugar de `OnTrip`). Motoristas têm `Active | OnLeave | Inactive`.
- **Impacto**: nas Fases 2 e 3, `OnTrip` e `UnderMaintenance` passarão a ser controlados por viagens e manutenções, e a edição manual será restringida.

## ADR-010 — Mantine 7 como biblioteca de UI
- **Alternativas**: Tailwind + shadcn/ui; MUI; Ant Design; Mantine.
- **Decisão**: Mantine 7 + TanStack Query + react-imask.
- **Motivo**: visual moderno e limpo, tema centralizado, formulários, notificações, modais, datas e AppShell responsivo prontos, com boa acessibilidade. O shadcn exige manter dezenas de componentes copiados, e o MUI/AntD têm cara de "admin genérico".
- **Impacto**: a versão 7.x é compatível com React 18 e Node 18.

## ADR-011 — Soft delete via `DeletedAt` + interceptação no `SaveChanges`
- **Decisão**: `ISoftDeletable` + filtro global + conversão automática de `Remove()` em update + índices únicos filtrados por `DeletedAt IS NULL`.
- **Motivo**: um único ponto de aplicação, impossível de esquecer num módulo.
- **Impacto**: `IgnoreQueryFilters()` passa a ser um sinal de alerta em code review.

## ADR-012 — Auditoria genérica no `SaveChanges`
- **Decisão**: entidades `IAuditable` geram `AuditLogs` com um diff JSON (valor antigo/novo) na mesma transação da alteração.
- **Alternativas**: temporal tables do SQL Server (não funcionam no SQLite de teste e não registram *quem*); eventos de domínio (cerimônia desnecessária agora).
- **Impacto**: é gratuita para toda entidade nova que implemente `IAuditable`.

## ADR-013 — Validação de CNPJ alfanumérico
- **Problema**: desde julho de 2026 a Receita Federal emite CNPJs alfanuméricos (IN RFB 2.229/2024). Um validador só numérico rejeitaria empresas novas.
- **Decisão**: validar os 12 primeiros caracteres como `[0-9A-Z]` e os 2 DVs como dígitos, com o valor de cada caractere = código ASCII − 48 (algoritmo oficial). CNPJs numéricos continuam válidos pelo mesmo cálculo.

## ADR-014 — Usuário com vários papéis (N:N) e permissões pela união
- **Alternativas**: (a) um papel por usuário (FK simples, como no REC4); (b) N:N com a união das permissões.
- **Decisão**: (b), com a tabela `UserRoles`.
- **Motivo**: perfis reais se combinam (ex.: Operações + Manutenção) sem a necessidade de criar um papel para cada combinação. Migrar de FK para N:N depois seria uma mudança de ruptura, enquanto começar em N:N não custa nada.
- **Impacto**: `PermissionResolver` é a única fonte da regra. O anti-escalonamento vale para cada papel atribuído.

## ADR-015 — Versões do frontend limitadas pelo Node 18
- **Problema**: a máquina de desenvolvimento tem Node 18.16. As versões mais recentes de Vite (7+) e Vitest (4+) exigem Node 20.
- **Decisão**: Vite 6.4, Vitest 3.2, React 18 e Mantine 7.17. O React Router 7.18 é usado **como biblioteca** (`createBrowserRouter`), sem o modo framework nem a CLI, porque a v6 tem open redirect conhecido.
- **Impacto**: as dependências de produção estão sem vulnerabilidades conhecidas (`npm audit --omit=dev`). Fica um aviso moderado somente no Vitest, em desenvolvimento. **Recomendação**: atualizar para Node 20 LTS+ e depois para Vite 7 e Vitest 4.

## ADR-016 — Rotas em português e carregamento sob demanda
- **Decisão**: as URLs visíveis ao usuário ficam em pt-BR (`/veiculos`, `/motoristas/novo`), enquanto código e API ficam em inglês. As páginas são carregadas com `React.lazy` (code splitting).
- **Motivo**: a URL faz parte da interface. O carregamento sob demanda reduz a carga inicial (a tela de login não baixa as telas de cadastro).
- **Impacto**: uma página nova é registrada em `App.tsx` pelo helper `page()`.

## ADR-017 — Cookie de refresh `Secure` configurável apenas para testes
- **Problema**: o `TestServer` usa http puro, e um cookie `Secure` não volta nas requisições seguintes. Isso impediria testar a rotação do refresh token de ponta a ponta.
- **Decisão**: `Auth:RefreshCookie:Secure` (padrão `true`). Só o `FleetApiFactory` usa `false`.
- **Impacto**: nenhum ambiente real deve desligar essa opção (ver SECURITY.md).

---

## Pontos em aberto (escolhas provisórias, a confirmar)

- **Categoria do veículo**: interpretada como classe de peso (Leve, Médio, Semipesado, Pesado), opcional. Se o negócio quiser a categoria do CRLV (Particular, Aluguel, Oficial…), isso vira outro campo.
- **Número da CNH**: só é validado o formato (11 dígitos, não todos iguais). O algoritmo de DV da CNH tem variantes públicas divergentes, e validar errado bloquearia motoristas reais.
- **Implemento sem placa**: hoje placa, RENAVAM e chassi são obrigatórios (reboques são veículos registrados). Se existirem implementos não emplacados, esses campos passam a ser opcionais.
- **Capacidade de veículo** em kg; **capacidade de implemento** com unidade escolhida.
- **Exclusão de empresa** exige que ela não tenha usuários ativos.
- **Troca de empresa ativa** para o super-admin (hoje ele opera a frota apenas da própria empresa e administra as demais): não entrou no escopo da Fase 2 (redefinido pelo usuário); continua no backlog.
- **Histórico de auditoria**: os nomes dos campos aparecem como estão gravados (`CurrentOdometerKm`). Um mapa de rótulos pt-BR por módulo é o próximo refinamento.

---

# Fase 2 — Controle operacional (2026-09-30)

## ADR-018 — Situação operacional derivada; "Alocado" não é gravado
- **Status**: aceito (decisão do usuário, 2026-09-30). Complementa o ADR-009.
- **Problema**: a Fase 2 pede os status Available, Assigned, OnTrip, Unavailable, UnderMaintenance e Inactive. "Assigned" (tem motorista) é um eixo diferente da condição do veículo: um veículo alocado entra em manutenção e continua com o motorista. Gravar Assigned no campo de status perderia uma das informações ou exigiria regras para restaurá-la.
- **Alternativas**: (a) gravar Assigned no enum, alterado pelo serviço de alocação; (b) manter a condição gravada e **derivar** a situação operacional.
- **Decisão**: (b). A condição gravada ganha `Unavailable`. A situação operacional é calculada por `VehicleOperationalState.From(status, temAlocaçãoAtiva)` (Domain), e o filtro SQL equivalente fica em `VehicleService.WhereOperationalStatus`.
- **Impacto**: não existem combinações inválidas; o dashboard e a lista mostram "Alocado" sem nenhum gatilho de sincronização. Motorista e implemento mantêm os status da Fase 1.

## ADR-019 — Histórico de hodômetro; leitura suspeita fica pendente
- **Status**: aceito (decisão do usuário sobre o salto suspeito).
- **Decisão**: tabela `OdometerReadings` (append-only). `Vehicle.CurrentOdometerKm`/`OdometerUpdatedAt` continuam como leitura rápida e só o `MileageService` os altera. A edição do veículo recusa mudar o hodômetro. As regras ficam em `OdometerPolicy`: não retroceder e salto > 1.500 km/dia = suspeito. A leitura suspeita é gravada como `PendingReview` e **não é aplicada** até ser aprovada; a correção exige `mileage.manage` e motivo.
- **Alternativa rejeitada**: aplicar a leitura suspeita e só sinalizar. Um erro de digitação (um dígito a mais) faria todas as leituras seguintes, corretas, serem recusadas por "retroceder".
- **Impacto**: nenhuma migração de dados. Veículos sem histórico usam o hodômetro cadastrado como linha de base.

## ADR-020 — Alocação motorista ↔ veículo com vigência
- **Status**: aceito (regras confirmadas pelo usuário).
- **Decisão**: entidade `VehicleAssignment` (`StartedAt`/`EndedAt`), com índices únicos filtrados para **uma alocação ativa por veículo e por motorista**. Bloqueios: veículo inativo, motorista desligado/afastado e CNH vencida. A troca exige confirmação (409 → `endCurrent`). Encerrar e abrir acontecem na mesma transação (`IFleetDbContext.InTransactionAsync`), porque o EF não ordena os comandos por índices filtrados.
- **Impacto**: o histórico nunca é sobrescrito. Motorista secundário e alocação agendada ficam fora desta fase.

## ADR-021 — Documentos com catálogo configurável e status calculado
- **Decisão**: `DocumentType` por empresa (dono, validade, antecedência do alerta) + `Document` com FKs opcionais por dono (`VehicleId`/`DriverId`/`ImplementId`) e check constraint `CK_Documents_Owner`. O status nunca é gravado: `DocumentExpiryPolicy` é o único lugar da regra. O início da janela de alerta (`AlertStartsOn`) é gravado para as consultas compararem só datas (indexável e sem aritmética de data específica de provedor). Renovar marca o anterior como substituído.
- **Alternativas**: dono polimórfico sem FK (perde integridade); status gravado (fica errado no dia seguinte); cálculo por `DATEADD` no SQL (a tradução varia entre SQL Server e SQLite).
- **Decisão sobre a CNH**: ela **não** é um tipo de documento. A validade continua no cadastro do motorista, para evitar duas fontes para a mesma data.

## ADR-022 — Arquivos fora do banco, atrás de `IFileStorage`
- **Decisão**: `StoredFile` guarda só os metadados e os bytes ficam no `IFileStorage` (`LocalFileStorage` em disco, fora do web root, em `Storage:LocalRootPath`). A chave do storage é gerada pelo servidor. O formato é validado por *magic bytes* (PDF/JPG/PNG, 10 MB). O fluxo é upload → arquivo sem dono (só o autor vê) → vínculo ao salvar o registro. O download passa pela API, com permissão do dono; o frontend baixa como blob porque o `<img>` não envia o Bearer.
- **Motivo**: o banco não cresce com binários e o backup fica leve. Trocar para object/cloud storage é só outra implementação da interface.
- **Impacto**: arquivos órfãos (enviados e nunca vinculados) ainda não são limpos (ver "Pontos em aberto"). O frontend reduz as fotos para no máximo 1600px antes do envio.

## ADR-023 — Ocorrência operacional genérica com máquina de estados explícita
- **Decisão**: `Occurrence` com tipo, gravidade e `Open → InAnalysis → Resolved | Cancelled`. Os estados finais exigem texto; não há reabertura nem exclusão. As transições ficam em `OccurrenceWorkflow`, e a API devolve `nextStatuses` para a UI não decidir regras.
- **Motivo**: é uma porta de entrada simples para os futuros módulos de Manutenção e Sinistros, sem implementá-los agora.

## ADR-024 — Checklists versionados com snapshot na execução
- **Decisão**: `ChecklistTemplate` + itens. Alterar os itens incrementa `Version`. A `ChecklistExecution` copia cada pergunta para `ChecklistAnswer` (snapshot), e um envio com versão antiga é recusado (409). Cada item "Não conforme" abre uma `Occurrence` na mesma transação. O hodômetro do checklist reutiliza o `MileageService.AddReadingAsync`, sem duplicar regras.
- **Alternativa rejeitada**: execuções apontando para os itens vivos do modelo. Uma edição do modelo reescreveria o passado.
- **"Pendente"**: modelos diários/semanais × veículos em operação (com motorista e condição Disponível/Em viagem). Sem módulo de viagens, não há como saber se um veículo de pool foi usado.

## ADR-025 — Eventos operacionais: histórico e outbox na mesma tabela
- **Problema**: a Fase 2 pede histórico por veículo e a base das notificações futuras (DocumentExpiring, ChecklistFailed…), sem acoplar os módulos.
- **Alternativas**: (a) montar o histórico juntando as tabelas de cada módulo na leitura; (b) MediatR/eventos de domínio em memória; (c) uma tabela `OperationalEvents` gravada na mesma transação da mudança.
- **Decisão**: (c). `OperationalEventLog.Record(...)` adiciona o evento ao unit of work. A tabela é a linha do tempo (índices por veículo/motorista + data) e o outbox (`PublishedAt` nulo). Eventos baseados em tempo (vencimento de documento) vêm de `DocumentExpirationScanner`, executado por um `BackgroundService` na API (`Jobs:DocumentExpirationScan`), idempotente via `Document.LastAlertedStatus`.
- **Motivo**: (a) acopla o histórico a todo módulo novo e pagina mal; (b) perde eventos se o processo cair e contraria o ADR-005. Com (c), um módulo novo só acrescenta valores ao enum.
- **Impacto**: o resumo do evento é uma frase pt-BR congelada (o histórico não muda depois). Um dispatcher de notificações futuro lê `PublishedAt IS NULL` (índice filtrado já criado).

## Pontos em aberto da Fase 2
- **Acesso do motorista** (app/"Meu veículo"): adiado por decisão do usuário. O papel Motorista continua sem permissões.
- **Motorista secundário / revezamento** e **alocação agendada**: não suportados; a regra atual é um responsável por vez.
- **Categoria da CNH × tipo de veículo** (ex.: cavalo mecânico exige E): não validado; hoje só a CNH vencida bloqueia a alocação.
- **Limite de 1.500 km/dia**: constante de domínio. Pode virar configuração por empresa ou por tipo de veículo.
- **Horímetro**: ~~continua editável no cadastro, sem histórico~~ (histórico entregue na Fase 3, ADR-027; a edição direta no cadastro do veículo foi removida).
- **Arquivos órfãos**: uploads nunca vinculados e arquivos removidos (soft delete) permanecem no storage até existir um job de limpeza.
- **Rate limit do `/auth/refresh`** (Fase 1: 10 por minuto por IP, junto com o login): recarregar a página várias vezes seguidas, ou vários usuários atrás do mesmo NAT, pode gerar 429 e forçar novo login. Recomendação: política própria e mais generosa para o refresh (o token tem 64 bytes aleatórios; força bruta é inviável).

---

# Fase 3 — Manutenção (2026-10-01)

## ADR-026 — Pular a Fase 2.5 (Viagens) e ir direto para a Fase 3 (Manutenção)
- **Status**: aceito (decisão explícita do usuário, 2026-10-01, confirmada quando avisado que o `ROADMAP.md`/`CLAUDE.md` recomendavam a Fase 2.5 antes).
- **Problema**: o usuário enviou uma especificação completa da Fase 3 sem pedir a Fase 2.5 (Viagens) primeiro, contrariando a recomendação registrada no roadmap.
- **Decisão**: implementar a Fase 3 agora; a Fase 2.5 volta para o backlog, antes da Fase 4.
- **Impacto**: `OnTrip` continua manual (não há módulo de viagens controlando-o), então uma ordem de serviço recusa iniciar com o veículo nesse estado em vez de lidar com uma transição Viagem↔Manutenção que ainda não existe. O vínculo veículo↔implemento com vigência (`VehicleImplementCoupling`) também continua pendente — por isso `WorkOrder.ImplementId` é um campo solto, sem o vínculo formal.

## ADR-027 — Modelo de manutenção: planos, agenda e escopo
- **Status**: aceito.
- **Problema**: a especificação do usuário (52 seções) descreve um ERP de manutenção completo — muito além do padrão enxuto das Fases 1/2 deste projeto.
- **Decisão**: escopo deliberadamente reduzido, documentado como corte consciente (não omissão):
  - **Sem** entidade `Mechanic`/técnico interno — `WorkOrderLabor.TechnicianName` é texto livre.
  - **Sem** inventário/estoque de peças nem ordens de compra — `WorkOrderPart` é só uma linha de custo.
  - **Sem** calendário visual (arrastar-e-soltar) — a "agenda" é a lista de `/work-orders` agrupada por data no frontend.
  - **Permissões**: 6 (`maintenance.{view, createrequest, manageplans, manageworkorders, manageworkshops, viewcosts}`), não as 9 sugeridas na especificação — `manageworkorders` cobre aprovar/criar/editar/atribuir/concluir (mesmo molde de `occurrences.manage` cobrindo editar+transição).
- **Modelo de planos** (`MaintenancePlan`): `VehicleId` e `VehicleType` nulos = plano padrão da empresa; a precedência (veículo específico > tipo de veículo > padrão) é resolvida por `MaintenancePlanResolver`, uma função pura, não uma query.
- **`MaintenanceSchedule`** só grava uma linha depois que o item é atendido pela primeira vez (mesma ideia do `OdometerReading`/`Vehicle.CurrentOdometerKm`): evita popular uma linha por combinação veículo×item sem necessidade. Para veículos nunca atendidos, `MaintenanceScheduleService` calcula a linha de base a partir do cadastro (leitura de registro do hodômetro, `CreatedAt`), do mesmo jeito que o `MileageService.BaselineAsync` já faz.
- **Status da agenda** (`Scheduled/DueSoon/Due/Overdue`) nunca é gravado — `MaintenanceSchedulePolicy.Evaluate` calcula a partir dos valores atuais do veículo, igual ao `DocumentExpiryPolicy`. Cada eixo configurado (km/data/horas) tem sua própria carência (`Grace*`), e o eixo mais urgente decide o status final.
- **Número da OS** (`WorkOrder.Sequence` → `"OS-000001"`): calculado por `MAX(Sequence)+1` por empresa, sem contador atômico dedicado. Sob concorrência real (duas OS abertas no mesmo milissegundo) poderia colidir; aceito como risco baixo para o volume esperado — ver "Pontos em aberto da Fase 3".
- **Solicitação → Ordem de serviço**: aprovar uma `MaintenanceRequest` cria a `WorkOrder` já em `Approved`, na mesma transação, e marca a solicitação `Converted` — não existe um estado intermediário "aprovada, sem OS".

## ADR-028 — Quem controla `Vehicle.Status = UnderMaintenance`
- **Status**: aceito.
- **Problema**: não existe `VehicleStatusService` — `Vehicle.Status` só mudava, até aqui, dentro de `VehicleService.UpdateAsync` (edição manual). A Fase 3 precisa que a ordem de serviço controle `UnderMaintenance` sem apagar uma mudança manual (ex.: `Unavailable` por documentação pendente) nem quebrar quando duas ordens do mesmo veículo se sobrepõem.
- **Decisão**: `WorkOrderService` muta o veículo diretamente, do mesmo jeito que `MileageService`/`ChecklistService` já fazem, com uma regra explícita:
  - Ao entrar em `InProgress`/`WaitingParts` pela primeira vez: se `Vehicle.Status == Available`, vira `UnderMaintenance`. Se já é `UnderMaintenance` (outra ordem ativa), não faz nada. Se é `OnTrip` ou `Inactive`, recusa. Se é `Unavailable`, permite sem mexer no status (já está correto).
  - Ao sair dessas situações (concluir/cancelar): só volta a `Available` se **nenhuma outra ordem ativa** restar para o veículo **e** o status ainda for `UnderMaintenance` — nunca sobrescreve uma mudança manual feita nesse meio tempo.
- **Alternativa rejeitada**: uma pilha/prioridade de "quem é dono do status atual". Mais correta no limite, mas desnecessária para o volume de ordens simultâneas esperado; a regra acima cobre os casos reais sem introduzir um conceito novo no modelo.
- **Impacto**: nenhum campo novo em `Vehicle` além de `HourMeterUpdatedAt`. A regra fica só no `WorkOrderService`, testada em `WorkOrderServiceTests`.

## Pontos em aberto da Fase 3
- **Número sequencial da OS** sob concorrência: ver ADR-027. Uma migração futura pode trocar por uma coluna contadora na `Company` com `UPDATE ... OUTPUT`, se o volume justificar.
- **Calendário visual**: a "agenda" é uma lista ordenada por data agrupada no frontend, não um componente de calendário com arrastar-e-soltar.
- **Entidade `Mechanic`**: técnico é texto livre em `WorkOrderLabor.TechnicianName`. Vira entidade própria se o negócio precisar de histórico por técnico, agenda de disponibilidade etc.
- **Inventário de peças**: `WorkOrderPart` é só uma linha de custo; não controla estoque, não desconta de um almoxarifado.
- **Horímetro de implementos**: `HourMeterReading`/`HourMeterService` hoje só valem para `Vehicle`. Implementos e máquinas com horímetro próprio ficam para quando existir demanda real.
- **`WorkOrder.ImplementId`**: campo solto (sem o vínculo formal `VehicleImplementCoupling` da Fase 2.5, que ainda não existe).

---

# Central de Ajuda / Manual do usuário (2026-10-01)

## ADR-029 — Conteúdo do manual como dado estático em TypeScript, não CMS/banco
- **Status**: aceito.
- **Problema**: o manual do usuário precisa evoluir junto do sistema (toda funcionalidade nova pede um artigo novo, seção 43/44 do pedido do usuário), sem virar um módulo pesado nem uma segunda fonte de verdade desalinhada do código.
- **Alternativas**: (a) artigos como dado estático versionado no frontend (`features/help/content/*.ts`); (b) tabela no banco com CRUD de administração; (c) arquivos Markdown carregados em runtime.
- **Decisão**: (a).
- **Motivo**: KISS/YAGNI (`DEVELOPMENT_GUIDELINES.md`) — não existe hoje a necessidade de editar o manual sem um deploy, e manter o conteúdo no mesmo repositório/PR que muda o comportamento que ele descreve é o que torna fácil cumprir a regra "feature nova ganha artigo novo". A opção (b) exigiria uma entidade, permissões de CRUD e uma tela de administração só para isso; a opção (c) exigiria um parser de Markdown e perderia a checagem de tipo dos ids relacionados.
- **Impacto**: um teste de integridade de conteúdo (`features/help/content/content.test.ts`) substitui a validação que um banco daria de graça (sem id duplicado, sem link quebrado entre artigos). Se um dia o manual precisar ser editado por alguém que não mexe em código, a decisão deve ser revista.

## ADR-030 — Busca client-side simples, sem biblioteca nova
- **Status**: aceito.
- **Decisão**: `features/help/search.ts` é uma função pura que normaliza o texto (minúsculas, sem acento) e pontua por campo (título > resumo/palavras-chave > passos/exemplo), exigindo que toda palavra da busca apareça em algum campo.
- **Motivo**: o conteúdo é pequeno e estático (não cresce por entrada de usuário) — uma lib de busca (Lunr, Fuse.js, Elasticsearch) seria uma dependência nova para resolver um problema que um `Array.filter` já resolve bem nesse volume.
- **Impacto**: se o manual crescer muito (centenas de artigos) ou pedir busca difusa (erro de digitação, sinônimos automáticos), essa decisão deve ser revisitada.

## Pontos em aberto da Central de Ajuda
- **Testes de interação** (abrir/fechar o drawer, clicar numa categoria, navegar para um artigo): não implementados — o projeto hoje só tem Vitest para função pura no frontend, sem Testing Library/jsdom de componente configurado. Os testes cobrem `search.ts`, `context.ts` e a integridade do conteúdo; a interação, a responsividade e os dois temas foram verificados manualmente.
- **Analytics** (`HelpArticleViewed`, `HelpSearchPerformed`, `HelpSearchNoResult`): só o ponto de extensão (`trackHelpEvent`, hoje loga em desenvolvimento). Sem destino real (produto de analytics) ainda.
- **Screenshots/imagens nos artigos**: a estrutura do artigo (`HelpArticle`) não tem campo de imagem ainda — texto e exemplo em bloco cobrem o conteúdo inicial. Se precisar, adiciona-se um campo opcional sem quebrar os artigos existentes.
