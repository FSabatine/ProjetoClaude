# Controle de Frota

Plataforma de **gestão e controle de frotas** multiempresa: veículos, implementos, motoristas, controle operacional (alocação, hodômetro, documentos, checklists, ocorrências e histórico) e, nas próximas fases, viagens, manutenção, combustível, pneus, custos, rastreamento e relatórios.

## Objetivos

- Dar ao gestor uma visão confiável e atualizada da frota (disponibilidade, situação e alertas).
- Centralizar os cadastros com qualidade de dados, validando documentos brasileiros na origem.
- Garantir rastreabilidade: quem mudou o quê e quando.
- Ter uma interface moderna, rápida e utilizável no celular (motoristas nas fases futuras).
- Ter uma base segura e extensível, que permita evoluir por fases sem retrabalho de arquitetura.

## Módulos

| Módulo | Situação |
|---|---|
| Autenticação (login, sessão, troca de senha) | Fase 1 |
| Empresas (multiempresa) | Fase 1 |
| Usuários, papéis e permissões | Fase 1 |
| Motoristas | Fase 1 |
| Veículos | Fase 1 |
| Implementos / reboques | Fase 1 |
| Dashboard inicial + alertas de CNH | Fase 1 |
| Auditoria (base) | Fase 1 |
| Situação operacional, alocação motorista ↔ veículo | Fase 2 |
| Histórico de hodômetro (revisão de suspeitas, correção auditada) | Fase 2 |
| Documentos com vencimento e arquivos | Fase 2 |
| Checklists configuráveis (celular) e ocorrências operacionais | Fase 2 |
| Histórico operacional, eventos para notificações, dashboard operacional | Fase 2 |
| Manutenção preventiva e corretiva (planos, solicitações, ordens de serviço, oficinas, horímetro) | Fase 3 |
| Combustível (abastecimentos, postos, preços, consumo, alertas, painel e relatórios) | Fase 4 |
| Viagens, pneus, financeiro, rastreamento, relatórios gerenciais, automação, integrações | ver [ROADMAP.md](ROADMAP.md) |

## Stack

| Camada | Tecnologia |
|---|---|
| Backend | .NET 8, ASP.NET Core Web API, EF Core 8, FluentValidation |
| Banco | SQL Server (LocalDB em desenvolvimento) |
| Autenticação | JWT (15 min) + refresh token rotativo em cookie HttpOnly |
| Frontend | React 18, TypeScript, Vite 5, Mantine 7, TanStack Query, React Router |
| Testes | xUnit, FluentAssertions, SQLite em memória, WebApplicationFactory, Vitest |

## Como executar

Pré-requisitos: .NET SDK 8, Node 18+ (recomendado 20+), SQL Server LocalDB e `dotnet-ef` 8 (`dotnet tool install -g dotnet-ef --version 8.*`).

```bash
# a partir de ControleDeFrota/
dotnet run --project src/Fleet.Api --launch-profile http    # API em http://localhost:5080 (Swagger em /swagger)

cd frontend
npm install
npm run dev                                                 # SPA em http://localhost:5173 (proxy /api → 5080)
# porta 5173 ocupada (ex.: frontend do REC4 rodando)? use: npx vite --port 5174
```

Qualidade (antes de concluir qualquer entrega):

```bash
dotnet build Fleet.sln -warnaserror && dotnet test Fleet.sln
cd frontend && npm run lint && npm test && npm run build
```

Em Development, a API aplica as migrations e cria o banco `ControleDeFrota` no LocalDB na primeira execução, com duas empresas de demonstração (uma com CNPJ alfanumérico), uma frota de exemplo (4 veículos, 2 implementos e 4 motoristas, sendo um com a CNH vencida e outro com a CNH vencendo), dados operacionais de exemplo (modelo "Inspeção diária", dois veículos alocados, histórico de hodômetro com uma leitura suspeita, documentos vencidos e vencendo e uma ocorrência aberta) e estes usuários, todos com a senha **`FrotaDev!2026`**:

| E-mail | Empresa | Papel | Para testar |
|---|---|---|---|
| admin@frota.local | Rodoxisto (Dev) | Administrador da plataforma | tudo, inclusive a gestão de empresas |
| gestor@frota.local | Rodoxisto (Dev) | Gestor de frota | cadastros, operação completa (alocação, revisão de hodômetro, configuração de checklists e tipos de documento) |
| operacao@frota.local | Rodoxisto (Dev) | Operações | operação diária sem correção de hodômetro, exclusão de documento nem configuração (Fase 2); registra abastecimentos sem ver o gasto da frota (Fase 4) |
| manutencao@frota.local | Rodoxisto (Dev) | Manutenção | acompanhamento das ocorrências, somente leitura (Fase 2) |
| consulta@frota.local | Rodoxisto (Dev) | Visualizador | telas somente leitura e 403 |
| operacao@exemplo.local | Exemplo Transportes | Administrador | isolamento entre empresas e estados vazios |

Para recomeçar do zero, apague o banco (`sqllocaldb` / SSMS: `DROP DATABASE ControleDeFrota`) e suba a API de novo.

**Nunca use essas credenciais fora do ambiente local.**

## Configuração de ambiente

| Chave | Onde | Descrição |
|---|---|---|
| `ConnectionStrings:Fleet` | appsettings / env `ConnectionStrings__Fleet` | connection string do SQL Server |
| `Jwt:SigningKey` | **env/cofre em produção** | chave HMAC com no mínimo 32 bytes |
| `Jwt:Issuer`, `Jwt:Audience` | appsettings | |
| `Jwt:AccessTokenMinutes`, `Jwt:RefreshTokenDays` | appsettings | 15 / 7 |
| `Auth:MaxFailedAttempts`, `Auth:LockoutMinutes` | appsettings | 5 / 15 |
| `Database:MigrateOnStartup`, `Database:SeedDevelopmentData`, `Database:SeedSampleData` | somente Development | |
| `Storage:LocalRootPath` | appsettings | pasta dos arquivos enviados (padrão `App_Data/files`, relativa ao content root; ignorada pelo Git). Com várias instâncias, use um storage compartilhado |
| `Jobs:DocumentExpirationScan:Enabled`, `IntervalMinutes` | appsettings | job que emite os eventos de vencimento de documentos (padrão: ligado, a cada 360 min) |
| `Auth:RequestsPerMinutePerIp` | appsettings | limite de login/refresh por IP (10) |
| `Auth:RefreshCookie:Secure` | appsettings | `true` sempre; `false` só nos testes automatizados |
| `Cors:AllowedOrigins` | appsettings | origens da SPA em produção |

## Estrutura

```
ControleDeFrota/
├── CLAUDE.md                     guia rápido para agentes de IA
├── .claude/skills/fleet-development/   skill de padrões do projeto
├── docs/                         documentação oficial (fonte da verdade)
├── Fleet.sln
├── src/
│   ├── Fleet.Domain/             entidades, enums, validação de documentos, catálogo de permissões
│   ├── Fleet.Application/        serviços por módulo, DTOs, validators
│   ├── Fleet.Infrastructure/     EF Core, migrations, JWT, hash de senha, seed
│   └── Fleet.Api/                controllers, auth, middleware
├── tests/
│   ├── Fleet.Domain.Tests/
│   ├── Fleet.Application.Tests/
│   └── Fleet.Api.Tests/
└── frontend/                     SPA React
```

## Status

**Fases 1 (Fundação), 2 (Controle operacional), 3 (Manutenção) e 4 (Combustível): concluídas** (até 2026-10-02). Próximo passo recomendado: Fase 2.5 — Viagens e composição. Ver [CHANGELOG.md](CHANGELOG.md) e [ROADMAP.md](ROADMAP.md).

## Documentos

[ARCHITECTURE](ARCHITECTURE.md) · [DATABASE](DATABASE.md) · [DOMAIN](DOMAIN.md) · [SECURITY](SECURITY.md) · [UX_UI](UX_UI.md) · [DEVELOPMENT_GUIDELINES](DEVELOPMENT_GUIDELINES.md) · [ROADMAP](ROADMAP.md) · [DECISIONS](DECISIONS.md) · [CHANGELOG](CHANGELOG.md)

### Dados de exemplo da Fase 4

Com `Database:SeedSampleData`, a primeira execução (ou a próxima, em bancos de fases anteriores) cria 3 postos — um deles o tanque próprio —, preços de referência e cerca de 4 meses de abastecimentos dos veículos RDX2B34, RDX3C45 e ABC1234, terminando no hodômetro atual de cada um. Dois abastecimentos ficam em "Requer revisão" de propósito (760 L num tanque de 700 L e um trecho com consumo bem abaixo do esperado) para demonstrar o fluxo de revisão.
