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
         ├─< Vehicle ─┴─< VehicleAssignment (vigência)      ···(futuro)··· Trips, Costs
         │    ├─< OdometerReading
         │    ├─< HourMeterReading
         │    ├─< ChecklistExecution ─< ChecklistAnswer (snapshot) ──> Occurrence
         │    ├─< Occurrence ─< StoredFile (fotos) ──> MaintenanceRequest (manual)
         │    ├─< MaintenanceRequest ──> WorkOrder (aprovação)
         │    ├─< WorkOrder ─< WorkOrderItem | WorkOrderPart | WorkOrderLabor
         │    ├─< Fueling ─< FuelingAnomaly | FuelingCorrection (Fase 4) >── FuelStation, FuelType
         │    ├─< TireInstallation (Fase 5: vigência do pneu na posição) >── Tire ──> TireModel
         │    ├──> TireLayout ─< TireLayoutAxle (configuração de eixos; o Implement também aponta para ela)
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
- **Fase 4**: o combustível do cadastro passou a se chamar `VehicleFuelType` no código (o que o motor aceita); capacidade do tanque principal e do segundo tanque e consumo esperado (km/unidade, opcional) são atributos do veículo. Não se exclui veículo com abastecimentos.

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

---

# Fase 4 — Combustível

## Linguagem

| Código | Interface | Significado |
|---|---|---|
| FuelType | Combustível (tipo) | Produto comprado na bomba (Diesel S10, Gasolina aditivada, GNV…) — catálogo por empresa |
| VehicleFuelType | Combustível do veículo | O que o motor aceita (DieselS10, Flex, Híbrido…) — atributo do cadastro do veículo (era `FuelType` até a Fase 3; ADR-031) |
| FuelStation | Posto de combustível | Onde se abastece; `IsInternal` = tanque próprio da empresa |
| FuelPrice | Preço de referência | Preço praticado/negociado de um combustível num posto a partir de uma data |
| Fueling | Abastecimento | O registro central: veículo, hodômetro, quantidade, preço, total |
| FuelingAnomaly | Alerta do abastecimento | Algo fora do padrão que pede revisão (nunca uma acusação) |
| FuelingCorrection | Correção | Histórico de uma correção: quem, quando, motivo, campo de → para |
| FuelSettings | Limites dos alertas | Tolerâncias por empresa |
| Segment (trecho) | Trecho | Intervalo entre dois tanques cheios — a unidade de medida do consumo |
| Baseline | Consumo esperado | Referência de consumo do veículo para comparar cada trecho |

## Modelo

```
Company ─┬─< FuelType (catálogo, padrão em FuelTypeDefaults)
         ├─< FuelStation ─< FuelPrice >── FuelType
         ├── FuelSettings (0..1 por empresa; ausente = padrões)
         └─< Fueling >── Vehicle, Driver?, FuelStation?, FuelType
                ├─< FuelingAnomaly
                ├─< FuelingCorrection
                ├─< StoredFile (OwnerType = Fueling: cupom, nota, foto)
                └─< OdometerReading (Source = Fueling | Correction, FuelingId) — a quilometragem continua com fonte única
Vehicle: + FuelTankCapacity, SecondaryFuelTankCapacity, ExpectedConsumption (atributos, não registros)
```

Entidades sugeridas na especificação e **não** criadas: `FuelingItem` (um abastecimento é de um único combustível; dois produtos = dois registros), `FuelConsumption` (o consumo é um snapshot no próprio `Fueling` que fecha o trecho — ADR-032), `FuelAnomaly` como entidade solta (é filha do abastecimento: `FuelingAnomaly`).

## FuelType (Combustível) — ADR-031

- Nome e código únicos por empresa (código em maiúsculas: `S10`, `GAS`…), categoria (`Diesel`, `Gasoline`, `Ethanol`, `Cng`, `Electric`, `Other`), unidade (`Liter`, `CubicMeter`, `KilowattHour`), ativo e descrição.
- Toda empresa começa com o catálogo padrão na primeira leitura (Diesel S10, Diesel S500, Gasolina comum, Gasolina aditivada, Etanol, GNV em m³, Recarga elétrica em kWh). Empresas excluem/inativam/criam os seus.
- Um tipo com abastecimentos **não é excluído** (inative) e **não muda de unidade** (mudaria o sentido do km/unidade já calculado). Tipo inativo é recusado em novos abastecimentos.
- **Compatibilidade** (`FuelCompatibility`): Diesel S10/S500 → Diesel; Gasolina → Gasolina; Etanol → Etanol; Flex → Gasolina/Etanol; GNV → GNV/Gasolina/Etanol; Elétrico → Elétrico; Híbrido → Gasolina/Etanol/Elétrico; `Other` (dos dois lados) aceita tudo. Incompatível = alerta `FuelTypeMismatch`, nunca bloqueio.

## FuelStation (Posto) e FuelPrice

- Nome obrigatório; CNPJ opcional (numérico ou alfanumérico, DV válido, **único por empresa** entre postos não excluídos); endereço, telefone e contato opcionais; `IsInternal` (tanque próprio); ativo; observações.
- Não é cadastro de fornecedor. O tanque próprio é só identificado: **controle de estoque fora do escopo**.
- Posto com abastecimentos não é excluído (inative). Posto inativo é recusado em novos abastecimentos (uma correção pode manter o posto antigo).
- **FuelPrice**: (posto, combustível, preço por unidade, vigente a partir de `EffectiveFrom`, até 30 dias à frente). Histórico manual; excluir é soft delete auditado. Gera `FuelPriceChanged` com o preço anterior. **Nunca altera abastecimentos**: cada abastecimento guarda o preço pago.
- Preço vigente em uma data = maior `EffectiveFrom` ≤ data.

## Fueling (Abastecimento) — ADR-032

Campos: veículo, motorista (opcional), posto (opcional), combustível, `FueledAt` (UTC, segundos inteiros) e `FueledOn` (data de negócio no Brasil, usada em filtros e agrupamentos), hodômetro, quantidade (3 casas), preço por unidade (4 casas), **total calculado**, tanque cheio (padrão sim), forma de pagamento, número do cupom, observações, `Source` (`Manual`; integrações futuras acrescentam valores), situação, revisão, cancelamento e o snapshot do consumo.

### Regras de validação (seção 8)

1. Quantidade > 0 (máx. 100.000) e preço > 0 (máx. R$ 1.000/unidade).
2. **Total** = `round(quantidade × preço, 2, half away from zero)` (`FuelingAmounts.Total`). O cliente pode enviar o total do cupom: ele é **conferido** (tolerância de R$ 0,05) e recusado se divergir, mas o valor gravado é sempre o calculado.
3. Data não futura (tolerância de 5 min do relógio). Sem data = agora.
4. **Veículo**: precisa existir na empresa. `Inactive` é recusado; `Available`, `OnTrip`, `UnderMaintenance` e `Unavailable` são aceitos (teste de rodagem, manobra no pátio, veículo aguardando papelada ainda abastece) — mesma linha do histórico de hodômetro (ADR-019). Seção 37.
5. **Motorista**: opcional por padrão; obrigatório se `FuelSettings.RequireDriver`. Se informado, precisa estar `Active` (afastado/desligado recusado). Numa correção, manter o mesmo motorista não é revalidado (é histórico).
6. Combustível e posto precisam existir e estar ativos (exceto manter o mesmo numa correção).
7. Pagamento: `Cash, Pix, DebitCard, CreditCard, FuelCard, Invoice (faturado), InternalTank (tanque próprio), Other` — enum, não catálogo (conjunto estável; nenhuma regra depende dele). Só informação operacional: sem conciliação.

### Integração com o hodômetro (seções 9, 10, 35)

Fonte única: o abastecimento **não** tem quilometragem própria fora do `OdometerReading`.

- **Abastecimento atual** (data ≥ última leitura válida): vira uma leitura `Source = Fueling` com `FuelingId`, pelas regras do `MileageService.AddReadingAsync`: menor que a última → **400** ("peça uma correção"); salto suspeito → leitura `PendingReview` **não aplicada**, o abastecimento ganha o alerta `MileageJump` e o evento `FuelingMileageInconsistencyDetected`.
- Tolerância de 5 minutos: um abastecimento digitado com hora "redonda" logo depois de outra leitura (ex.: cadastro do veículo) não conta como retroativo; a leitura fica no instante da linha de base.
- **Abastecimento lançado depois** (data anterior à última leitura, ex.: cupom digitado dias depois): não gera leitura (o histórico já avançou), mas o km precisa caber **entre as leituras válidas vizinhas** (`MileageService.EnsureFitsHistoryAsync`), senão 400.
- Cancelar um abastecimento rejeita a leitura dele se ainda estiver pendente; uma leitura já válida **fica** (o histórico não é reescrito) e a tela orienta corrigir o hodômetro.

### Situação e transições (seção 13)

| De \ Para | Valid | PendingReview | Cancelled |
|---|---|---|---|
| (criação) | sem alerta | com alerta | — |
| Valid | — | recálculo/correção detecta alerta novo | cancelar |
| PendingReview | revisar (todos os alertas revisados) ou correção remove os alertas | — | cancelar |
| Cancelled | final | final | — |

- A situação é derivada dos alertas (`Fueling.RefreshStatus`): algum alerta sem revisão = `PendingReview`. "Corrigido" e "Suspeito" não são situações: correção é histórico; suspeito = `PendingReview`.
- `PendingReview` **conta nos custos** (o dinheiro foi gasto) e no consumo; `Cancelled` não conta em nada.
- A API devolve `actions { canCorrect, canCancel, canReview }` (workflow + permissão); a UI não decide regra.

### Correção (seção 14)

- Exige `fuel.correct` e motivo. Pelo menos um campo precisa mudar. Veículo não muda (cancele e registre de novo).
- Grava um `FuelingCorrection` (quem, quando, motivo e cada campo **de → para**, já formatado em pt-BR) + auditoria genérica + evento `FuelingCorrected`. Total, alertas e consumo são recalculados (posição antiga e nova).
- **Hodômetro/data** de abastecimento com leitura:
  - leitura **pendente**: a data não muda; a leitura pendente é rejeitada ("corrigido no abastecimento") e o km corrigido passa pelas regras de novo;
  - leitura **válida**: a data não muda; corrigir o km exige também `mileage.manage` e que essa leitura ainda seja a mais recente do veículo — vira uma leitura `Correction` (com `FuelingId`) no mesmo instante, com `MileageCorrected`. Com leituras posteriores: 422, orientando a corrigir pela aba Quilometragem.
  - sem leitura (lançado depois): o km/data novos precisam caber entre as leituras vizinhas.
- Valores monetários da correção aparecem como "—" para quem não vê custos.

### Revisão e cancelamento

- **Revisar** (`fuel.reviewanomalies`, texto obrigatório): marca todos os alertas como revisados (quem/quando) e grava `ReviewedAt/By/Notes`. Se o hodômetro está pendente, a revisão também aprova a leitura (exige `mileage.manage`, senão 403) e o consumo é recalculado. Evento `FuelingReviewed`. **Nunca abre manutenção** (seção 36): se for o caso, o usuário abre uma solicitação.
- **Cancelar** (`fuel.cancel`, motivo obrigatório): final; o trecho seguinte é recalculado sem o abastecimento. Evento `FuelingCancelled`.

## Consumo (seções 17, 18, 56) — ADR-032

**Método tanque cheio a tanque cheio** (`ConsumptionCalculator`):

- Um abastecimento **completo** fecha o trecho aberto pelo completo anterior (não cancelado) do mesmo veículo:
  `distância = km(completo) − km(completo anterior)`; `combustível = soma das quantidades depois do completo anterior até este, inclusive`; `consumo = distância ÷ combustível` (2 casas).
- Resultado gravado no abastecimento que fecha o trecho (`ConsumptionResult`):
  - `PartialFill` — complemento: sem número próprio, entra no próximo completo;
  - `FirstFullTank` — primeiro completo do veículo: só abre o primeiro trecho;
  - `Calculated` — trecho medido;
  - `NotReliable` — distância ≤ 0, unidades diferentes no trecho, hodômetro em revisão (alerta `MileageJump` não revisado) em qualquer ponta/abastecimento do trecho, ou **correção de hodômetro** (feita pela aba Quilometragem) dentro do trecho. Correções feitas pelo próprio abastecimento não invalidam o trecho.
- Unidade principal: **km por unidade** (km/L, km/m³, km/kWh). L/100 km aparece só no detalhe do abastecimento (combustível líquido).
- **Recálculo**: só os trechos tocados por uma mudança — os dois primeiros completos a partir do ponto alterado (criação, correção nas posições antiga e nova, cancelamento, revisão que aprova hodômetro). Trechos antigos não são recalculados por mudança de configuração.
- **Snapshot** (seção 43): distância, quantidade, custo do trecho, consumo, consumo esperado usado, fonte do esperado, desvio % e combustível esperado do trecho (`SegmentExpectedQuantity = distância ÷ esperado`) ficam gravados. Mudar o esperado do veículo ou os limites não altera o passado.
- Média de um período = Σ distância ÷ Σ combustível dos trechos medidos fechados no período (média ponderada, nunca a média das razões). Esperado do período = Σ distância ÷ Σ combustível esperado.
- Casos documentados: abastecimento **não registrado** dentro de um trecho infla o consumo (km/L acima do esperado → alerta `HighConsumption`, que sugere essa causa); **troca de veículo** (transferência) não existe nesta fase; leituras inválidas ficam fora porque só leituras válidas entram no hodômetro.

### Consumo esperado (seção 20, `ConsumptionBaseline`)

Precedência: `Vehicle.ExpectedConsumption` (configurado) > média do próprio veículo (últimos 10 trechos medidos, mínimo 3, mesma unidade) > média dos veículos do mesmo `VehicleType` e unidade nos últimos 180 dias (mínimo 5 trechos). Sem dados suficientes, não há esperado nem alerta de consumo — "dados insuficientes" nunca vira alerta.

## Alertas (seções 11, 12, 21, 33) — `FuelAnomalyRules`

| Tipo | Regra | Limite padrão (`FuelSettings`) |
|---|---|---|
| `ExcessiveQuantity` | quantidade > (tanque principal + segundo tanque) × (1 + tolerância). Sem capacidade cadastrada, não avalia | 5% |
| `AbnormalPrice` | \|preço − referência\| / referência > limite. Referência: preço de referência vigente do posto; senão média ponderada da empresa para o mesmo combustível nos últimos 30 dias (mínimo 3 abastecimentos) | 20% |
| `MileageJump` | a leitura de hodômetro do abastecimento ficou em revisão (regra do ADR-019) | 1.500 km/dia (OdometerPolicy) |
| `HighFrequency` | outro abastecimento não cancelado do veículo a menos de N horas (antes ou depois) | 2 h (0 desliga) |
| `FuelTypeMismatch` | combustível incompatível com o motor do veículo | — |
| `LowConsumption` | consumo do trecho abaixo do esperado além do limite | 20% |
| `HighConsumption` | consumo do trecho acima do esperado além do limite (possível abastecimento não registrado/hodômetro errado) | 20% |

- Textos neutros e factuais ("Revisão recomendada", "Confira o valor digitado"); nunca "fraude", "defeito" ou ranking de motorista. Mensagens de preço **não contêm valores em R$** (são exibidas a quem pode não ver custos); valores esperado/real de preço voltam nulos sem permissão de custo.
- Os alertas são reavaliados a cada correção: tipos que deixaram de ocorrer somem, tipos que continuam mantêm a revisão já feita, tipos novos voltam a exigir revisão.
- Limites: 0–200% (variação de preço/consumo ≥ 1%), intervalo 0–48 h. Valem para as próximas avaliações.

## Custos (seção 24)

- Custo total = Σ `TotalAmount` dos abastecimentos não cancelados do período (inclui `PendingReview`).
- Preço médio/L = Σ total ÷ Σ litros (só combustíveis em litros).
- Custo/km = Σ custo dos trechos medidos ÷ Σ distância desses trechos.
- Por veículo, motorista ("Sem motorista informado"), posto ("Sem posto informado") e combustível. Valores **operacionais**, não contábeis.

## Eventos (seção 34) — ADR-025

`FuelingRecorded`, `FuelingCorrected`, `FuelingCancelled`, `FuelingMarkedForReview`, `FuelingReviewed`, `FuelConsumptionAnomalyDetected`, `FuelingMileageInconsistencyDetected`, `FuelPriceChanged`. Os de abastecimento entram na linha do tempo do veículo (e do motorista, quando informado). **Os resumos não contêm valores em R$** — a linha do tempo é visível a quem vê o veículo. Nenhum canal (e-mail, WhatsApp, push) é acionado: o outbox (`PublishedAt`) fica pronto para a Fase 9.

## Dashboard e relatórios

- **Painel de Combustível** (`/combustivel`): custo total, litros, preço médio/L, consumo médio, custo/km, nº de abastecimentos (período escolhido); "o que requer atenção" (fila de revisão, todos os períodos); gasto mensal e consumo mensal (12 meses); por combustível; veículos com maior custo; veículos com consumo abaixo do esperado; últimos abastecimentos.
- **Dashboard principal**: alerta `FuelingPendingReview` para quem tem `fuel.reviewanomalies`.
- **Relatórios** (`/combustivel/relatorios`): abastecimentos; consumo (veículo, trechos, distância, combustível, consumo, esperado, variação); custos (por veículo/motorista/posto/combustível, com custo/km por veículo); postos (por posto × combustível: abastecimentos, quantidade, preço médio/mín./máx., custo); preços (combustível × mês). Período obrigatório de até 2 anos (padrão 30 dias), ordenação e paginação. **Sem exportação** (o sistema ainda não tem exportação; Fase 8).
- **Aba Combustível do veículo**: consumo médio × esperado, custo/km, custo e volume do período, histórico por dia (30 dias), semana (120 dias) ou mês (12 meses) com períodos vazios sem número inventado, últimos abastecimentos.

## Catálogo de permissões (Fase 4)

| Permissão | Significado |
|---|---|
| `fuel.view` | abastecimentos, consumo, postos, combustíveis, painel e relatórios **sem valores em R$** |
| `fuel.create` | registrar abastecimentos (e anexar comprovantes nos próprios) |
| `fuel.correct` | corrigir abastecimentos (km já aplicado exige também `mileage.manage`) |
| `fuel.cancel` | cancelar abastecimentos |
| `fuel.reviewanomalies` | revisar abastecimentos com alerta (hodômetro pendente exige também `mileage.manage`) |
| `fuel.managestations` | postos e preços de referência |
| `fuel.configure` | catálogo de combustíveis e limites dos alertas |
| `fuel.viewcosts` | preços, totais, custo/km, relatórios de custos e preços. **Exceção**: o autor de um abastecimento sempre vê os valores do que registrou |

`Fuel.Edit` e `Fuel.ViewReports` da especificação foram absorvidos: editar = corrigir (não existe edição livre), e relatórios seguem `fuel.view` + `fuel.viewcosts` para os valores (mesmo molde de `maintenance.viewcosts`).

| Papel | Permissões de combustível |
|---|---|
| Administrador / Administrador da plataforma | todas |
| Gestor de frota | todas |
| Operações | `fuel.view`, `fuel.create` (registra e vê o que registrou; não vê o gasto da frota) |
| Manutenção | `fuel.view` (consumo é sinal de manutenção) |
| Financeiro | `fuel.view`, `fuel.viewcosts` |
| Visualizador | `fuel.view` |
| Motorista | nenhuma (decisão da Fase 2 mantida). Quando existir o acesso do motorista, `fuel.create` sem `fuel.viewcosts` já atende a seção 41 |

## Cartão combustível (preparação, seção 16)

Não implementado. A forma de pagamento `FuelCard` e o `Source` do abastecimento já existem; um futuro `FuelCard` (número mascarado, fornecedor, situação, limite, vínculo com veículo/motorista) entra como entidade própria e uma FK nula `Fueling.FuelCardId` — migration aditiva, sem reestruturar o abastecimento. Importação de transações de fornecedor acrescenta `FuelingSource.Integration` + um identificador externo único.

---

# Fase 5 — Pneus

## Linguagem

| Código | Interface | Significado |
|---|---|---|
| Tire | Pneu | O pneu físico, com identidade própria. Passa por vários veículos durante a vida |
| Code (número de fogo) | Número de fogo | Identificador interno marcado na lateral (`PN-000123` gerado, ou o número da empresa). Único por empresa |
| TireModel | Modelo de pneu | Marca + modelo + medida + especificações (aplicação, construção, carga, velocidade, sulco original) |
| TireLayout / TireLayoutAxle | Configuração de eixos / Eixo | Quantos eixos, simples ou duplos, obrigatórios ou não, medida exigida e pressão de referência; estepes |
| TirePosition | Posição | Gerada a partir dos eixos (`1E`, `2EE`, `EST1`…). Não é tabela |
| TireInstallation | Instalação (vigência) | Período de um pneu numa posição de um veículo/implemento: instalação e remoção são as duas pontas do mesmo registro |
| TireRotation | Rodízio | Cabeçalho de uma troca de posições atômica |
| TireInspection | Inspeção / medição | Sulco, pressão, condição, desgaste, danos e fotos num momento. Também guarda a medição da remoção e o sulco da banda nova |
| TireServiceOrder | Conserto / Recapagem | Envio a um fornecedor e o resultado (aprovado/reprovado) |
| TireCost | Custo do pneu | Conserto, recapagem, montagem e outros (a compra fica no pneu) |
| TireAnomaly | "Requer revisão" | Sinal calculado (perda rápida de sulco, consertos/furos repetidos, vida curta, danos repetidos na posição) |
| TireSettings | Limites de pneus | Política da empresa (não é exigência legal) |

## Modelo (ADR-035)

```
Company ─┬─< TireModel ─< Tire ─┬─< TireInstallation >── Vehicle | Implement (exatamente um)
         │                       ├─< TireInspection ─< TireInspectionDamage ; ─< StoredFile (fotos)
         │                       ├─< TireServiceOrder >── Workshop? ; ─< StoredFile
         │                       ├─< TireCost (manual ou gerado pela conclusão do serviço)
         │                       ├─< TireAnomaly
         │                       ├─< StoredFile (nota, garantia, documento da baixa)
         │                       └─< OperationalEvent (TireId) — a linha do tempo do pneu
         ├─< TireLayout ─< TireLayoutAxle   ← Vehicle.TireLayoutId, Implement.TireLayoutId
         ├─< TireRotation ─< (TireInstallation.RotationId / RemovalRotationId)
         └── TireSettings (0..1)
```

Entidades sugeridas na especificação e **não** criadas: `TireBrand` e `TireSpecification` (atributos do `TireModel`: ninguém mais as referencia; a lista de marcas do filtro é `DISTINCT Brand`), `TirePosition` (gerada a partir dos eixos — `TirePositions.For`), `TireRemoval` (é a outra ponta do `TireInstallation`), `TireRepair`/`TireRetread` separados (um único `TireServiceOrder` com `Kind`: mesmo ciclo enviado → concluído/cancelado), `TireLifecycleEvent` (reutiliza `OperationalEvent` com a nova coluna `TireId` — ADR-025).

## Tire (Pneu)

- Número de fogo único por empresa; em branco, o servidor gera `PN-{sequência:D6}` (pula números já usados manualmente). Série, DOT (normalizado; os quatro últimos dígitos dão semana/ano — `TireDot`; data de fabricação = segunda-feira da semana ISO; sem código válido, informada à mão), compra (data ≤ hoje e ≥ fabricação, valor, fornecedor em texto), sulco original (padrão do modelo), local de armazenamento (texto livre, só fora do veículo) e observações.
- Pneu usado cadastrado (migração da frota): sulco atual (vira uma medição `Registration`) e recapagens anteriores (0–10).
- **Campos de leitura rápida** mantidos só pelos serviços de pneu: km acumulado (das instalações encerradas), `HasUnmeasuredDistance`, recapagens, consertos, sulco atual e data, última inspeção, último desgaste, dano na última inspeção, última checagem de pressão, referência da inspeção (`InspectionReferenceAt`) e `LastMovementAt`.
- O modelo só muda com o pneu fora do veículo; a medida do modelo não muda depois de haver pneus dele.
- Excluir só o pneu sem nenhum histórico (cadastro por engano). Com histórico: baixa.

## Situação e transições (ADR-036 — `TireWorkflow`)

| De \ operação | Instalar | Remover (destino) | Rodízio / transferir | Enviar a serviço | Concluir serviço | Avaliação / liberar | Baixa |
|---|---|---|---|---|---|---|---|
| **InStock** (Em estoque) | → Installed | — | — | → UnderRepair / UnderRetread | — | → UnderInspection | → Disposed |
| **Installed** | — | → InStock, UnderInspection, UnderRepair, UnderRetread ou Disposed | continua Installed | só conserto no veículo (continua Installed) | — | — | só pela remoção com destino Baixa |
| **UnderInspection** (Em avaliação) | — | — | — | → UnderRepair / UnderRetread | — | liberar → InStock | → Disposed |
| **UnderRepair / UnderRetread** | — | — | — | — | aprovado → InStock; reprovado ou cancelado → UnderInspection | — | — |
| **Disposed** (Baixado) | final | final | final | final | final | final | final |

- Vendido, extraviado, transferido para outra empresa e descartado são **motivos da baixa** (`TireDisposalReason`), não situações: um estado final mantém as transições simples.
- "Reservado" não existe nesta fase (não há fluxo de reserva). "Recapado" não é situação: é o número da vida (`RetreadCount`) de um pneu em estoque.
- A API devolve `actions` (permissão + workflow); a tela não decide regra.

## Posições e configuração de eixos (ADR-037 — seções 8–10)

- `TireLayout`: nome único por empresa, alvo (`Vehicle`/`Implement`), descrição, estepes (0–2), ativa, 1–10 eixos. Cada eixo: tipo (`Steer` direcional, `Drive` tração, `Free` livre, `Trailer` implemento), rodado duplo, obrigatório, **medida exigida** (opcional) e **pressão de referência em psi** (opcional).
- Posições geradas (`TirePositions.For`), do eixo 1 (frente) para trás, vistas de cima: eixo simples `nE`, `nD`; duplo `nEE`, `nEI`, `nDI`, `nDE` (esquerdo externo/interno, direito interno/externo); estepes `EST1`, `EST2` (não obrigatórias, sem medida).
- Veículo e implemento apontam para uma configuração (`TireLayoutId`), escolhida na aba Pneus por quem tem `tires.managesettings`. Trocar exige que toda posição ocupada exista na nova; retirar exige nenhum pneu instalado. Editar uma configuração não pode remover posição ocupada em nenhum cadastro que a usa; o alvo não muda com cadastros usando.
- A instalação guarda o **código da posição + rótulo + eixo + estepe** (snapshot): o histórico continua legível mesmo que a configuração mude depois.
- Padrões criados na primeira leitura (`TireLayoutDefaults`): carro/picape, caminhão toco 4x2, truck 6x2, cavalo mecânico 6x2 (eixo livre opcional) e 6x4, semirreboques de 2 e 3 eixos.

## Compatibilidade (seção 33 — `TireCompatibility`)

| Situação | Resultado |
|---|---|
| A posição tem medida exigida e o pneu é de outra | **Incompatível**: instalação/substituição/transferência/rodízio recusados |
| Aplicação do pneu não indicada para o eixo (tração em direcional, reboque em tração) | Aviso (instala) |
| O par da roda dupla tem outra medida | Aviso (instala) |
| A posição não tem medida configurada | "A compatibilidade não pôde ser verificada automaticamente" |
| Estepe | Compatível |

Carga e velocidade são informativas: nenhuma posição registra exigência delas, então nada é afirmado. A tela consulta `GET /tires/{id}/compatibility` antes de salvar.

## Instalação, remoção, substituição, transferência e rodízio (seções 11–15, 32)

- **Instalar**: pneu `InStock`; veículo/implemento existente e não inativo, com configuração; posição existente e **livre** (ocupada → 409 orientando "Substituir"); compatível. Data: em branco = agora; pode ser passada, nunca futura (5 min de tolerância), nunca anterior à última movimentação do pneu nem à última saída daquela posição.
- **Remover**: motivo (`Rotation`, `Replacement`, `Repair`, `Retread`, `Inspection`, `VehicleSale`, `VehicleDecommission`, `Damage`, `EndOfLife`, `Transfer`, `Other`) e destino (estoque, avaliação, conserto, recapagem, baixa). Medição opcional (vira uma inspeção `Removal`). Conserto/recapagem abrem o `TireServiceOrder` na mesma transação; baixa exige motivo da baixa. Destino conserto/recapagem/baixa exige também `tires.repair`/`tires.retread`/`tires.dispose`. Remoção por dano verifica "danos repetidos na mesma posição".
- **Substituir** = remoção do atual + instalação do novo na mesma posição, numa transação (dois `SaveChanges`: a posição é liberada antes de ser ocupada — índice único filtrado). Qualquer falha (ex.: medida) desfaz tudo.
- **Transferir** = sair de um veículo/implemento e entrar em outro, numa transação; hodômetros de origem e destino opcionais.
- **Rodízio** (atômico): validado contra o **mapa final** — cada pneu uma vez, nenhuma posição repetida, destino livre ou liberado no mesmo rodízio, compatibilidade de medida e do par duplo pelo mapa final. Fecha todas as vigências envolvidas (`RemovalRotationId`), salva, abre as novas (`RotationId`), salva — tudo em uma transação. Evento por pneu (linha do tempo do pneu) + um evento do rodízio (linha do tempo do veículo). Imutável depois; a única correção é a de km (abaixo).
- **Correção controlada** (`tires.edit`, motivo obrigatório): só os km de uma vigência de veículo (instalação; remoção se encerrada). O km acumulado do pneu acompanha; evento `TireHistoryCorrected` (de → para) + auditoria. Datas e posições não mudam.

## Quilometragem (seção 38 — `TireMileage`)

- **Fonte única: o histórico de hodômetro do veículo.** Operação atual: km = linha de base do `MileageService`; se a pessoa informa um km maior, ele vira uma leitura `Source = TireService` pelas regras do ADR-019; um salto suspeito **recusa a operação** (a vigência não pode começar num km que ninguém confirmou — registre a leitura pela aba Quilometragem). Operação lançada depois: km = última leitura válida até aquela data (`MileageService.OdometerAtAsync`) ou o km informado, que precisa caber entre as leituras vizinhas.
- Km da vigência = remoção − instalação (nunca negativo); **estepe = 0**; implemento (sem hodômetro) = desconhecido → `HasUnmeasuredDistance`. Km atual = acumulado + vigência aberta (`Vehicle.CurrentOdometerKm − instalação`).

## Inspeção, sulco e pressão (seções 16–21)

- Inspeção: data (pode ser passada, não futura), hodômetro opcional (mesma regra acima), sulco (0–40 mm, uma casa), pressão + unidade (psi/bar/kPa), condição (`Good` Bom, `Attention` Atenção, `Unfit` Impróprio), desgaste observado (`Normal`, `CenterWear`, `ShoulderWear`, `OneSidedWear`, `IrregularWear`, `Cupping`, `Unknown`), danos (`Cut`, `Crack`, `Bulge`, `Puncture`, `SidewallDamage`, `BeadDamage`, `Other` — "Outro" exige descrição), fotos, ocorrência de origem (opcional).
- **Nada é sobrescrito**: cada medição é uma linha; os campos rápidos do pneu seguem só a medição mais recente (uma inspeção lançada depois de outra mais nova não muda o "sulco atual").
- Pressão comparada com a referência do eixo da posição, convertida para psi, com a tolerância da empresa (`TirePressure.Check`): `WithinRange`, `Low`, `High` ou `NotEvaluated` (sem referência — nunca adivinhado).
- Desgaste e dano são **observações**: o sistema não diagnostica causa.

## Conserto e recapagem (seções 22, 23)

- `TireServiceOrder` (`Kind` = `Repair` | `Retread`): `Open` → `Completed` (resultado `Approved`/`Rejected`) | `Cancelled`. Um pneu tem no máximo um serviço aberto (índice único filtrado). Fornecedor: oficina do cadastro da Fase 3 **ou** nome livre.
- Enviar: pneu em estoque ou em avaliação (ou pela remoção). Recapagem registra o número (`RetreadCount + 1`).
- **Conserto no veículo** (`InPlace`): pneu instalado, só conserto, registrado já concluído — o pneu não sai da posição.
- Concluir aprovado → estoque; conserto soma `RepairCount`; recapagem soma `RetreadCount` e exige o **sulco da banda nova**, que vira medição `Retread` e o sulco atual. Aprovado limpa os sinais de dano/desgaste da última inspeção. Reprovado (motivo obrigatório) ou cancelado → avaliação. **Quem decide se a carcaça aceita recapagem é o fornecedor/gestor**; o sistema registra a decisão.

## Custos e custo/km (seções 24–26 — `TireCostPolicy`)

- Ciclo de vida = valor de compra (no pneu) + Σ `TireCost` (conserto e recapagem gerados ao concluir o serviço com valor; montagem e outros manuais). Custos de serviço não são excluídos à mão; os manuais sim (soft delete auditado).
- **Custo/km = total ÷ km**, só com **≥ 5.000 km**, total > 0 e **sem trecho não medido** (implemento). Fora disso, nulo com a explicação (`CostPerKmNote`).
- `tires.viewcosts` controla todo valor; digitar valor (compra, serviço, custo manual) também a exige; custo manual exige ainda `tires.edit`.

## Alertas calculados (seção 34 — `TireAlertPolicy`)

| Alerta | Regra (limites de `TireSettings`, padrão) | Gravidade |
|---|---|---|
| `TreadBelowMinimum` | sulco atual ≤ mínimo (3 mm) | crítico |
| `TreadNearMinimum` | sulco atual ≤ aviso (4 mm) | aviso |
| `InspectionOverdue` | instalado e `InspectionReferenceAt` (última inspeção ou instalação a partir do estoque) há mais que o intervalo (30 dias; 0 desliga) | aviso |
| `UnevenWear` | último desgaste diferente de normal/não avaliado | aviso |
| `DamageReported` | dano na última inspeção | crítico |
| `AgeExceeded` | fabricado há mais que a idade (5 anos; 0 desliga) | aviso |
| `PressureOutOfRange` | última pressão baixa/alta | aviso |

Nunca gravados (como o vencimento de documentos); a forma SQL de cada um está em `TireService.WhereAlert` e é a mesma comparação. Textos dizem "configurado pela empresa" — nunca "legal".

## "Requer revisão" (seção 35 — `TireAnomalyRules`)

| Tipo | Regra |
|---|---|
| `RapidTreadLoss` | perda de sulco entre duas medições > referência (0,5 mm/1.000 km; 0 desliga), com ≥ 1.000 km entre elas e sem recapagem no meio |
| `RepeatedRepairs` | ≥ 3 consertos concluídos em 365 dias |
| `RepeatedPunctures` | ≥ 2 furos em 180 dias (inspeção + conserto do mesmo furo contam uma vez) |
| `ShortLifecycle` | baixa com km abaixo da vida mínima esperada (0 = desligado, padrão) |
| `RecurringPositionDamage` | ≥ 2 remoções por dano na mesma posição do mesmo veículo em 180 dias |

Uma anomalia aberta por tipo e pneu (não acumula). Texto sempre "Requer revisão", nunca uma causa. Revisar exige `tires.edit` e texto.

## Integração com manutenção, checklist e ocorrências (seções 36, 37, 62)

- Inspeção "Impróprio para uso" de pneu em **veículo** abre `MaintenanceRequest` (`Source = AutomaticAlert`, prioridade alta) **só se** `TireSettings.AutoMaintenanceRequestOnUnfit` (padrão desligado) — pelo `MaintenanceRequestService.AddAutomatic`, na mesma transação. Nunca ordem de serviço.
- Checklist: o item de pneu reprovado já vira ocorrência `TireProblem` (Fase 2); a ocorrência ganhou o atalho "Inspecionar pneus do veículo" (aba Pneus) e a inspeção aceita `OccurrenceId`.

## Eventos (seção 61 — ADR-025)

`TireRegistered`, `TireInstalled`, `TireRemoved`, `TireRotated`, `TireInspected`, `TireInspectionFailed`, `TireTreadLow`, `TirePressureLow`, `TireRepairStarted`, `TireRepairCompleted`, `TireRetreadStarted`, `TireRetreadCompleted`, `TireServiceCancelled`, `TireReturnedToStock`, `TireSentToEvaluation`, `TireEndOfLife`, `TireAnomalyDetected`, `TireCostRecorded`, `TireHistoryCorrected`, `TireLayoutChanged`. Todos levam `TireId` (linha do tempo do pneu); os que acontecem num veículo/implemento levam também o `VehicleId`/`ImplementId`. `OccurredAt` = quando a operação aconteceu (operação lançada depois fica no lugar certo da linha do tempo). Resumos sem R$. Nenhum canal de notificação acionado.

## Catálogo de permissões (Fase 5)

| Permissão | Significado |
|---|---|
| `tires.view` | pneus, histórico, diagrama, painel e relatórios (sem R$) |
| `tires.create` | cadastrar pneus (e criar modelo durante o cadastro) |
| `tires.edit` | editar cadastro, corrigir km do histórico, custos manuais (com `viewcosts`), revisar "requer revisão" |
| `tires.install` | instalar, substituir (com `tires.remove`), transferir (com `tires.remove`) |
| `tires.remove` | remover (destino conserto/recapagem/baixa exige a permissão correspondente) |
| `tires.rotate` | rodízio |
| `tires.inspect` | inspecionar, separar para avaliação, liberar para uso |
| `tires.repair` / `tires.retread` | consertos / recapagens |
| `tires.dispose` | baixa |
| `tires.viewcosts` | valores (compra, serviços, ciclo de vida, custo/km, relatório de custos) |
| `tires.managesettings` | configurações de eixos (e atribuí-las), catálogo de modelos, limites |

`Tires.ViewReports` foi absorvida (como no ADR-034): relatórios seguem `tires.view` + `tires.viewcosts`.

| Papel | Pneus |
|---|---|
| Administrador / plataforma / Gestor de frota | todas |
| Manutenção (borracharia) | todas, inclusive custos |
| Operações | `tires.view`, `tires.inspect` (inspeção de campo e dano; não move pneus) |
| Financeiro | `tires.view`, `tires.viewcosts` |
| Visualizador | `tires.view` |
| Motorista | nenhuma (decisão da Fase 2) |

## Preparação para fases futuras (Fase 5)

- **Inventário/almoxarifado**: `StorageLocation` é texto; um módulo de estoque substitui por FK de local + movimentações sem mudar o ciclo do pneu. Compra/fornecedor ficam em texto até existir o cadastro de fornecedores.
- **TPMS/telemetria**: uma leitura automática de pressão entra como nova `TireInspectionSource` (ex.: `Sensor`) — a comparação `TirePressure.Check` já é única.
- **Venda/transferência entre empresas**: a baixa com motivo `Sold`/`Transferred` preserva o histórico; um fluxo de venda futuro referencia o pneu baixado.

# Financeiro (Fase 6) — ADR-040

## Modelo

`CostCenter` e `ExpenseCategory` são catálogos configuráveis por empresa, hierárquicos (FK para si mesmos). Três `ExpenseCategory` são de sistema (`IsSystemCategory=true`, `CostAggregationKey` = `Fuel`/`Maintenance`/`Tires`): alimentadas automaticamente pelos módulos de origem, nunca por um lançamento manual. Não existe uma entidade genérica de Fornecedor nem de Filial — um fornecedor é a `Workshop` da Fase 3 (opcional) ou texto livre, e uma "filial" é só mais um `CostCenter` (o exemplo da especificação, "Filial São Paulo", é um `CostCenter` folha).

`Expense` é o lançamento manual/recorrente — nunca o custo de combustível, manutenção ou pneus, que têm módulo próprio. `RecurringExpense` é o modelo de uma obrigação periódica (seguro, financiamento, leasing…); o `RecurringExpenseGenerationScanner` (job em background, mesmo formato do `DocumentExpirationScanner`) transforma isso em linhas de `Expense` com até 30 dias de antecedência do vencimento, de forma idempotente (índice único `RecurringExpenseId + DueDate`). `Budget` é um valor planejado por período (ano ou ano+mês) × categoria × (opcionalmente) centro de custo/veículo — o realizado nunca é gravado, é sempre calculado.

## Situação de pagamento (`PaymentStatus`) — `ExpensePaymentPolicy`

Nunca gravada. Calculada a partir de `CancelledAt`, `Amount`, `PaidAmount` e `DueDate`:

```
Cancelled   se CancelledAt preenchido (estado final)
Paid        se PaidAmount >= Amount
PartiallyPaid se 0 < PaidAmount < Amount
Overdue     se DueDate < hoje e não totalmente pago
Scheduled   se tem DueDate no futuro e nada pago
Pending     se não tem DueDate e nada pago
```

Mesma ideia do `DocumentExpiryPolicy`/`MaintenanceSchedulePolicy`: um campo calculado nunca fica desatualizado. Uma despesa não é excluída pelo usuário — é **cancelada** (motivo obrigatório), e o cancelamento é definitivo.

## Integração com combustível, manutenção e pneus (`CostAggregationService`) — seção 6 do pedido

O custo de um veículo/da frota **nunca duplica** o dado de origem. `CostAggregationService` lê e soma direto:

- Combustível: `Fuelings.TotalAmount` (exclui `Cancelled`), por `FueledOn`.
- Manutenção: `WorkOrders.TotalCost` (exclui `Cancelled`/`Rejected`), por `OpenedAt`.
- Pneus: `TireCosts.Amount`, por `IncurredOn` — atribuído ao veículo que tinha aquele pneu instalado naquela data (via `TireInstallations`), não ao veículo atual do pneu.
- Outras: soma de `Expenses` não canceladas.

Cada fatia só entra na soma se o usuário tiver a permissão de custo **daquele módulo** (`fuel.viewcosts`/`maintenance.viewcosts`/`tires.viewcosts`) **e** `finance.viewcosts`. Faltando uma, a fatia some e a resposta carrega `IsPartial = true` — a tela avisa "totais parciais" em vez de mostrar um número incompleto como se fosse o total real.

## Custo por quilômetro — `VehicleCostPolicy`

`CostPerKm = custo total ÷ km rodados no período`, usando o histórico de hodômetro (`MileageService.OdometerAtAsync`) já existente — nenhum módulo novo de quilometragem. Abaixo de `VehicleCostPolicy.MinKmForCostPerKm` (50 km) ou sem leitura de hodômetro cobrindo o período inteiro, o resultado é `null` com motivo explícito, nunca um número — mesmo espírito do `TireCostPolicy` (que usa 5.000 km, porque mede o ciclo de vida inteiro de um pneu, não um mês de um veículo).

## TCO — `FinanceAnalyticsService.GetVehicleTcoAsync`

`TCO = AcquisitionValue (já existe em Vehicle, Fase 1) + custo operacional acumulado desde AcquisitionDate` (ou desde o cadastro, se a data de aquisição não foi informada). `CostPerMonth = TCO ÷ meses desde o início`; `CostPerKm` segue a mesma regra de dados insuficientes acima. É uma análise de gestão operacional (seção 16 do pedido) — não um cálculo contábil/fiscal de depreciação.

## Orçamento x Realizado — `BudgetAnalysis`

`Remaining = Budget − Actual`; `UtilizationPercent = Actual ÷ Budget × 100` (null sem orçamento, para não mostrar um percentual sem sentido); `Status`: `UnderBudget` (< 90%), `NearBudget` (90–100%), `OverBudget` (> 100%), `NoBudget`. Um orçamento de veículo lê a fatia da categoria no `VehicleCostBreakdown`; um orçamento de categoria/empresa lê o `GetFleetCostByCategoryAsync`.

## Anomalias e alertas (seção 20 do pedido)

Sem um subsistema novo de anomalia: reaproveita os dois padrões já existentes no projeto. (1) **Alerta do painel**: o painel principal ganhou "Despesa em atraso" (`DashboardAlert`, calculado a cada leitura, igual aos alertas de documento/CNH/pneu), e `BudgetExceeded` é emitido como evento operacional quando uma despesa recém-criada ultrapassa o orçamento da sua categoria/período. (2) **Sinal calculado na leitura**: `ExpenseResponse.IsDuplicateSuspect` (mesmo veículo/categoria/valor/data de outra despesa não cancelada) é calculado a cada listagem, nunca gravado — o texto é sempre factual ("possível duplicidade"), nunca acusatório.

## Eventos (ADR-025)

`ExpenseCreated`, `ExpenseEdited`, `ExpenseCancelled`, `ExpensePaymentRegistered`, `RecurringExpenseGenerated`, `BudgetExceeded`. Nenhum carrega valor em R$ no resumo (mesma regra do combustível/manutenção/pneus): o histórico do veículo é visível a quem não tem `finance.viewcosts`.

## Catálogo de permissões (Fase 6)

| Permissão | Significado |
|---|---|
| `finance.view` | despesas, categorias, centros de custo, orçamentos e recorrentes (sem R$) |
| `finance.viewcosts` | todo valor em R$ do módulo: despesas, painel, relatórios, custo/km, TCO — e a fatia financeira de um total combinado com outros módulos |
| `finance.create` | registrar despesas |
| `finance.edit` | editar despesas |
| `finance.cancel` | cancelar despesas |
| `finance.registerpayment` | registrar pagamento (total ou parcial) |
| `finance.managecategories` | configurar categorias de despesa |
| `finance.managecostcenters` | configurar centros de custo |
| `finance.managebudgets` | configurar orçamentos |
| `finance.managerecurring` | configurar despesas recorrentes |

| Papel | Financeiro |
|---|---|
| Administrador / plataforma / Gestor de frota | todas |
| Financeiro | todas |
| Visualizador | `finance.view` |
| Operações / Manutenção / Motorista | nenhuma |

## Preparação para fases futuras (Fase 6)

- **Exportação** dos relatórios (Excel/PDF): Fase 8, como as demais.
- **Multas e sinistros**: o roadmap deixa em aberto se entram aqui ou na Fase 2 (controle operacional) — por ora, uma multa é só mais uma categoria de despesa manual.
- **Rateio/depreciação contábil**: fora do escopo desta fase (seção 35 do pedido); o TCO é uma análise operacional, não substitui um módulo contábil.
- **Fornecedor genérico**: se o negócio precisar de um cadastro de fornecedores além da oficina (`Workshop`), ele entra como entidade própria referenciada por `Expense`/`RecurringExpense`, sem mudar o modelo atual (`SupplierName` continua como texto livre de fallback).
- **Implementos com km**: com o engate (Fase 2.5), a km da vigência em implemento pode vir do veículo trator — hoje é desconhecida.

# Alertas e automação (fase final, etapa A) — ADR-045

## Regra de automação (`AutomationRule`)
QUANDO (gatilho + condição) → ENTÃO (criar alerta e/ou avisar). Gatilhos e o que observam:

| Gatilho | Condição | Público (quem vê) | Observação |
|---|---|---|---|
| `MaintenanceOverdue` | item do plano em `Overdue` (`MaintenanceSchedulePolicy`, com carência) | manutenção | crítico se o item é de prioridade crítica |
| `MaintenanceDueSoon` | item em `DueSoon` ou `Due` | manutenção | texto diz quanto falta (km/dias/horas) |
| `FuelConsumptionAbnormal` | consumo do período ≥ X% pior que a média dos 180 dias anteriores do próprio veículo | combustível | mín. 2 trechos no período e 3 na referência; média ponderada (km ÷ litros) |
| `VehicleCostAboveAverage` | custo do período ≥ X% acima da média dos outros veículos ativos do mesmo tipo | custos da frota (todas as `*.viewcosts`) | mín. 3 pares com custo; aponta a fatia que mais puxou |
| `BudgetThreshold` | utilização do orçamento do mês/ano ≥ X% | custos da frota | > 100% = crítico |
| `TireTreadLow` | pneu instalado com sulco ≤ aviso da empresa | pneus | no mínimo = crítico |
| `ExpenseOverdue` | despesa não paga com vencimento há ≥ X dias | `finance.view` (sem R$) | |
| `DocumentExpiring` | documento vencido/vencendo (`WhereAlert`) | documentos (+ motoristas se o dono é motorista) | vencido = crítico |
| `OperationalEvent` | um fato do outbox (`NotifiableEvents`) | o do módulo do evento | só eventos posteriores à regra |

Limites, faixas e textos ficam em `AutomationTriggerCatalog`. Regras padrão: uma por gatilho agendado + "Checklist reprovado" (só avisa).

## Alerta (`FleetAlert`)
Título, explicação (o que e por quê), **base** (os números) e sugestão; gravidade (Crítico/Atenção/Informativo); categoria; público; registro de destino (`EntityType/EntityId`, `VehicleId`, aba).

Situação (`FleetAlertWorkflow`): `Novo → Lido → Em andamento → Resolvido | Descartado` (Novo pode ir direto para qualquer uma; finais não reabrem). Descartar exige motivo. Abrir o alerta marca como lido. "Assumir" = Em andamento + responsável.

Ciclo automático por chave de deduplicação: condição continua → alerta atualizado; condição some → **Resolvido automaticamente**; volta depois de resolvido → alerta novo com recorrência; descartado → silêncio por 30 dias.

Prioridade (`AlertPriority`): gravidade (100/50/10) + impacto (0–30) + urgência (0–30) + 10 por recorrência (até 30).

## Notificação (`UserNotification`)
Caixa pessoal (o sino). Destinatários: usuários ativos da empresa que podem ver o alerta (público + `alerts.view`), todos ou um escolhido. Mais de 3 alertas novos de uma regra numa execução → uma notificação-resumo. Nunca contém R$.

## "Requer atenção" (`AttentionService`)
Grupos de alertas abertos por gatilho + filas ao vivo: abastecimentos para revisão (`fuel.reviewanomalies`), leituras de hodômetro suspeitas (`mileage.manage`), solicitações de manutenção abertas (`maintenance.manageworkorders`), ocorrências críticas abertas (`occurrences.view`), motoristas ativos com CNH vencida (`drivers.view`). Ordenado por gravidade e quantidade; zeros omitidos.

## Catálogo de permissões (fase final)
| Permissão | Libera |
|---|---|
| `alerts.view` | central de alertas, "Requer atenção" (grupos de alertas) e o sino — cada alerta ainda filtrado pelo público |
| `alerts.manage` | assumir, resolver e descartar alertas |
| `automation.manage` | configurar regras e rodar a verificação na hora |

Papéis: Administrador/Plataforma (todas); Gestor de frota (as três); Operações, Manutenção e Financeiro (`alerts.view` + `alerts.manage`); Visualizador (`alerts.view`); Motorista (nenhuma).

# Análises cruzadas (fase final, etapa B) — ADR-047

## Métricas por veículo (`VehicleMetricsService`)
| Métrica | Fonte | Visível com |
|---|---|---|
| Km rodados | hodômetro: maior leitura válida até o fim − maior leitura válida antes do início (sem leitura anterior: desde a primeira do período, marcado como não confiável) | `vehicles.view` |
| Consumo médio (km/l) | trechos `Calculated` em litros: Σ km ÷ Σ litros | `fuel.view` |
| Combustível, Manutenção, Pneus, Outras, Total | `CostAggregationService` | cada fatia: `*.viewcosts` da fonte **e** `finance.viewcosts` |
| Custo/km | total ÷ km (só total completo e km confiável ≥ 50) | todas as `*.viewcosts` |
| OS concluídas, corretivas, tempo parado (h) | `WorkOrders` concluídas no período (`DowntimeMinutes`) | `maintenance.view` |
| Trocas de pneu | `TireInstallations` removidas no período (sem estepe) | `tires.view` |

## Problemas recorrentes
Itens de OS **corretivas** concluídas no período, agrupados por veículo + descrição normalizada (sem acento, maiúsculas, pontuação); 2+ ordens distintas = recorrente.

## Comparação e benchmark
1 a 6 veículos + "Média da frota" (veículos ativos) + "Média do tipo X" (se houver 2+ do tipo). Médias por métrica sobre quem tem o dado. Diferença ≥ 15% para pior em relação à frota é destacada.

## Saúde operacional (`VehicleHealthPolicy`)
Áreas: manutenção (preventiva atrasada = crítico; vencendo = atenção), combustível (alerta de consumo ou abastecimento em revisão), pneus (sulco mínimo = crítico; sulco baixo/dano = atenção), custos (alerta de custo acima da média), documentos (vencido = crítico; vencendo = atenção), ocorrências (crítica aberta = crítico; outra aberta = atenção), hodômetro (leitura em revisão ou desatualizada = atenção). Nota = 100 − 10 por atenção − 25 por crítico (mín. 0); ≥ 80 Boa, ≥ 50 Atenção, < 50 Crítica. Área sem permissão = "Sem acesso", não conta.

## Destaques (`InsightService`) e tendência (`TrendAnalysis`)
Janela: últimos 30 dias × 30 anteriores (pneus: 90 × 90). Só variação significativa (base mínima + variação mínima). Destaques: custo por fatia (≥ 10%, base ≥ R$ 500, com os veículos que mais variaram), concentração de manutenção (um veículo ≥ 25% do custo em 90 dias), consumo médio da frota (≥ 5%), veículos acima da própria média (alertas abertos), tempo parado (≥ 15%), corretivas (≥ 25%), trocas de pneu (≥ 25%). Máximo de 5, negativos primeiro.

## Busca global
Por tipo, até 5 resultados, cada tipo com a permissão da sua lista: veículos, implementos, motoristas, pneus, OS, abastecimentos, despesas (sem R$), documentos (de motorista só com `drivers.view`), ocorrências e alertas (por público).

# Assistente (fase final, etapa C) — ADR-050

| Ferramenta | Responde | Permissão |
|---|---|---|
| `get_fleet_ranking` (métrica, ordem, período, limite) | veículo mais caro, pior consumo, acima da média, custo/km, manutenção, pneus | `vehicles.view` + a do dado (custos: `finance.viewcosts` e as `*.viewcosts`) |
| `get_vehicle_analysis` (placa ou `current`) | análise do veículo, "por que o custo aumentou" (período atual × anterior, × média do tipo e da frota, fatia que mais variou, saúde, alertas, recorrentes) | idem, por campo |
| `get_fleet_costs` (período) | quanto gastamos, onde, qual categoria aumentou | `finance.viewcosts` |
| `get_budget_status` | dentro do orçamento? | `finance.viewcosts` |
| `get_open_alerts` | o que merece atenção | `alerts.view` + público |
| `get_fleet_insights` | resumo/tendências | as dos destaques |
| `get_maintenance_overview` | mais manutenções, problemas repetidos, preventivas atrasadas | `maintenance.view` |
| `get_tire_overview` | pneus perto da troca, desgaste, custo/trocas por veículo | `tires.view` |
| `get_fuel_overview` | consumo da frota, piores, fora do padrão, gasto | `fuel.view` |

Períodos: neste mês, mês passado, últimos 30 dias, últimos 90 dias (padrão), este ano. Formato de resposta: Resposta, Motivo, Evidências, Sugestão + fontes (telas) + modo + aviso.
