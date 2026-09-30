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

## FASE 2.5 — Viagens e composição (próxima recomendada)
Itens previstos originalmente para a Fase 2 que ficaram fora do escopo redefinido:
- Viagens e ordens de transporte: origem, destino, motorista, veículo e composição.
- **Vínculo veículo ↔ implemento com vigência** (`VehicleImplementCoupling`).
- `OnTrip`/`InUse` controlados pelas viagens; bloqueio de motorista com CNH vencida para iniciar viagem.
- Hodômetro de início e fim de viagem alimentando o histórico de leituras.
- Acesso do motorista ("Meu veículo" no celular: checklist e ocorrência só no veículo alocado), se o negócio confirmar.
- Troca de empresa ativa para usuários com acesso a várias empresas.

## FASE 3 — Manutenção
Planos de manutenção preventiva (por km, horas e tempo), ordens de manutenção corretiva, oficinas e fornecedores. `UnderMaintenance` passa a ser controlado pelas ordens. **Ponto de integração pronto**: ocorrências abertas (e itens reprovados de checklist) viram ordens, e as leituras de hodômetro alimentam a preventiva por km.

## FASE 4 — Gestão de combustível
Abastecimentos, postos, consumo médio (km/l), detecção de anomalias e tanque próprio.

## FASE 5 — Pneus
Cadastro por número de fogo, posições por eixo, rodízio, recapagem, sulco e CPK.

## FASE 6 — Gestão financeira
Custos por veículo (TCO), rateio, depreciação e centro de custo. Multas e sinistros entram aqui ou na Fase 2, conforme a prioridade do negócio.

## FASE 7 — Rastreamento
Integração com rastreadores/telemetria, posição, cercas eletrônicas e telemetria de condução.

## FASE 8 — Relatórios
Relatórios gerenciais, exportação (Excel/PDF) e KPIs de frota no dashboard (disponibilidade, custo/km, consumo).

## FASE 9 — Automação
Notificações por e-mail, push e WhatsApp a partir dos `OperationalEvents` (outbox já existente: `DocumentExpiring`, `DocumentExpired`, `ChecklistFailed`, `OccurrenceCreated`, `MileageAnomalyDetected`…), preferências por usuário e regras configuráveis.

## FASE 10 — Integrações
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
