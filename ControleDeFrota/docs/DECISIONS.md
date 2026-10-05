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

---

# Fase 4 — Combustível (2026-10-02)

## ADR-031 — Modelo de combustível: catálogo configurável, posto operacional, abastecimento como snapshot
- **Status**: aceito.
- **Contexto**: o usuário enviou a especificação da Fase 4 diretamente. O roadmap registrava a Fase 2.5 (Viagens) "antes da Fase 4" (ADR-026); seguimos o pedido explícito e a Fase 2.5 continua no backlog — mesmo tratamento do ADR-026. Consequência: não há viagem para associar um abastecimento nem transferência de veículo entre operações.
- **Decisão**:
  - **`FuelType` é um catálogo por empresa** (nome, código, categoria, unidade, ativo), com padrão criado na primeira leitura (mesmo molde do `DocumentType`). O enum `Vehicle.FuelType` (DieselS10, Flex, Híbrido…) **foi mantido** como atributo do motor e renomeado no C# para `VehicleFuelType` — "Flex" e "Híbrido" são características do veículo, não produtos de bomba. A coluna e a API não mudaram (enum como texto). A relação entre os dois é a tabela de compatibilidade `FuelCompatibility` (gera alerta, não bloqueia).
  - **`FuelStation`** é entidade operacional (não fornecedor); `IsInternal` marca o tanque próprio. Estoque do tanque fica fora (a especificação proíbe inventário).
  - **`FuelPrice`** é histórico manual de preço de referência; o preço pago é sempre o do abastecimento.
  - **Forma de pagamento é enum**, não catálogo: conjunto pequeno e estável, sem regra dependente; virar catálogo depois é aditivo.
  - **Sem `FuelingItem`**: um abastecimento = um produto (dois produtos = dois registros), o que mantém o consumo por unidade sem ambiguidade.
  - **Capacidade do tanque e consumo esperado são colunas do `Vehicle`**: são atributos do veículo (como `CargoCapacityKg`), não registros operacionais — a regra "entidade própria com VehicleId" do skill vale para registros com vida (o abastecimento).
- **Preparação para cartão combustível/integrações**: `PaymentMethod.FuelCard` e `Fueling.Source` (`Manual`) existem; um `FuelCard` futuro entra como entidade própria + FK nula `Fueling.FuelCardId`, e importação de fornecedor como novo `Source` + identificador externo — tudo aditivo.

## ADR-032 — Consumo tanque cheio a tanque cheio, com snapshot no abastecimento que fecha o trecho
- **Status**: aceito.
- **Problema**: um abastecimento sozinho não mede consumo (seção 18). Precisamos de uma regra que trate primeiro abastecimento, complementos, correções de hodômetro e registros fora de ordem, e de resultados históricos estáveis (seção 43).
- **Alternativas**: (a) consumo entre abastecimentos consecutivos quaisquer (errado com complementos); (b) calcular na leitura, sempre, a partir dos registros (resultado muda quando a configuração muda; caro em relatórios); (c) **tanque cheio a tanque cheio**, resultado gravado no abastecimento que fecha o trecho.
- **Decisão**: (c). `ConsumptionCalculator` (Domain, função pura) calcula; `FuelConsumptionService` recalcula **só os dois primeiros trechos a partir do ponto alterado**, depois do `SaveChanges`, na mesma transação. O esperado usado (configurado > histórico do veículo > média do tipo — `ConsumptionBaseline`) e o combustível esperado do trecho são gravados junto. Trecho com correção de hodômetro externa, hodômetro em revisão, unidades misturadas ou distância ≤ 0 fica `NotReliable` em vez de mostrar um número enganoso.
- **Abastecimento × hodômetro**: fonte única de verdade continua o `OdometerReading`. Abastecimento atual vira leitura `Source=Fueling` pelas regras do `MileageService` (reaproveitado, não duplicado); abastecimento lançado depois de leituras mais recentes não gera leitura, mas precisa caber entre as vizinhas. O `MileageService` ganhou só métodos aditivos (aprovar/rejeitar dentro da unidade de trabalho do chamador, linha de base pública, checagem histórica) — o comportamento da Fase 2 é o mesmo (todos os testes anteriores passam).
- **Correção do km**: leitura pendente → rejeitada e substituída; leitura válida → leitura `Correction` auditada (exige `mileage.manage` e que seja a mais recente); a data de um abastecimento com leitura não muda (cancelar e registrar de novo) — reordenar o histórico de hodômetro não é uma correção, é outra coisa.
- **Impacto**: relatórios somam colunas gravadas (rápidos e estáveis). Recalcular o passado depois de mudar o esperado de um veículo é uma ação que não existe — de propósito.

## ADR-033 — Agregação no banco; `decimal` como REAL só no SQLite dos testes
- **Status**: aceito.
- **Problema**: a seção 44 exige agregação no banco ("não carregar milhares de abastecimentos para somar"). O SQLite usado nos testes (ADR-004) recusa `SUM/AVG/MIN/MAX` sobre `decimal` — verificado com um teste exploratório antes do desenho.
- **Alternativas**: (a) somar em memória (contraria a seção 44); (b) gravar valores como inteiros escalados (centavos, mililitros) no domínio (modelo estranho, conversões espalhadas); (c) converter `decimal` para `REAL` **apenas quando o provedor é SQLite**.
- **Decisão**: (c), em `FleetDbContext.ConfigureConventions`. SQL Server continua com `decimal(p,s)` exato; as migrations não mudam. Regra derivada: some **colunas**, não expressões calculadas (o SQLite também recusa `SUM(a/b)`) — por isso o combustível esperado do trecho é uma coluna (`SegmentExpectedQuantity`) e as colunas de trecho ficam `NULL` fora dos trechos medidos.
- **Impacto**: as consultas foram validadas nos dois provedores (testes no SQLite + execução real no LocalDB). A restrição antiga "não ordene por decimal" deixa de valer nos testes, mas continua sendo uma boa prática evitar ordenar listas grandes por colunas não indexadas.

## ADR-034 — Visibilidade de custos por registro e alertas neutros
- **Status**: aceito.
- **Decisão**: `fuel.viewcosts` controla todo valor em R$ (preço, total, custo de trecho, custo/km, relatórios de custos e preços); sem ela os campos voltam `null`. **Exceção**: o autor de um abastecimento sempre vê o que registrou (quem digita o preço precisa conferir o que salvou), atendendo à seção 41 ("motorista registra sem ver o gasto da frota"). Comprovantes seguem a mesma regra (mostram o valor pago). Eventos operacionais e mensagens de alerta de preço **não carregam R$**, porque aparecem em telas sem esse controle.
- **Alertas**: nunca bloqueiam (exceto o hodômetro que retrocede, que já era recusado pelo ADR-019); ficam como "Requer revisão" com texto factual, sem acusar ninguém e sem ranking de motoristas (seção 48). Nenhum alerta abre manutenção automaticamente (seção 36).
- **Permissões**: 8 em vez das 11 sugeridas — `Fuel.Edit` virou `fuel.correct` (não há edição livre de um registro operacional) e `Fuel.ViewReports` foi absorvida por `fuel.view` + `fuel.viewcosts` (mesmo molde de `maintenance.viewcosts`). O papel Motorista continua sem permissões (decisão da Fase 2).

## Pontos em aberto da Fase 4
- **Fase 2.5 (Viagens)** continua pendente; abastecimento não se liga a viagem e não há "transferência de veículo" a tratar no consumo.
- **Acesso do motorista**: a tela de abastecimento já é mobile-first, mas o papel Motorista segue sem permissões. Ligar `fuel.create` nele é a mudança quando o acesso do motorista for decidido.
- **Exportação** (Excel/PDF) dos relatórios: não existe no sistema; prevista para a Fase 8.
- **Estoque do tanque próprio** (entradas, saldo, perdas): fora do escopo (sem inventário nesta fase).
- **Cartão combustível e integração com fornecedores**: só a preparação (ADR-031).
- **Recalcular o histórico** depois de mudar o consumo esperado de um veículo: não existe (decisão consciente — os resultados são snapshots). Se o negócio pedir, vira uma ação explícita e auditada.
- **Corretor sem `fuel.viewcosts`** que não é o autor: o preço chega vazio no formulário de correção e precisa ser redigitado. Hoje nenhum papel do sistema tem `fuel.correct` sem `fuel.viewcosts`.
- **Limites por tipo de veículo** (ex.: tolerância diferente para leves e pesados): hoje são por empresa.
- **Revisão visual automatizada**: não executada (o navegador headless travou esta máquina na Fase 1); a revisão de UX/responsividade foi feita no código e precisa de conferência manual em 375px, tablet e desktop, nos dois temas.

---

# Fase 5 — Pneus (2026-10-02)

## ADR-035 — Modelo de pneus: pneu individual, catálogo de modelos e linha do tempo nos eventos operacionais
- **Status**: aceito.
- **Contexto**: o usuário enviou a especificação da Fase 5 diretamente, com a Fase 2.5 (Viagens) ainda no backlog — mesmo tratamento dos ADR-026/031. A especificação lista 13 entidades possíveis e pede para não criá-las às cegas.
- **Decisão**:
  - `Tire` é o pneu físico, com **número de fogo** único por empresa (gerado `PN-000001` ou informado — fleets já têm numeração própria marcada no pneu). Não depende de veículo.
  - `TireModel` concentra marca, modelo, medida e especificações. `TireBrand`/`TireSpecification` não viram tabelas: nada mais as referencia; marca para filtro = `DISTINCT`.
  - Uma única `TireServiceOrder` (`Kind` = conserto | recapagem): o ciclo é o mesmo (enviado → concluído aprovado/reprovado | cancelado); recapagem só acrescenta número, banda e sulco novo.
  - A compra fica no pneu; `TireCost` guarda o resto (gerado ao concluir serviço, ou manual). Custo/km nunca é gravado.
  - **Linha do tempo do pneu = `OperationalEvent` com a coluna nova `TireId`** (índice `(CompanyId, TireId, OccurredAt)`), não uma tabela `TireLifecycleEvent`: reaproveita o histórico/outbox do ADR-025 e a mesma tela de linha do tempo. `OperationalEventLog.RecordAt` grava `OccurredAt` = quando a operação aconteceu (operações podem ser lançadas depois).
  - Fornecedor de conserto/recapagem = `Workshop` da Fase 3 **ou** texto livre (sem cadastro de fornecedores nesta fase).
- **Consequência**: 11 tabelas; módulo isolado em `Domain/Tires` + `Application/Tires`, integrações por métodos aditivos.

## ADR-036 — Ciclo de vida: seis situações, vigência por posição e um estado final
- **Status**: aceito.
- **Problema**: a especificação sugere 11 situações (Available, Installed, Removed, UnderInspection, UnderRepair, UnderRetread, Retreaded, Reserved, Discarded, Sold, Lost) e pede transições controladas.
- **Decisão**: `InStock`, `Installed`, `UnderInspection` (removido/devolvido aguardando decisão — cobre "Removed"), `UnderRepair`, `UnderRetread`, `Disposed` (final). Vendido/extraviado/descartado/transferido são **motivos da baixa**; "Recapado" é o `RetreadCount` de um pneu em estoque; "Reservado" não existe (não há fluxo de reserva — entra junto com almoxarifado). Transições só em `TireWorkflow`; a API devolve as ações possíveis.
- **Instalação como vigência** (`TireInstallation`, instalação e remoção no mesmo registro), nunca uma FK no pneu: um pneu tem muitas, cada uma com os km do período (snapshot na remoção). A posição é guardada como **código + rótulo** (snapshot).
- **Histórico imutável**: nada é editado; a única correção é a de km de uma vigência de veículo, com motivo, evento e auditoria. Datas não são corrigidas (reordenar o histórico não é correção).

## ADR-037 — Posições geradas de configurações de eixos compartilhadas
- **Status**: aceito.
- **Alternativas**: (a) posições fixas (FL/FR/RL/RR) — proibido pela especificação; (b) posições cadastradas uma a uma por veículo — trabalhoso e inconsistente; (c) **configuração de eixos reutilizável** (eixos simples/duplos + estepes) da qual as posições são geradas por função pura.
- **Decisão**: (c). `TireLayout` + `TireLayoutAxle` (tipo, duplo, obrigatório, medida exigida, pressão de referência), apontada por `Vehicle.TireLayoutId` e `Implement.TireLayoutId` — mesmo conceito para veículo e implemento (seção 10). Códigos estáveis em pt-BR (`1E`, `2EE`, `2EI`, `2DI`, `2DE`, `EST1`), gerados igual no servidor (`TirePositions.For`) e na pré-visualização do editor (`lib/tires.previewPositions`). A configuração é atributo do ativo (como o perfil de combustível, ADR-031) e muda por um endpoint próprio da aba Pneus, não pelo formulário do veículo.
- **Proteções**: não se remove posição ocupada (ao editar a configuração ou trocar a do veículo); estepe não soma km.

## ADR-038 — Concorrência e transações garantidas pelo banco
- **Status**: aceito.
- **Problema**: seções 51/52 — duas pessoas instalando o mesmo pneu, rodízio pela metade.
- **Decisão**: índices únicos filtrados (vigência aberta por pneu; por veículo+posição; por implemento+posição; serviço aberto por pneu) + **token de concorrência** `Tires.Version` (coluna int incrementada pela aplicação — `rowversion` não existe no SQLite dos testes, e o token manual funciona nos dois provedores). Toda operação roda em `InTransactionAsync`; liberar e ocupar posições usa dois `SaveChanges` na mesma transação (o EF não ordena comandos por índice filtrado). `TireLifecycle.RunAsync` traduz as falhas em 409 com texto pt-BR.
- **Não escolhido**: lock pessimista (`UPDLOCK`) — específico do SQL Server e desnecessário com o token.

## ADR-039 — Km do pneu pelo hodômetro do veículo; custo/km só quando significa algo
- **Status**: aceito.
- **Decisão**: o pneu não tem hodômetro. Km da vigência = km do veículo na remoção − na instalação, vindos do `MileageService` (linha de base; `OdometerAtAsync` para operação lançada depois; km informado vira leitura `TireService` pelas regras do ADR-019). **Salto suspeito recusa a operação de pneu** (diferente do abastecimento, que fica em revisão): uma vigência não pode começar num km que ninguém confirmou. Implemento sem hodômetro → km desconhecido (`HasUnmeasuredDistance`).
- **Custo/km** = ciclo de vida ÷ km, só com ≥ 5.000 km, total > 0 e sem trecho não medido; caso contrário nulo com explicação (seção 25: "não calcule custo/km enganoso"). O ranking do painel usa a mesma regra.
- **Alertas** calculados na leitura (limites da empresa, valem já); **"requer revisão"** gravado no momento do fato (uma anomalia aberta por tipo), sempre sem causa. Inspeção imprópria só abre solicitação de manutenção com a regra ligada (padrão desligado).

## Pontos em aberto da Fase 5
- **Fase 2.5 (Viagens e engate)** continua pendente — por isso pneu em implemento não tem km e o custo/km desses pneus não aparece.
- **Almoxarifado**: local de armazenamento é texto; não há movimentação de estoque, reserva, compra ou fornecedor (só nome livre). Pneu comprado já recapado: registrar como "usado" com as recapagens.
- **Rodízio no implemento** funciona, mas sem km (mesma razão).
- **Correção** de vigência cobre só km; data/posição errada → remover e instalar de novo com a data certa (a ordem cronológica é protegida).
- **Limites por tipo de veículo/eixo** (ex.: sulco mínimo diferente no direcional): hoje um valor por empresa; a pressão de referência já é por eixo.
- **Sulco por canal**: a inspeção guarda a menor medida; medir 3–4 canais é extensão aditiva (linhas filhas).
- **Exportação** dos relatórios: Fase 8. **Notificações**: eventos prontos para a Fase 9.
- **Revisão visual automatizada**: não executada (headless desaconselhado nesta máquina); conferir o diagrama em 375px/tablet/desktop nos dois temas.

---

# Fase 6 — Financeiro (2026-10-05)

## ADR-040 — Modelo financeiro: sem duplicar custo de outro módulo, sem Fornecedor/Filial genéricos
- **Status**: aceito.
- **Contexto**: o usuário enviou a especificação completa da Fase 6 (38 seções) diretamente — a mesma situação das Fases 3/4/5 (ADR-026/031/035): a Fase 2.5 (Viagens) segue no backlog, mas desta vez o pedido coincide com o próprio título da Fase 6 do roadmap ("Gestão financeira"), então não houve a mesma tensão de pular uma fase recomendada.
- **Problema**: a especificação pede uma entidade `Expense` genérica cobrindo também combustível, manutenção e pneus, um cadastro de Fornecedor e um conceito de Filial — três coisas que, se criadas cegamente, duplicariam dado que já existe (seção 6 do pedido já alerta para isso) ou criariam um segundo sistema de fornecedor/filial sem necessidade real.
- **Decisão**:
  - **`Expense` nunca recebe custo de combustível/manutenção/pneus.** Um novo `CostAggregationService` lê `Fuelings.TotalAmount`, `WorkOrders.TotalCost` e `TireCosts.Amount` direto das tabelas de origem (agregados no banco) e combina com as despesas manuais. Três `ExpenseCategory` de sistema (`IsSystemCategory=true`, `CostAggregationKey`) representam essas três fontes nos relatórios por categoria, sem nunca aceitar um lançamento manual.
  - **Sem Fornecedor genérico.** `Expense`/`RecurringExpense` referenciam opcionalmente a `Workshop` da Fase 3 (quando o fornecedor é uma oficina já cadastrada) ou guardam o nome em texto livre (`SupplierName`) — mesmo molde do `WorkOrderLabor.TechnicianName` (ADR-027): texto livre até existir demanda real de um cadastro.
  - **Sem Branch/Filial.** O exemplo da especificação ("Filial São Paulo" como cost center) já mostra que uma filial é só um `CostCenter` folha — `CostCenter` é hierárquico desde o início, então não há necessidade de uma segunda entidade.
  - **`PaymentMethod` do financeiro é um enum próprio**, distinto do `PaymentMethod` de combustível (ADR-031): o conjunto de formas de pagamento de uma despesa genérica (transferência, PIX, cartões, débito automático…) não é o mesmo de um abastecimento (cartão frota, faturado, tanque próprio).
- **Motivo**: evita exatamente o que a seção 6 da especificação pede para evitar ("não force o usuário a duplicar dado já registrado em outro lugar"), e segue o princípio de escopo deliberadamente reduzido já estabelecido nas Fases 3–5.
- **Impacto**: o skill do projeto foi atualizado para que uma sessão futura nunca crie uma segunda forma de registrar custo de combustível/manutenção/pneus, nem um cadastro de fornecedor/filial, sem antes checar este ADR.

## ADR-041 — Situação de pagamento calculada; despesa nunca é excluída, só cancelada
- **Status**: aceito.
- **Decisão**: `PaymentStatus` (Pendente, Agendado, Parcialmente pago, Pago, Atrasado, Cancelado) nunca é gravado — `ExpensePaymentPolicy.Evaluate` calcula a partir de `CancelledAt`, `Amount`, `PaidAmount` e `DueDate`, mesmo molde do `DocumentExpiryPolicy`/`MaintenanceSchedulePolicy`. Uma despesa mal lançada é **cancelada** (motivo obrigatório), nunca excluída — `Expense` implementa `ISoftDeletable` só pelo padrão comum da entidade auditável, mas o serviço não expõe uma ação de excluir.
- **Motivo**: seção 32 da especificação ("avoid destructive deletion of financial records... prefer cancellation"). Um campo calculado nunca fica desatualizado (a despesa que passa do vencimento vira "Atrasado" sozinha, sem um job que precise rodar).
- **Impacto**: pagamento parcial é suportado (`PaidAmount < Amount`), mas não há conciliação bancária nem múltiplas parcelas por despesa — um valor pago é a soma simples, consistente com "esta fase é sobre controle financeiro, não processamento de pagamento" (seção 10).

## ADR-042 — Totais combinados exigem a permissão de custo de CADA fonte; "totais parciais" em vez de total errado
- **Status**: aceito.
- **Problema**: um total de veículo que mistura combustível, manutenção, pneus e despesas manuais só deveria mostrar R$ de uma fonte para quem tem a permissão de custo **daquela fonte** — do contrário, um usuário com `finance.viewcosts` mas sem `tires.viewcosts` veria o custo de pneus "vazando" pela tela financeira.
- **Decisão**: `CostAggregationService` calcula `CanSeeFuel`/`CanSeeMaintenance`/`CanSeeTires`/`CanSeeOther` como a permissão da fonte **E** `finance.viewcosts` juntas. Uma fonte sem permissão soma zero (nunca lança exceção nem omite a linha), e a resposta carrega `IsPartial = true` quando pelo menos uma fonte foi zerada — a tela mostra um aviso de "totais parciais" em vez de apresentar um total incompleto como se fosse o valor real.
- **Motivo**: o princípio de segurança já estabelecido ("Valores em R$ ficam atrás de uma permissão *.viewcosts decidida no serviço") precisa de uma extensão explícita quando o valor é a SOMA de várias fontes com permissões independentes — sem isso, a combinação mais permissiva (só `finance.viewcosts`) vazaria dado de módulos mais restritos.
- **Impacto**: nos papéis hoje seedados (Finance, FleetManager, Administrator), ninguém tem `finance.viewcosts` sem também ter as três outras — a parcialidade só aparece com uma combinação de permissões personalizada. Documentado porque é o desenho correto independentemente dos papéis atuais.

## ADR-043 — Custo de pneu atribuído ao veículo por correlação em memória (não SQL puro)
- **Status**: aceito.
- **Problema**: `TireCost` não tem `VehicleId` (o pneu é independente do veículo, ADR-035) — o veículo correto é "quem tinha aquele pneu instalado na data do custo", resolvido por `TireInstallations` (intervalo `InstalledAt`/`RemovedAt`).
- **Alternativas**: (a) subconsulta correlacionada comparando `DateOnly` (do custo) com `DateTime` (da instalação) direto no SQL; (b) resolver em memória, sobre a lista de custos do período e das instalações dos pneus envolvidos.
- **Decisão**: (b). EF Core 8 não garante a tradução de uma comparação `DateOnly`×`DateTime` correlacionada em todo provedor (o mesmo motivo do ADR-033 para `decimal`), e o volume de `TireCost` (consertos, recapagens, compras) é pequeno o bastante — ao contrário de `Fuelings`/`WorkOrders`, que continuam 100% agregados no banco — para que resolver em memória não viole o espírito da regra "agregue no banco, não carregue para somar" do skill.
- **Impacto**: documentado como desvio consciente, não um descuido. Validado por teste (`TireCost_AttributedToVehicleInstalledAtTheTime_NotToAVehicleOutsideTheInstallationWindow`): um custo incorrido enquanto o pneu estava no veículo A não aparece no custo do veículo B, mesmo que o pneu tenha sido transferido depois.

## ADR-044 — Custo por km e TCO: limiares próprios, nunca um número enganoso
- **Status**: aceito.
- **Decisão**: `VehicleCostPolicy.MinKmForCostPerKm = 50` km para o custo por km de um **período** (mês/ano) — bem menor que o `TireCostPolicy.MinKmForCostPerKm = 5.000` km, porque este último mede o ciclo de vida inteiro de um pneu (anos), não um mês de um veículo. Km do período vem do histórico de hodômetro já existente (`MileageService.OdometerAtAsync`), nunca um módulo de quilometragem novo. TCO = `Vehicle.AcquisitionValue` (campo que já existia desde a Fase 1) + custo operacional acumulado desde `AcquisitionDate` (ou desde o cadastro, se a data não foi informada); é explicitamente uma análise de gestão operacional, não um cálculo contábil/fiscal de depreciação (seção 16 do pedido).
- **Anomalias (seção 20 do pedido)**: sem um subsistema novo. Reaproveita dois padrões já existentes: alerta computado no painel principal (`DashboardAlert`, "Despesa em atraso", igual aos alertas de documento/pneu) + evento operacional `BudgetExceeded` emitido quando uma despesa nova ultrapassa o orçamento da categoria/período; e sinal calculado na leitura (`ExpenseResponse.IsDuplicateSuspect`), nunca gravado, sem acusar ninguém ("possível duplicidade").
- **Motivo**: menos um subsistema (entidade de anomalia + workflow de revisão) a manter, reaproveitando exatamente a infraestrutura que o dashboard e o fuel/tire já usam para o mesmo tipo de sinal.

## Pontos em aberto da Fase 6
- **Exportação** dos relatórios: Fase 8, como as demais.
- **Multas e sinistros**: o roadmap deixa em aberto se entram no financeiro ou na Fase 2 — por ora, uma multa é só mais uma categoria de despesa manual, sem fluxo próprio.
- **Fornecedor genérico**: se o negócio precisar de um cadastro de fornecedores além da oficina, ele entra como entidade própria referenciada por `Expense`/`RecurringExpense` (ADR-040), sem mudar o modelo atual.
- **Rateio automático** de uma despesa entre vários veículos/centros de custo: hoje uma despesa pertence a no máximo um veículo e um centro de custo; dividir uma despesa única entre vários exigiria um conceito de "rateio" ainda não modelado.
- **Revisão visual automatizada**: não executada (headless desaconselhado nesta máquina); conferir o painel, a aba Financeiro do veículo e os formulários em 375px/tablet/desktop, nos dois temas.
