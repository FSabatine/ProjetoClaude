# Roadmap

Cada fase só começa com a anterior concluída e com os quality gates passando. As fases futuras estão descritas no nível de intenção; o escopo de cada uma é detalhado quando ela iniciar.

## FASE 1 — Fundação (concluída em 2026-09-29)
Arquitetura, documentação, skill `fleet-development`, autenticação (JWT + refresh token), usuários, papéis e permissões, empresas (multi-tenant), motoristas, veículos, implementos, dashboard inicial, auditoria e soft delete.

## FASE 2 — Controle operacional (concluída em 2026-09-30)
O escopo foi redefinido pelo usuário: controle operacional do dia a dia, **sem** viagens.
- Situação operacional derivada do veículo (Disponível, Alocado, Em viagem, Indisponível, Em manutenção, Inativo).
- Alocação motorista ↔ veículo com vigência e histórico.
- Histórico de leituras de hodômetro com "não retroceder", revisão de leituras suspeitas e correção auditada.
- Documentos com tipos configuráveis, vencimento calculado, renovação e arquivos (PDF/JPG/PNG).
- Checklists configuráveis e versionados, execução no celular com fotos e item reprovado → ocorrência.
- Ocorrências operacionais com fluxo de situação.
- Histórico operacional do veículo e do motorista + base de eventos para notificações (outbox).
- Dashboard operacional.

## FASE 2.5 — Viagens e composição (adiada)
itens previstos originalmente para a Fase 2 que ficaram fora do escopo redefinido. Era a próxima fase recomendada, mas o usuário decidiu conscientemente pular para a Fase 3 (2026-10-01, ADR-026) e depois pediu as Fases 4 e 5 (2026-10-02, ADR-031/ADR-035) — continua no backlog e é a **próxima fase recomendada**:
- Viagens e ordens de transporte: origem, destino, motorista, veículo e composição.
- **Vínculo veículo ↔ implemento com vigência** (`VehicleImplementCoupling`).
- `OnTrip`/`InUse` controlados pelas viagens; bloqueio de motorista com CNH vencida para iniciar viagem.
- Hodômetro de início e fim de viagem alimentando o histórico de leituras.
- Acesso do motorista ("Meu veículo" no celular: checklist e ocorrência só no veículo alocado), se o negócio confirmar.
- Troca de empresa ativa para usuários com acesso a várias empresas.

## FASE 3 — Manutenção (concluída em 2026-10-01)
Planos de manutenção preventiva (por km, horas e tempo, com carência configurável e precedência veículo > tipo de veículo > padrão da empresa), solicitações de manutenção (motorista/checklist/ocorrência/gestor → aprovação → ordem de serviço), ordens de serviço com máquina de estados (itens, peças, mão de obra, custo, tempo de indisponibilidade), oficinas e histórico de horímetro (mesma forma do hodômetro). `UnderMaintenance` passa a ser controlado pelas ordens de serviço (ADR-028), sem sobrescrever uma mudança manual do veículo. Ocorrências abertas (e itens reprovados de checklist, via ocorrência) viram solicitações de manutenção por ação manual do gestor; as leituras de hodômetro alimentam a preventiva por km.
**Fora do escopo desta fase** (pontos de extensão deixados prontos): inventário/estoque de peças, ordens de compra, entidade de mecânico/técnico interno (técnico é texto livre), calendário visual de manutenções (lista agrupada por dia no lugar), manutenção preditiva/IA, notificações por e-mail/WhatsApp (só o evento operacional fica pronto para a Fase 9).

## FASE 4 — Gestão de combustível (concluída em 2026-10-02)
Tipos de combustível configuráveis, postos (com tanque próprio identificado) e preços de referência, abastecimentos com total calculado no servidor e integração com o histórico de hodômetro, consumo tanque cheio a tanque cheio com consumo esperado (configurado > histórico do veículo > tipo de veículo), alertas neutros para revisão (quantidade acima do tanque, preço, hodômetro, frequência, combustível incompatível, consumo abaixo/acima do esperado) com limites configuráveis, correção auditada e cancelamento, painel de Combustível, aba Combustível do veículo e relatórios (abastecimentos, consumo, custos, postos, preços). Pedida diretamente pelo usuário, com a Fase 2.5 ainda no backlog (ADR-031).
**Fora do escopo desta fase** (pontos de extensão deixados prontos): estoque do tanque próprio, cartão combustível e integração com fornecedores (só `PaymentMethod.FuelCard` e `Fueling.Source`), exportação de relatórios (Fase 8), notificações (eventos prontos para a Fase 9), telemetria/GPS (Fase 7), acesso do motorista.

## FASE 5 — Pneus (concluída em 2026-10-02)
Cada pneu pelo número de fogo (identidade própria, passa por vários veículos), catálogo de modelos, configurações de eixos reutilizáveis por veículos e implementos (posições geradas, medida exigida e pressão de referência por eixo), diagrama interativo do veículo visto de cima (lista no celular), instalação com compatibilidade, remoção com motivo e destino, substituição, transferência e **rodízio atômicos**, inspeção (sulco, pressão, condição, desgaste, danos, fotos) com histórico de medições, consertos (inclusive no veículo) e recapagens com resultado do fornecedor, custos e custo/km do ciclo de vida, baixa, alertas pela política da empresa e sinais "requer revisão", painel e relatórios de pneus. Km do pneu a partir do histórico de hodômetro; concorrência garantida no banco (ADR-035 a ADR-039). Pedida diretamente pelo usuário, com a Fase 2.5 ainda no backlog.
**Fora do escopo desta fase** (pontos de extensão prontos): almoxarifado/estoque e movimentações, pedidos de compra e fornecedores, TPMS/sensores e telemetria, rastreamento de pneu por GPS, predição de desgaste e diagnóstico por IA, contabilidade, integração com fornecedores de pneus, venda de pneus (só a baixa com motivo), km de implemento (depende do engate da Fase 2.5), exportação de relatórios (Fase 8).

## FASE 6 — Gestão financeira (concluída em 2026-10-05)
Centros de custo e categorias de despesa hierárquicos e configuráveis (três categorias — Combustível, Manutenção, Pneus — automáticas, alimentadas pelos próprios módulos, sem lançamento manual nem duplicação de dado). Despesas manuais com situação de pagamento calculada (pendente, agendado, parcialmente pago, pago, atrasado, cancelado — nunca excluída, só cancelada), pagamento parcial/total, anexos e aviso neutro de possível duplicidade. Despesas recorrentes geradas automaticamente até 30 dias antes do vencimento (job idempotente). Orçamentos por ano/mês × categoria × (centro de custo ou veículo opcional) com orçado x realizado calculado na leitura. Custo do veículo e da frota combinando, sem duplicar, os dados de combustível/manutenção/pneus com as despesas manuais — cada fatia só aparece para quem tem a permissão de custo daquele módulo e de `finance.viewcosts`, com aviso de "totais parciais" quando falta alguma. Custo por km e TCO (valor de aquisição + custo operacional acumulado) com guarda contra quilometragem insuficiente. Ranking de veículos por custo, painel financeiro, aba Financeiro do veículo e relatórios (despesas, centro de custo, custo mensal da frota, orçado x realizado, TCO). Pedida diretamente pelo usuário, com a Fase 2.5 ainda no backlog (ADR-040).
**Fora do escopo desta fase** (pontos de extensão deixados prontos): exportação de relatórios (Fase 8), rateio/depreciação contábil (análise operacional apenas), cadastro genérico de fornecedores (oficina da Fase 3 + texto livre).

## FASE FINAL — Inteligência, automação, relatórios e integração (concluída em 2026-10-05)
Pedida pelo usuário como a última fase planejada, reunindo as intenções das Fases 7–10 e um assistente de IA (ADR-045). Entregue em etapas, cada uma com quality gates e commit:
- **A — concluída (2026-10-05)**: motor de automação, alertas persistidos, notificações no app, "Requer atenção" e painel executivo.
- **B — concluída (2026-10-05)**: relatórios cruzados, exportação (CSV/Excel/PDF), busca global, linha do tempo com filtro por área e por permissão, saúde do veículo, comparação e benchmarking interno, tendências e destaques.
- **C — concluída (2026-10-05)**: assistente da frota (modo calculado + Claude opcional; números sempre calculados pelo sistema).
- **D — concluída (2026-10-05)**: base de rastreamento/GPS (provedor, dispositivo, instalação, posição), API de recebimento, mapa OpenStreetMap, localização do veículo e página de integrações.
- **E — concluída (2026-10-05)**: revisão final de segurança, desempenho e UX; manual completo; documentação e skill.

Esta foi a última fase planejada. Nenhuma fase nova começa automaticamente.

## FASE 7 — Rastreamento
**Base entregue na fase final (etapa D)**: rastreadores, recebimento, mapa e rota. Fora: cercas eletrônicas, telemetria de condução.
Integração com rastreadores/telemetria, posição, cercas eletrônicas e telemetria de condução.

## FASE 8 — Relatórios
**Entregue na fase final (etapa B)**: relatórios cruzados, exportação CSV/Excel/PDF, KPIs e destaques no painel.
Relatórios gerenciais, exportação (Excel/PDF) e KPIs de frota no dashboard (disponibilidade, custo/km, consumo).

## FASE 9 — Automação
**Entregue na fase final (etapa A)** com notificações só no app; e-mail/push/WhatsApp e preferências por usuário ficam como ações futuras das regras.
Notificações por e-mail, push e WhatsApp a partir dos `OperationalEvents` (outbox já existente: `DocumentExpiring`, `DocumentExpired`, `ChecklistFailed`, `OccurrenceCreated`, `MileageAnomalyDetected`, `FuelingMarkedForReview`, `FuelConsumptionAnomalyDetected`, `TireTreadLow`, `TireInspectionFailed`, `TireAnomalyDetected`…), preferências por usuário e regras configuráveis.

## FASE 10 — Integrações
**Arquitetura entregue na fase final (etapa D)** (portas/adaptadores e página de integrações); integrações com terceiros não implementadas.
ERP, WhatsApp, consulta de CEP, CNPJ e placa em serviços externos, SSO, API pública para parceiros e reset de senha por e-mail.

## Backlog transversal (quando houver necessidade real)
- Papéis personalizados por empresa (o modelo já suporta via `Roles.CompanyId`).
- Tela de histórico de auditoria por registro.
- Concorrência otimista (`RowVersion`) nas edições.
- OpenTelemetry e métricas.
- ~~Documentos do veículo (CRLV, seguros, ANTT) com vencimento~~ (entregue na Fase 2).
- Limpeza de arquivos órfãos e antivírus no upload.
- Política de rate limit própria para `/auth/refresh`.
- Mapa de rótulos pt-BR dos campos no histórico de auditoria.
