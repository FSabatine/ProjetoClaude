# Domínio

Linguagem ubíqua: o código usa **inglês** e a interface usa **português**. Esta tabela é o dicionário oficial.

| Código | Interface | Significado |
|---|---|---|
| Company | Empresa | Tenant: cada empresa enxerga apenas os próprios dados |
| User | Usuário | Pessoa que acessa o sistema |
| Role | Papel / Perfil de acesso | Conjunto nomeado de permissões |
| Permission | Permissão | Direito atômico de executar uma ação (`vehicles.update`) |
| Driver | Motorista | Condutor cadastrado (não é necessariamente usuário do sistema) |
| Vehicle | Veículo | Unidade motorizada da frota |
| Implement | Implemento | Reboque, semirreboque, tanque, baú, sider, dolly… |
| License (CNH) | CNH | Carteira Nacional de Habilitação |
| EAR | EAR | "Exerce Atividade Remunerada", anotação na CNH |
| Odometer | Hodômetro | Quilometragem atual |
| Hour meter | Horímetro | Horas de uso do motor |
| VehicleAssignment | Alocação | Motorista responsável por um veículo durante um período |
| OdometerReading | Leitura de hodômetro | Um registro do histórico de quilometragem |
| DocumentType / Document | Tipo de documento / Documento | CRLV, seguro, exame, licença… com vencimento |
| ChecklistTemplate / ChecklistExecution | Modelo de checklist / Checklist realizado | Inspeção configurável e a sua execução |
| Occurrence | Ocorrência | Problema, avaria ou observação operacional |
| OperationalEvent | Evento operacional | Fato ocorrido na operação (histórico + base das notificações) |
| Operational status | Situação operacional | Situação exibida do veículo: condição + alocação |

## Mapa de relacionamentos

```
Company ─┬─< User >─< Role >─< Permission
         ├─< DocumentType ─< Document >── (Vehicle | Driver | Implement | Company)
         ├─< Driver ──┐
         ├─< Vehicle ─┴─< VehicleAssignment (vigência)      ···(futuro)··· Fueling, Tires, Trips, Costs
         │    ├─< OdometerReading
         │    ├─< HourMeterReading
         │    ├─< ChecklistExecution ─< ChecklistAnswer (snapshot) ──> Occurrence
         │    ├─< Occurrence ─< StoredFile (fotos) ──> MaintenanceRequest (manual)
         │    ├─< MaintenanceRequest ──> WorkOrder (aprovação)
         │    ├─< WorkOrder ─< WorkOrderItem | WorkOrderPart | WorkOrderLabor
         │    └─< MaintenanceSchedule >─ MaintenancePlanItem
         ├─< ChecklistTemplate ─< ChecklistTemplateItem
         ├─< MaintenancePlan ─< MaintenancePlanItem (padrão | por VehicleType | por Vehicle)
         ├─< Workshop ──> WorkOrder
         ├─< Implement ···(futuro)··· VehicleImplementCoupling (vínculo veículo ↔ implemento com vigência)
         └─< OperationalEvent (histórico + outbox, sem FKs)
```

## Company (Empresa)

- O CNPJ é obrigatório, com dígitos verificadores válidos e único entre empresas não excluídas. São aceitos o **CNPJ numérico e o alfanumérico** (novo formato da Receita Federal, vigente desde jul/2026).
- A razão social e o endereço completo (logradouro, número, bairro, cidade, UF, CEP) são obrigatórios.
- Uma empresa **inativa** impede o login e a renovação de sessão de todos os seus usuários.
- Um usuário não pode inativar nem excluir a **própria** empresa.
- Não se exclui uma empresa que ainda tem usuários ativos. O caminho normal é inativar.
- Só usuários com `companies.manage` (super-admin da plataforma) criam, listam todas e excluem empresas. Com `companies.update`, o usuário edita **apenas a própria** empresa.

## User (Usuário)

- O e-mail é o login: **único no sistema inteiro**, armazenado em minúsculas.
- Pertence a exatamente uma empresa (`CompanyId`). Só quem tem `companies.manage` pode criar ou mover usuários para outra empresa.
- Deve ter **pelo menos um papel**. As permissões efetivas são a **união** das permissões de todos os papéis.
- **Anti-escalonamento**: ninguém atribui um papel que contenha permissão que ele próprio não tem.
- Ninguém inativa, exclui ou remove os próprios papéis (evita perder o último acesso administrativo por acidente).
- Status: `Active` ou `Inactive`. Inativar ou excluir revoga imediatamente todas as sessões (refresh tokens).
- Senha: mínimo de 10 e máximo de 128 caracteres, com letras e números, e não pode conter o e-mail. Ver SECURITY.md.
- Bloqueio temporário de 15 minutos após 5 tentativas de login seguidas sem sucesso.

## Role e Permission (Papel e Permissão)

- **Toda decisão de acesso é por permissão, nunca pelo nome do papel.** Não pode existir `if (role == "Administrator")` no código.
- O catálogo de permissões está no código (`Fleet.Domain/Authorization/Permissions.cs`) e é semeado no banco. Criar uma permissão = constante + seed + migration + uso no endpoint.
- Nesta fase, papéis são do **sistema** (`IsSystem = true`) e somente leitura. Papéis personalizados por empresa estão previstos (coluna `Roles.CompanyId`).

### Catálogo de permissões (Fase 1)

| Módulo | Permissões |
|---|---|
| dashboard | `dashboard.view` |
| companies | `companies.view` (própria), `companies.update` (própria), `companies.manage` (todas; plataforma) |
| users | `users.view`, `users.manage` |
| roles | `roles.view` |
| drivers | `drivers.view`, `drivers.create`, `drivers.update`, `drivers.delete` |
| vehicles | `vehicles.view`, `vehicles.create`, `vehicles.update`, `vehicles.delete` |
| implements | `implements.view`, `implements.create`, `implements.update`, `implements.delete` |
| audit | `audit.view` |
| assignments (Fase 2) | `assignments.view`, `assignments.manage` |
| mileage (Fase 2) | `mileage.record` (registrar leitura), `mileage.manage` (revisar suspeitas e corrigir) |
| documents (Fase 2) | `documents.view`, `documents.manage` (cadastrar, editar, renovar, anexar), `documents.delete` |
| checklists (Fase 2) | `checklists.view`, `checklists.execute` |
| occurrences (Fase 2) | `occurrences.view`, `occurrences.create`, `occurrences.manage` (editar, analisar, resolver, cancelar) |
| operations (Fase 2) | `operations.configure` (tipos de documento e modelos de checklist) |

### Papéis do sistema

| Papel (Key) | Nome na tela | Permissões |
|---|---|---|
| PlatformAdministrator | Administrador da plataforma | todas |
| Administrator | Administrador | todas, exceto `companies.manage` |
| FleetManager | Gestor de frota | dashboard, `companies.view`, `users.view`, `roles.view`, drivers/vehicles/implements completos, `audit.view` e **todas** as permissões operacionais da Fase 2 |
| Operations | Operações | dashboard; drivers view/create/update; vehicles e implements view/update; alocações; `mileage.record`; `documents.view/manage`; checklists view/execute; ocorrências view/create/manage. Não exclui documentos, não corrige hodômetro e não configura |
| Maintenance | Manutenção | dashboard; vehicles e implements view/update; `occurrences.view` |
| Finance | Financeiro | dashboard; drivers, vehicles e implements view |
| Driver | Motorista | nenhuma nesta fase. Decisão do usuário (2026-09-30): o acesso do motorista fica para uma fase futura, e gestores e operações executam os checklists pelo motorista |
| Viewer | Visualizador | dashboard, `companies.view`, drivers/vehicles/implements view e as permissões `*.view` operacionais (alocações, documentos, checklists, ocorrências) |

## Driver (Motorista)

- CPF com DV válido, único por empresa. O mesmo motorista pode existir em duas empresas diferentes (tenants independentes).
- Número de registro da CNH com 11 dígitos, único por empresa.
- Categoria da CNH: `A, B, C, D, E, AB, AC, AD, AE`.
- Idade entre 18 e 100 anos na data do cadastro.
- **CNH vencida ou a vencer em até 30 dias gera alerta** no dashboard. Nesta fase não bloqueia o cadastro, porque o bloqueio de uso (ex.: não iniciar viagem) pertence ao módulo de Viagens.
- Status: `Active` (Ativo), `OnLeave` (Afastado: férias, licença), `Inactive` (Desligado).
- Endereço opcional. Se informados, o CEP e a UF precisam ser válidos.
- Um motorista **desligado** (`Inactive`) não pode ter alocação ativa: encerre a alocação antes de desligá-lo. O **afastado** mantém a alocação existente (o veículo fica reservado), mas não recebe nova.
- Não se exclui motorista com histórico operacional (alocações, checklists, ocorrências); o caminho é "Desligado". Os documentos dele são excluídos junto.
- **Fora de escopo**: viagens, multas, sinistros e o app do motorista. Exames e cursos agora são **documentos** (Fase 2).

## Vehicle (Veículo)

- Placa no padrão antigo (`ABC1234`) ou Mercosul (`ABC1D23`), gravada sem hífen e em maiúsculas.
- **Placa única por empresa entre veículos E implementos** (no Brasil, a placa identifica um único veículo registrado).
- RENAVAM com 11 dígitos e DV válido. Chassi (VIN) com 17 caracteres, sem I, O e Q. Ambos únicos por empresa.
- Ano de fabricação entre 1950 e o ano corrente + 1. Ano do modelo igual ao de fabricação ou ao seguinte.
- Hodômetro, horímetro, capacidade, tara e valor de aquisição ≥ 0. A data de aquisição não pode ser futura.
- **Condição gravada** (`Status`; ADR-009 e ADR-018), um único eixo:
  - `Available` (Disponível): pode ser usado.
  - `OnTrip` (Em viagem): em operação.
  - `UnderMaintenance` (Em manutenção): indisponível por manutenção.
  - `Unavailable` (Indisponível, **Fase 2**): fora de uso por outro motivo (documento bloqueado, sinistro, aguardando papelada).
  - `Inactive` (Inativo): fora da frota (vendido, sinistrado, baixado).
  - **"Ativo" é derivado** = qualquer condição diferente de `Inactive`.
  - `OnTrip` e `UnderMaintenance` ainda são manuais. Passarão a ser controlados pelos módulos de Viagens e Manutenção.
- **Situação operacional** (derivada, nunca gravada; ADR-018) = condição + alocação ativa:

  | Condição | Tem motorista? | Situação operacional |
  |---|---|---|
  | Inactive | — | Inativo |
  | UnderMaintenance | — | Em manutenção |
  | Unavailable | — | Indisponível |
  | OnTrip | — | Em viagem |
  | Available | sim | **Alocado** |
  | Available | não | Disponível |

  "Alocado" **não** é gravado no veículo: é a combinação "disponível + motorista ativo". Assim, um veículo alocado que entra em manutenção e sai volta a aparecer como Alocado, sem nenhuma regra extra.
- Não se exclui um veículo `OnTrip`, nem um veículo com histórico operacional (alocações, checklists, ocorrências): inative-o. Os documentos são excluídos junto com o veículo.
- Um veículo com motorista alocado não pode ser inativado: encerre a alocação antes.
- O hodômetro é informado **só no cadastro**, como a primeira leitura do histórico. Depois disso, muda apenas pelo registro de leituras (ver "Leituras de hodômetro").
- Mudar a condição gera o evento `VehicleStatusChanged`.

## Implement (Implemento)

- Tipos: `Trailer` (Reboque), `SemiTrailer` (Semirreboque), `Tanker` (Tanque), `BoxBody` (Baú), `Sider`, `Dolly`, `Other` (Outro).
- Capacidade com unidade explícita (`Kg`, `Liters`, `CubicMeters`), porque um tanque é medido em litros e um baú em kg ou m³.
- As mesmas regras de placa, RENAVAM, chassi e anos do veículo. A placa não pode colidir com a de um veículo.
- Status: `Available`, `InUse` (Em uso / engatado), `UnderMaintenance`, `Inactive`.
- **Preparação**: o vínculo veículo ↔ implemento será uma entidade própria com vigência (`VehicleImplementCoupling`: VehicleId, ImplementId, CoupledAt, UncoupledAt), não uma FK em `Implement`, para preservar o histórico de composições.

## Dashboard (Fase 1)

- Indicadores da Fase 1: veículos ativos, motoristas ativos, veículos disponíveis e veículos em manutenção.
- Alertas recentes: CNH vencida e CNH vencendo em até 30 dias.
- Cada indicador é um método independente no `DashboardService`, para que novos KPIs (combustível, custos, documentos) sejam acrescentados sem mexer nos existentes.


---

# Fase 2 — Controle operacional

## Status: quem tem qual (ADR-018)

| Entidade | Campo gravado | Valores | Observação |
|---|---|---|---|
| Vehicle | `Status` (condição) | Available, OnTrip, UnderMaintenance, Unavailable, Inactive | + situação operacional derivada (Alocado) |
| Driver | `Status` | Active, OnLeave, Inactive | "Com veículo" é derivado da alocação ativa |
| Implement | `Status` | Available, InUse, UnderMaintenance, Inactive | sem mudança (o engate veículo ↔ implemento fica para uma fase futura) |
| VehicleAssignment | `EndedAt` nulo = ativa | — | sem campo de status: a vigência basta |
| OdometerReading | `Status` | Valid, PendingReview, Rejected | |
| Document | calculado | Valid, ExpiringSoon, Expired, NoExpiration, Replaced | nunca gravado |
| Occurrence | `Status` | Open, InAnalysis, Resolved, Cancelled | máquina de estados explícita |
| ChecklistExecution | `Result` | Approved, Failed | |

Nenhum campo representa dois conceitos: a condição do veículo não diz quem o dirige, e a alocação não diz se ele está em manutenção.

## VehicleAssignment (alocação motorista ↔ veículo) — ADR-020

- Registra o motorista **responsável** pelo veículo durante um período (`StartedAt` → `EndedAt`; `EndedAt` nulo = ativa). Nesta fase não há motorista secundário.
- **O histórico nunca é sobrescrito**: trocar o motorista encerra a alocação atual e abre outra.
- Regras (decididas com o usuário em 2026-09-30):
  1. Um veículo tem no máximo **uma** alocação ativa.
  2. Um motorista tem no máximo **uma** alocação ativa (um veículo por motorista).
  3. Veículo **inativo** não recebe motorista. Em manutenção ou indisponível pode: o motorista continua responsável.
  4. Motorista **desligado** ou **afastado** não recebe nova alocação.
  5. Motorista com **CNH vencida** não recebe alocação (CTB). A CNH que vence hoje ainda é válida.
  6. Início e fim não podem ser futuros, porque não há alocação agendada nesta fase (tolerância de 5 minutos para o relógio do cliente). O fim não pode ser anterior ao início.
  7. Um registro retroativo não pode se sobrepor a uma alocação já encerrada do mesmo veículo ou motorista.
  8. **Troca com confirmação**: se o veículo já tem motorista ou o motorista já tem veículo, a API responde **409** explicando o motivo ("o veículo está com Maria…"). A UI mostra a confirmação e reenvia com `endCurrent = true`. A alocação anterior é encerrada no mesmo instante em que a nova começa, numa única transação.
- Os índices únicos filtrados (`EndedAt IS NULL`) garantem as regras 1 e 2 mesmo com duas requisições simultâneas.
- Um veículo alocado não pode ser inativado, e um motorista alocado não pode ser desligado, sem antes encerrar a alocação. Um motorista afastado mantém a alocação.
- Eventos: `VehicleAssigned`, `VehicleAssignmentEnded`. Auditoria automática.

## OdometerReading (leituras de hodômetro) — ADR-019

- Cada leitura guarda: km, data e hora da leitura, **origem** (`Registration` no cadastro, `Manual`, `Checklist`, `Correction`), quem registrou (`CreatedBy`), situação e observações.
- `Vehicle.CurrentOdometerKm` + `OdometerUpdatedAt` são a leitura rápida da **última leitura válida**. Só o `MileageService` altera esses campos.
- **Linha de base** = última leitura válida (por data da leitura). Veículos anteriores ao histórico usam o hodômetro gravado no cadastro.
- Regras:
  1. **Não retroceder**: uma leitura menor que a linha de base é recusada (400, com a leitura anterior na mensagem). Uma leitura igual é aceita (veículo parado).
  2. A data da leitura não pode ser futura nem anterior à da última leitura registrada.
  3. **Salto suspeito**: média acima de **1.500 km/dia** desde a linha de base, contando no mínimo 1 dia. Um dígito a mais equivale a um salto de 10×. A leitura é gravada como `PendingReview` e **não altera o hodômetro atual** (decisão do usuário). As leituras seguintes continuam comparadas com a última válida, então um erro de digitação não trava a operação.
  4. **Revisão** (`mileage.manage`): aprovar aplica a leitura ao veículo, desde que ela ainda seja a mais recente e não retroceda. Rejeitar exige justificativa. As duas ações ficam registradas (`ReviewedAt/By/Notes`).
  5. **Correção** (`mileage.manage`, motivo obrigatório): pode ser menor que a anterior (troca de painel, erro antigo) e passa a ser a nova linha de base. Fica auditada na leitura e no veículo (valor antigo → novo) e gera o evento `MileageCorrected`.
  6. Leituras nunca são editadas nem excluídas.
- Constantes em `Fleet.Domain/Mileage/OdometerPolicy`: `MaxPlausibleKmPerDay = 1500` e `StaleAfterDays = 7` (prazo para "sem leitura recente").
- Eventos: `MileageRecorded`, `MileageAnomalyDetected`, `MileageCorrected`, `MileageReviewed`.

## Documentos — ADR-021

- **DocumentType** é um catálogo por empresa, configurável com `operations.configure`: nome, a quem se aplica (veículo, motorista, implemento ou empresa), se tem validade e a **antecedência do alerta** (0 a 365 dias). O nome é único por empresa + dono. Um tipo com documentos não muda de dono nem é excluído; é inativado.
- Toda empresa começa com um catálogo padrão (`DocumentTypeDefaults`): CRLV/licenciamento, seguro, aferição do tacógrafo, inspeção, exame toxicológico, ASO, MOPP, treinamentos, CIV/CIPP, RNTRC, licença ambiental, alvará e "outros". Empresas criadas antes da Fase 2 recebem o catálogo na primeira leitura.
- **A CNH não é um tipo de documento**. A validade dela continua no cadastro do motorista (fonte única), e os alertas de CNH seguem como na Fase 1.
- **Document**: tipo, dono (`OwnerType` + a FK correspondente; documento da empresa não tem FK), número, emissão, vencimento, observações e arquivos.
- Regras: vencimento obrigatório se o tipo tem validade e proibido se não tem; emissão não futura; vencimento ≥ emissão; o dono precisa existir na empresa.
- **Status calculado**, com a regra num único lugar (`DocumentExpiryPolicy`):
  - `Expired`: vencimento < hoje.
  - `ExpiringSoon`: hoje ≤ vencimento ≤ hoje + antecedência do tipo.
  - `Valid`: vencimento > hoje + antecedência.
  - `NoExpiration`: o tipo não vence.
  - `Replaced`: substituído por uma renovação (não alerta).
  - O primeiro dia da janela de alerta (`AlertStartsOn`) é gravado no documento, para que as consultas apliquem as mesmas regras comparando só datas. Mudar a antecedência de um tipo recalcula os documentos dele.
- **Renovação**: um novo documento com `replacesDocumentId` marca o anterior como substituído. Ele sai dos alertas e fica no histórico.
- Excluir exige `documents.delete`: é soft delete, auditado, e gera `DocumentDeleted`.
- Eventos: `DocumentCreated`, `DocumentRenewed`, `DocumentDeleted`, `DocumentExpiring` e `DocumentExpired`. Os dois últimos vêm do job de vencimentos e são emitidos uma vez por mudança de estado.

## Arquivos — ADR-022

- Formatos aceitos: **PDF, JPG e PNG**, reconhecidos pela assinatura do conteúdo (*magic bytes*), não pelo nome. Até **10 MB** por arquivo e no máximo 10 por registro.
- Fluxo: o arquivo é enviado (`POST /files`) e fica **sem dono**, visível só para quem enviou. Ao salvar o documento, a ocorrência ou a resposta do checklist, ele é vinculado ao registro.
- Quem pode abrir segue o dono: documento → `documents.view`; ocorrência → `occurrences.view`; foto de checklist → `checklists.view` ou `occurrences.view`.
- Fotos de um checklist enviado não podem ser removidas: são o registro da inspeção.

## Checklists — ADR-024

- **ChecklistTemplate** é configurável com `operations.configure`: nome único, descrição, frequência (`OnDemand`, `Daily`, `Weekly`), ativo e **versão**. Cada item tem seção, pergunta, tipo de resposta (`PassFail` = Conforme / Não conforme / Não se aplica; `Number`, com unidade; `Text`), obrigatoriedade, "exigir foto se não conforme" e o tipo e a gravidade da ocorrência aberta em caso de falha.
- Alterar os itens incrementa a versão. Um envio feito sobre uma versão antiga é recusado (409, "recarregue").
- **ChecklistExecution** guarda um **snapshot** de cada pergunta (seção, texto, tipo, obrigatoriedade, unidade), além do nome e da versão do modelo. Alterar ou excluir o modelo depois não muda a execução. Execuções são imutáveis.
- Validação no envio: todo item obrigatório respondido ("Não se aplica" conta como resposta), resposta compatível com o tipo, foto quando exigida e nenhum item de outro modelo. Os erros voltam por item (`answers.{itemId}`).
- Veículo inativo não é inspecionado. O motorista padrão é o alocado ao veículo.
- **Hodômetro opcional**: passa pelas mesmas regras do `MileageService` (origem `Checklist`) e é salvo junto com a execução. Registrar o hodômetro no checklist não exige `mileage.record`, porque faz parte da inspeção.
- **Item reprovado → ocorrência**: cada "Não conforme" abre uma ocorrência com origem `Checklist`, o tipo e a gravidade do item (ajustável no momento), ligada à execução e à resposta. As fotos do item aparecem na ocorrência.
- **Checklists pendentes**: para cada modelo ativo diário ou semanal, são os veículos **em operação** (com motorista alocado e condição Disponível ou Em viagem) sem execução no período atual (hoje, ou desde segunda-feira). Um veículo de pool sem motorista não é cobrado, porque ainda não há módulo de viagens para saber se ele foi usado.
- Eventos: `ChecklistCompleted`, `ChecklistFailed` e um `OccurrenceCreated` por item reprovado.

## Occurrence (ocorrência operacional) — ADR-023

- Tipos: problema mecânico, pneu, acidente, avaria, equipamento faltando, documentação, relato do motorista e observação geral. Gravidade: baixa, média, alta ou **crítica** (impede o uso seguro).
- Precisa envolver ao menos um veículo, motorista ou implemento. A data não pode ser futura e a descrição é obrigatória.
- **Máquina de estados**:

  ```
  Open ──> InAnalysis ──> Resolved
    │           └───────> Cancelled
    ├──────────────────> Resolved
    └──────────────────> Cancelled
  ```

  `Resolved` e `Cancelled` são **finais** e exigem texto (a resolução ou o motivo). Um problema que volta vira uma nova ocorrência. Não há exclusão: um registro indevido é cancelado. Os detalhes só podem ser editados enquanto a ocorrência não foi encerrada.
- Isto não é o módulo de sinistros nem o de manutenção: é a porta de entrada que eles vão consumir (por exemplo, ordens de serviço criadas a partir de ocorrências abertas).
- "Em aberto" = `Open` + `InAnalysis`. Eventos: `OccurrenceCreated`, `OccurrenceStatusChanged`.

## Histórico operacional e eventos — ADR-025

- Tudo o que acontece na operação vira um **OperationalEvent**, gravado **na mesma transação** da mudança: tipo, data, usuário, veículo/motorista/implemento envolvidos, o registro de origem, uma frase em pt-BR congelada no momento e um payload JSON.
- A mesma tabela serve de **linha do tempo** do veículo e do motorista e de **outbox** das notificações futuras (`PublishedAt` nulo = ainda não entregue).
- Catálogo: `VehicleAssigned`, `VehicleAssignmentEnded`, `VehicleStatusChanged`, `MileageRecorded`, `MileageAnomalyDetected`, `MileageCorrected`, `MileageReviewed`, `DocumentCreated`, `DocumentRenewed`, `DocumentDeleted`, `DocumentExpiring`, `DocumentExpired`, `ChecklistCompleted`, `ChecklistFailed`, `OccurrenceCreated`, `OccurrenceStatusChanged`.
- Um módulo futuro **acrescenta valores** ao enum e chama `OperationalEventLog.Record`. Nenhum módulo chama outro para "avisar".
- Diferença para a auditoria: `AuditLogs` responde "quem mudou qual campo"; o histórico operacional responde "o que aconteceu com este veículo".

## Dashboard (Fase 2)

- **Frota por situação operacional**: disponível, alocado, em viagem, indisponível, em manutenção e inativo (a soma dá o total).
- **Atenção** (cada número exige a permissão do módulo): documentos vencidos e vencendo, checklists pendentes hoje, ocorrências em aberto (e quantas são críticas) e veículos sem leitura de hodômetro há mais de 7 dias.
- **Quilometragem do mês**:
  - km rodados pela frota: por veículo, a maior leitura válida do mês menos a maior leitura válida anterior ao mês (ou a primeira do mês);
  - média por veículo ativo;
  - veículo com o maior hodômetro.
- **Alertas**: os 10 mais urgentes, críticos primeiro. Incluem CNH (com `drivers.view`), documentos (com `documents.view`; o nome do motorista só aparece com `drivers.view`), ocorrências críticas em aberto (`occurrences.view`), leituras suspeitas (`mileage.manage`) e, na Fase 3, ordens de serviço críticas em aberto (`maintenance.view`).

---

# Fase 3 — Manutenção

## Catálogo de permissões (Fase 3)

| Módulo | Permissões |
|---|---|
| maintenance | `maintenance.view`, `maintenance.createrequest` (solicitar manutenção), `maintenance.manageplans` (planos preventivos), `maintenance.manageworkorders` (aprovar solicitações, criar/editar/executar/fechar ordens), `maintenance.manageworkshops`, `maintenance.viewcosts` (peças, mão de obra e custo total) |

Papel **Manutenção** (`SystemRoles.Maintenance`) ganhou todas as permissões acima, além das que já tinha (veículos/implementos view+update, ocorrências view). **Gestor de frota**/**Administrador** ganham todas. **Operações** ganha `maintenance.view` + `maintenance.createrequest` (reporta problemas, não aprova). **Financeiro** ganha `maintenance.view` + `maintenance.viewcosts`.

## Workshop (Oficina)

- `Name`, `Document` (CPF/CNPJ, opcional), `Phone`, `Email`, `Address`, `Specialties` (texto livre — não é uma tabela de tags), `Status` (`Active`/`Inactive`), `Notes`. Catálogo por empresa, soft delete.
- Não se exclui oficina com ordens de serviço registradas; o caminho é inativar.

## MaintenancePlan / MaintenancePlanItem (Plano de manutenção preventiva)

- Um plano tem `VehicleId` **ou** `VehicleType` **ou** nenhum dos dois (plano padrão da empresa) — nunca os dois ao mesmo tempo. **Precedência** quando mais de um se aplica a um veículo: veículo específico > tipo de veículo > padrão da empresa (`MaintenancePlanResolver`).
- Cada item (`MaintenancePlanItem`) define o serviço e pelo menos um intervalo: `IntervalKm`, `IntervalMonths` e/ou `IntervalHours`. **O que vencer primeiro** dispara a manutenção. Cada eixo tem sua própria carência (`GraceKm`/`GraceDays`/`GraceHours`): tolerância depois do vencimento e também janela de aviso antes dele.
- Prioridade (`Low/Medium/High/Critical`), duração e custo estimados, se é obrigatório, observações.
- Não se remove um item que já tem manutenção registrada (teria que apagar histórico); o caminho é deixar um intervalo bem longo ou inativar o plano inteiro.

## MaintenanceSchedule (Agenda de manutenção)

- Uma linha por (veículo, item do plano), criada **só depois da primeira manutenção feita** naquele item — veículos ainda não atendidos não têm linha; a agenda deles é calculada a partir da data de cadastro/hodômetro inicial.
- Guarda a última manutenção (`LastPerformedOn/Km/Hours`) e a próxima calculada (`NextDueOn/Km/Hours`) — são recalculadas quando uma `WorkOrderItem` ligada ao item do plano é concluída.
- **Status nunca é gravado**: `Scheduled`, `DueSoon`, `Due` ou `Overdue`, calculado por `MaintenanceSchedulePolicy` a partir dos valores atuais do veículo. O eixo mais urgente entre os configurados decide o status final.

## HourMeterReading (Histórico de horímetro)

- Mesmo modelo do hodômetro (ADR-019/ADR-027): append-only, `Source` (`Registration/Manual/WorkOrder/Correction`), `Status` (`Valid/PendingReview/Rejected`), não retrocede, salto suspeito (acima de 20 h/dia) fica `PendingReview` e não é aplicado.
- `Vehicle.HourMeter`/`HourMeterUpdatedAt` só mudam por `HourMeterService` — a edição do veículo não altera mais o horímetro diretamente (antes da Fase 3 era editável livremente).

## MaintenanceRequest (Solicitação de manutenção)

- Origem (`Source`): motorista, checklist, gestor, ocorrência ou alerta automático. Quando vem de uma ocorrência (`OccurrenceId` preenchido), a origem é forçada para `Occurrence` no servidor.
- Tipo (`Preventive/Corrective/Inspection`), prioridade, descrição, hodômetro/horímetro no momento do relato.
- Situação: `Open → Converted | Rejected` (ambos finais). **Aprovar cria a ordem de serviço na mesma transação** — não existe "aprovada, sem ordem ainda".
- **Não é automático**: uma ocorrência aberta não vira solicitação sozinha — é um botão que o gestor aciona, porque nem toda ocorrência precisa de manutenção.

## WorkOrder (Ordem de serviço)

- Número sequencial por empresa (`OS-000001`). Tipo (`Preventive/Corrective/Inspection`), prioridade, oficina (opcional), veículo (e implemento, opcional, sem o vínculo formal — ver DECISIONS).
- **Situação** (`WorkOrderStatus`), máquina de estados explícita (`WorkOrderWorkflow`):

  ```
  Draft ──> Approved ──> Scheduled ──> InProgress ⇄ WaitingParts ──> Completed
    │           │              │             │
    └───────────┴──────────────┴─────────────┴──> Cancelled | Rejected
  ```

  `Completed`/`Cancelled`/`Rejected` são finais. Concluir exige o texto da resolução; cancelar/rejeitar exige o motivo. A API devolve `nextStatuses`, como a Ocorrência.
- **Itens** (`WorkOrderItem`): tarefas da ordem, cada uma `Pending/Done/Skipped`; quando ligada a um item de plano preventivo, concluir a ordem recalcula a `MaintenanceSchedule` dele. **Itens obrigatórios pendentes impedem concluir a ordem.**
- **Peças** (`WorkOrderPart`) e **mão de obra** (`WorkOrderLabor`, técnico em texto livre) são linhas de custo, somadas em `PartsCost`/`LaborCost`/`TotalCost` (+ `OtherCost` livre) a cada alteração.
- **Tempo de indisponibilidade** (`DowntimeMinutes`): `CompletedAt − StartedAt`, calculado ao fechar.
- **Controle de `Vehicle.Status`** (ADR-028): entrar em `InProgress`/`WaitingParts` pela primeira vez muda o veículo para `UnderMaintenance` (se estava `Available`); sair delas só devolve `Available` se não restar outra ordem ativa e ninguém mudou o status manualmente nesse meio tempo. Veículo `OnTrip`/`Inactive` recusa iniciar manutenção.
- Depois que a ordem sai de `Draft/Approved/Scheduled` (ou seja, já começou), a edição geral (`PUT`) é bloqueada — só as ações incrementais (itens, peças, mão de obra, situação) continuam disponíveis, para não apagar o progresso já registrado.
- Custos (`PartsCost/LaborCost/OtherCost/TotalCost`, `UnitCost` da peça, `HourlyRate` da mão de obra) só aparecem para quem tem `maintenance.viewcosts`; sem a permissão, voltam `null`/zerados.

## Dashboard (Fase 3)

- **Manutenção**: Due Today / Due Soon / Overdue (calculados a partir das `MaintenanceSchedule` existentes — veículos nunca atendidos não entram nessa contagem, só aparecem na própria aba do veículo), Em andamento, Aguardando peças, Concluídas no mês, Veículos em manutenção.
- **Alertas**: ordens de serviço com prioridade `Critical` ainda abertas (`maintenance.view`).
