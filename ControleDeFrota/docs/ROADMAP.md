# Roadmap

Cada fase só começa com a anterior concluída e com os quality gates passando. As fases futuras estão descritas no nível de intenção; o escopo de cada uma é detalhado quando ela iniciar.

## FASE 1 — Fundação (concluída em 2026-09-29)
Arquitetura, documentação, skill `fleet-development`, autenticação (JWT + refresh token), usuários, papéis e permissões, empresas (multi-tenant), motoristas, veículos, implementos, dashboard inicial, auditoria e soft delete.

## FASE 2 — Controle operacional
- Viagens e ordens de serviço de transporte: origem, destino, motorista, veículo e composição.
- **Vínculo veículo ↔ implemento com vigência** (`VehicleImplementCoupling`).
- Alocação motorista ↔ veículo.
- Histórico de leituras de hodômetro/horímetro (com a regra "não retroceder" e correção auditada).
- `OnTrip`/`InUse` passam a ser controlados pelas viagens.
- Bloqueio de motorista com CNH vencida para iniciar viagem.
- Troca de empresa ativa para usuários com acesso a várias empresas.

## FASE 3 — Manutenção
Planos de manutenção preventiva (por km, horas e tempo), ordens de manutenção corretiva, oficinas e fornecedores. `UnderMaintenance` passa a ser controlado pelas ordens.

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
Alertas agendados (vencimento de documentos, CNH, manutenção), notificações por e-mail e push, e regras configuráveis.

## FASE 10 — Integrações
ERP, WhatsApp, consulta de CEP, CNPJ e placa em serviços externos, SSO, API pública para parceiros e reset de senha por e-mail.

## Backlog transversal (quando houver necessidade real)
- Papéis personalizados por empresa (o modelo já suporta via `Roles.CompanyId`).
- Tela de histórico de auditoria por registro.
- Concorrência otimista (`RowVersion`) nas edições.
- OpenTelemetry e métricas.
- Documentos do veículo (CRLV, seguros, ANTT) com vencimento.
