# Changelog

Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/). Datas no padrão AAAA-MM-DD.

## [0.4.0] — 2026-10-02 — Fase 4: Combustível

### Adicionado
- **Tipos de combustível** configuráveis por empresa, com catálogo padrão (Diesel S10/S500, gasolinas, etanol, GNV em m³, recarga elétrica em kWh) e unidade travada depois de usado (ADR-031).
- **Postos de combustível** (com o tanque próprio identificado) e **preços de referência** por posto × combustível com vigência; evento `FuelPriceChanged`.
- **Abastecimentos**: total calculado no servidor (o total do cupom é só conferido), validação de veículo/motorista/combustível/posto, comprovantes (foto pela câmera, PDF), forma de pagamento, tanque cheio ou complemento. Nunca excluídos: **correção auditada** (motivo, quem, quando, campo de → para) e **cancelamento** com motivo.
- **Integração com o hodômetro** (fonte única): o abastecimento vira leitura `Fueling` pelas regras da Fase 2; abastecimento lançado depois é validado contra as leituras vizinhas sem gerar leitura; correção do km vira correção de hodômetro auditada (ADR-032).
- **Consumo tanque cheio a tanque cheio** com snapshot no abastecimento que fecha o trecho e **consumo esperado** (configurado no veículo > histórico do veículo > média do tipo de veículo).
- **Alertas para revisão** com limites configuráveis: quantidade acima do tanque, preço fora da média/referência, hodômetro em revisão, abastecimentos muito próximos, combustível incompatível com o veículo, consumo abaixo/acima do esperado. Textos neutros; nenhum alerta abre manutenção sozinho.
- **Painel de Combustível**, **aba Combustível no veículo** (histórico por dia/semana/mês) e **relatórios** (abastecimentos, consumo, custos por veículo/motorista/posto/combustível, postos, preços por mês), agregados no banco.
- Alerta "Abastecimento requer revisão" no painel principal (para quem pode revisar).
- Cadastro do veículo: capacidade do tanque principal e do segundo tanque, consumo esperado.
- Permissões `fuel.view/create/correct/cancel/reviewanomalies/managestations/configure/viewcosts` e mapeamento nos papéis (Operações registra sem ver o gasto da frota; Financeiro vê custos; Motorista continua sem acesso).
- Eventos `FuelingRecorded`, `FuelingCorrected`, `FuelingCancelled`, `FuelingMarkedForReview`, `FuelingReviewed`, `FuelConsumptionAnomalyDetected`, `FuelingMileageInconsistencyDetected`, `FuelPriceChanged` (sem valores em R$ nos resumos).
- **Central de Ajuda**: categoria Combustível com 14 artigos, ajuda contextual nas telas de combustível e na aba do veículo, "Novidades" atualizada; `[?]` nas métricas de consumo e custo/km.
- Dados de desenvolvimento da Fase 4 (`DevFuelSeeder`).
- **Testes**: +50 no Domain (regras puras; 194 → 244), +60 de serviço (180 → 240, incluindo tenant, permissões, estabilidade histórica e volume com 12 mil abastecimentos), +14 HTTP (28 → 42) e +10 no frontend (55 → 65).

### Alterado
- O enum `FuelType` do veículo passou a se chamar `VehicleFuelType` no código (o banco e a API não mudaram).
- `MileageService` ganhou operações aditivas para outros módulos (aprovar/rejeitar leitura pendente dentro da transação do chamador, linha de base pública, checagem contra o histórico). Sem mudança de comportamento da Fase 2.
- `FleetDbContext`: `decimal` é gravado como REAL **apenas no SQLite dos testes**, para permitir agregação no banco (ADR-033). SQL Server inalterado.
- Não se exclui veículo com abastecimentos (inative).

### Corrigido
- O histórico de auditoria (`/audit/{entidade}/{id}`) recusava as entidades da Fase 3 (oficina, plano, solicitação, ordem de serviço, horímetro), embora a tela oferecesse o botão "Histórico". Agora aceita essas e as da Fase 4.

### Limitações conhecidas
- Sem exportação de relatórios (o sistema ainda não tem exportação; Fase 8).
- Sem estoque do tanque próprio, cartão combustível ou integração com fornecedores (só a preparação).
- Sem revisão visual automatizada (headless desaconselhado nesta máquina): conferir telas em 375px/tablet/desktop e nos dois temas.

## [0.3.1] — 2026-10-01 — Central de Ajuda (manual do usuário)

### Adicionado
- **Central de Ajuda**: ícone `?` no cabeçalho, ao lado do seletor de tema, abrindo um `Drawer` com busca, categorias e artigos, sem navegar para outra rota (ADR-029/030 em DECISIONS.md).
- **Manual completo** de tudo o que já existe: Primeiros passos, Painel, Empresas, Usuários, Papéis e permissões, Motoristas, Veículos, Implementos, Alocações, Quilometragem, Documentos, Checklists, Ocorrências, Manutenção, Busca e filtros, e Perguntas frequentes — só funcionalidade real, nada planejado.
- **Busca** client-side (sem lib nova) e **ajuda contextual**: a categoria mais relevante para a tela atual aparece primeiro (ex.: abrir a ajuda na aba Manutenção do veículo prioriza a categoria Manutenção).
- **"Novidades"**: bloco com os marcos já lançados (Fase 2 e Fase 3), nunca um recurso futuro.
- Permissão opcional por artigo (reaproveita o catálogo existente): por padrão todo artigo é visível, só os de Empresas/Usuários/Papéis exigem a permissão de visualização do módulo.
- **Testes**: 15 novos no frontend (busca, ajuda contextual e integridade do conteúdo), totalizando 55.
- Regra nova no `fleet-development` e em `DEVELOPMENT_GUIDELINES.md`: funcionalidade visível ao usuário ganha artigo no mesmo PR.

### Limitações conhecidas
- Sem teste automatizado de interação (abrir/fechar, clique, responsividade, tema) — o projeto não tem Testing Library/jsdom de componente configurado; verificação manual.
- Analytics de uso (artigo mais visto, busca sem resultado) é só o ponto de extensão — sem destino real ainda.

## [0.3.0] — 2026-10-01 — Fase 3: Manutenção

### Adicionado
- **Planos de manutenção preventiva** (ADR-027): itens com intervalo por km, meses e/ou horas (o primeiro a vencer dispara a manutenção), carência configurável por eixo, precedência veículo específico > tipo de veículo > padrão da empresa (`MaintenancePlanResolver`).
- **Agenda de manutenção**: status calculado `Scheduled/DueSoon/Due/Overdue` (`MaintenanceSchedulePolicy`, mesma forma do `DocumentExpiryPolicy`), recalculado quando um item ligado a uma ordem de serviço é concluído; veículos nunca atendidos usam a linha de base do cadastro.
- **Histórico de horímetro** (ADR-027): mesma forma do hodômetro (ADR-019) — não retrocede, salto suspeito (> 20 h/dia) fica pendente de revisão, correção auditada. `Vehicle.HourMeter` deixou de ser editável no cadastro depois de criado.
- **Solicitações de manutenção**: origem (motorista, checklist, ocorrência, gestor, alerta automático), prioridade, tipo; aprovar abre a ordem de serviço na mesma transação (`Status = Approved`); rejeitar exige motivo. Botão manual "Abrir solicitação de manutenção" na ocorrência — nunca automático.
- **Ordens de serviço**: número sequencial por empresa (`OS-000001`), máquina de estados `Draft → Approved → Scheduled → InProgress ⇄ WaitingParts → Completed` (+ `Cancelled`/`Rejected`), itens (com itens obrigatórios bloqueando o fechamento), peças e mão de obra como linhas de custo, tempo de indisponibilidade calculado no fechamento.
- **`Vehicle.Status = UnderMaintenance` controlado pelas ordens de serviço** (ADR-028): entra em manutenção ao iniciar a execução, só volta a `Available` quando nenhuma outra ordem ativa resta e ninguém mudou o status manualmente nesse meio tempo.
- **Oficinas**: cadastro simples (interno ou externo), vinculado à ordem de serviço.
- **Permissões**: 6 novas (`maintenance.view`, `maintenance.createrequest`, `maintenance.manageplans`, `maintenance.manageworkorders`, `maintenance.manageworkshops`, `maintenance.viewcosts`). O papel Manutenção ganhou o conjunto completo; Operações ganhou `view`+`createrequest`; Financeiro ganhou `view`+`viewcosts`.
- **API**: endpoints de `/workshops`, `/maintenance-plans`, `/maintenance-requests`, `/work-orders` (+ status, itens, peças, mão de obra), `/vehicles/{id}/hour-meter-readings`, `/vehicles/{id}/maintenance/{schedule,history,repeated-problems}`.
- **Dashboard**: bloco "Manutenção" (vence hoje/vencendo/atrasadas/em andamento/aguardando peças/concluídas no mês/veículos em manutenção) e alertas de ordens de serviço críticas em aberto.
- **Frontend**: módulo `features/maintenance/` completo (oficinas, planos com itens dinâmicos, solicitações com aprovação/rejeição, ordens de serviço com fila e hub de execução), nova aba "Manutenção" no hub do veículo (próximas, ordens, problemas recorrentes).
- **Banco**: migration `Maintenance`, somente aditiva (10 tabelas, `Vehicles.HourMeterUpdatedAt`, seed de permissões e papéis).
- **Testes**: 78 novos no backend (42 de domínio, 31 de serviços, 5 de integração HTTP), totalizando 402. O lint, o build e os 40 testes de frontend continuam verdes; sem verificação visual automatizada (browser headless indisponível nesta máquina).

### Alterado
- O horímetro deixou de ser editável no cadastro do veículo depois de criado, do mesmo jeito que o hodômetro desde a Fase 2: muda só por leituras (`HourMeterService`), com a API recusando a alteração direta com uma mensagem orientando.
- `BrazilianFormat.Number` ganhou uma sobrecarga para `decimal` com casas decimais (horas, valores), além do inteiro já existente.
- `ROADMAP.md`/`CLAUDE.md`: a Fase 2.5 (Viagens) foi conscientemente adiada por decisão do usuário; a Fase 3 (Manutenção) entrou no lugar (ADR-026).

### Corrigido
- Desempate de ordenação de alocações (`AssignmentService`) por `Id` não era confiável no SQLite dos testes (o GUID sequencial só ordena corretamente sob a comparação específica do SQL Server); trocado por um desempate com significado de negócio (alocação ativa primeiro).

### Segurança
- `maintenance.viewcosts` checada no serviço: sem ela, os campos de custo da ordem de serviço voltam `null`/zerados em vez de recusar o acesso ao resto do registro.
- Início de ordem de serviço recusa veículo em viagem ou inativo; fechamento exige todos os itens obrigatórios resolvidos — regras que não dá para expressar como atributo de rota, checadas no serviço.
- Dependências sem vulnerabilidades conhecidas: `dotnet list package --vulnerable --include-transitive` e `npm audit --omit=dev`.

## [0.2.0] — 2026-09-30 — Fase 2: Controle operacional

### Adicionado
- **Situação operacional do veículo** (ADR-018): nova condição `Indisponível` e situação derivada `Alocado` (disponível + motorista), exibida em listas, hub e dashboard, sem gravar dois conceitos no mesmo campo.
- **Alocação motorista ↔ veículo** (ADR-020): histórico com vigência, um veículo por motorista e um motorista por veículo (índices únicos filtrados), bloqueio de veículo inativo, motorista desligado/afastado e CNH vencida, e troca com confirmação feita em uma transação.
- **Histórico de hodômetro** (ADR-019): leitura inicial no cadastro, regra "não retroceder", salto suspeito (> 1.500 km/dia) pendente de revisão sem alterar o hodômetro, aprovação/rejeição e correção auditada com motivo.
- **Documentos** (ADR-021): tipos configuráveis por empresa (16 padrões), documentos de veículo, motorista, implemento e empresa, status calculado com antecedência de alerta por tipo, renovação e exclusão auditada.
- **Arquivos** (ADR-022): upload de PDF/JPG/PNG validado pelo conteúdo (10 MB), armazenamento fora do banco atrás de `IFileStorage` e download com permissão do dono. Fotos reduzidas no celular antes do envio.
- **Checklists** (ADR-024): modelos versionados com seções e três tipos de resposta, execução com snapshot imutável, foto obrigatória configurável, hodômetro opcional, item reprovado → ocorrência e checklists pendentes do dia.
- **Ocorrências** (ADR-023): tipos, gravidade, fotos e fluxo `Aberta → Em análise → Resolvida | Cancelada`, com texto obrigatório no encerramento.
- **Histórico operacional e eventos** (ADR-025): tabela `OperationalEvents` (linha do tempo + outbox) com 16 tipos de evento e job `DocumentExpirationJob` que emite `DocumentExpiring`/`DocumentExpired` uma vez por mudança de estado.
- **Dashboard operacional**: frota por situação, documentos vencidos e vencendo, checklists pendentes, ocorrências abertas e críticas, veículos sem leitura recente, quilometragem do mês e alertas combinados, cada um levando à aba certa.
- **Permissões**: 13 novas (`assignments.*`, `mileage.*`, `documents.*`, `checklists.*`, `occurrences.*`, `operations.configure`), com a matriz dos papéis atualizada. O papel Motorista continua sem acesso (decisão do usuário).
- **API**: 30 endpoints novos (ver ARCHITECTURE.md), filtros no servidor para veículos (situação operacional, motorista, faixa de km, sem leitura recente), motoristas (categoria, com/sem veículo), documentos, ocorrências e checklists, e auditoria das novas entidades.
- **Frontend**:
  - hubs com abas para veículo e motorista (edição em `/editar`);
  - páginas de Documentos, Ocorrências (lista e detalhe), Checklists (pendentes, histórico e detalhe) e Realizar checklist (mobile-first);
  - configuração de Modelos de checklist e Tipos de documento;
  - seletores com busca no servidor, anexos com câmera, feedback de hodômetro enquanto se digita e tela amigável para erro inesperado.
- **Banco**: migration `OperationalControl`, somente aditiva (11 tabelas, `Vehicles.OdometerUpdatedAt`, índices por `(CompanyId, dono, data)`, check constraint de dono do documento).
- **Seed de desenvolvimento**: idempotente, com os usuários `operacao@frota.local` e `manutencao@frota.local`, o modelo "Inspeção diária", alocações, leituras (uma suspeita), documentos em todos os estados e uma ocorrência.
- **Testes**: 148 novos no backend (62 de domínio, 79 de serviços, 7 de integração HTTP), totalizando 324, e 5 no frontend (40 no total). Os fluxos 1 a 4 da especificação foram verificados de ponta a ponta na interface, em viewport de celular.

### Alterado
- O hodômetro deixou de ser editável no cadastro do veículo depois de criado: muda só por leituras (a API recusa a alteração com uma mensagem orientando).
- Veículo e motorista com histórico operacional não podem mais ser excluídos (inativar/desligar); os documentos são excluídos junto quando a exclusão é permitida. Implemento com ocorrências também não pode ser excluído.
- `IClock` ganhou `ToBusinessDateTime`, `ToBusinessDate` e `StartOfBusinessDayUtc`, a fonte única de fuso para "hoje" e "este mês".
- `IFleetDbContext.InTransactionAsync` para operações com vários `SaveChanges` atômicos.

### Corrigido
- `NumberInput` com `thousandSeparator="."` sem `decimalSeparator=","` derrubava a tela (inclusive o hodômetro do cadastro de veículo, desde a Fase 1).

### Segurança
- Upload validado por magic bytes, tamanho limitado em duas camadas, nome sanitizado, chave gerada pelo servidor e caminho confinado à raiz do storage. Anexos só podem ser vinculados pelo autor do upload.
- Nome do motorista no contexto do veículo limitado ao nome (demais dados pessoais exigem `drivers.view`).
- Risco registrado: o rate limit compartilhado entre login e `/auth/refresh` pode gerar 429 em recarregamentos seguidos ou atrás de NAT (recomendação em DECISIONS).
- Dependências sem vulnerabilidades conhecidas: `dotnet list package --vulnerable` e `npm audit --omit=dev`.

## [0.1.0] — 2026-09-29 — Fase 1: Fundação

### Adicionado
- **Projeto**: análise inicial (pasta vazia), arquitetura definida (ADR-001 a ADR-017), documentação em `/docs`, `CLAUDE.md` e skill de projeto `fleet-development`.
- **Backend (.NET 8, Clean Architecture)**: solução `Fleet.sln` com Domain, Application, Infrastructure e Api.
- **Banco**: migration `InitialFoundation` com as tabelas Companies, Users, Roles, Permissions, RolePermissions, UserRoles, RefreshTokens, Drivers, Vehicles, Implements e AuditLogs. Inclui soft delete com índices únicos filtrados e seed de permissões e papéis do sistema.
- **Multiempresa**: filtro global por `CompanyId`, `CompanyId` carimbado pelo servidor, 404 para registros de outra empresa e super-admin via `companies.manage`.
- **Autenticação**: JWT de 15 minutos, refresh token rotativo em cookie HttpOnly com detecção de reuso, bloqueio após 5 falhas, rate limit no login, troca de senha e redefinição de senha pelo administrador.
- **Autorização**: 20 permissões granulares, 8 papéis do sistema, `[HasPermission]` em todos os endpoints, fallback policy autenticada e anti-escalonamento de privilégio.
- **Módulos**: Empresas, Usuários, Papéis (leitura), Motoristas, Veículos, Implementos, Painel (4 indicadores + alertas de CNH) e Histórico de auditoria por registro.
- **Validações** (backend e frontend espelhados): CPF, CNPJ numérico e **alfanumérico**, placa antiga e Mercosul, RENAVAM, chassi (VIN), CEP, telefone, e-mail, UF, CNH, faixas de ano e datas.
- **Auditoria**: diff automático (valor antigo → novo) de Company, User, Driver, Vehicle e Implement, inclusive troca de papéis. A senha é mascarada.
- **API**: REST em `/api/v1`, erros em ProblemDetails com mensagens pt-BR e erros por campo, headers de segurança, `X-Trace-Id`, `/health` e Swagger (somente em Development).
- **Frontend (React 18 + Mantine 7)**: login, layout responsivo com menu por permissão, tema claro/escuro, listas com busca, filtros na URL, ordenação, paginação e cards no celular, formulários com seções, máscaras e validação inline, estados de carregamento, vazio, erro e 403, confirmação de exclusão, aviso de alterações não salvas, modo somente leitura e carregamento das páginas sob demanda.
- **Testes**: 176 no backend (90 de domínio, 70 de serviços com SQLite, 16 de integração HTTP) e 35 no frontend (Vitest).
- **Seed de desenvolvimento**: duas empresas, quatro usuários de demonstração e uma frota de exemplo.

### Segurança
- Dependências atualizadas para versões sem vulnerabilidades conhecidas em produção: axios 1.20, react-router 7.18, pacotes de teste do xUnit.
- Residual aceito, somente em desenvolvimento: 1 aviso moderado no Vitest 3.x, cuja correção exige Node 20+ (ver SECURITY.md).
