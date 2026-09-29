# Controle de Frota

Plataforma de **gestão e controle de frotas** multiempresa: veículos, implementos, motoristas e, nas próximas fases, operação, manutenção, combustível, pneus, custos, rastreamento e relatórios.

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
| Operação, manutenção, combustível, pneus, financeiro, rastreamento, relatórios, automação, integrações | ver [ROADMAP.md](ROADMAP.md) |

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

Em Development, a API aplica as migrations e cria o banco `ControleDeFrota` no LocalDB na primeira execução, com duas empresas de demonstração (uma com CNPJ alfanumérico), uma frota de exemplo (4 veículos, 2 implementos e 4 motoristas, sendo um com a CNH vencida e outro com a CNH vencendo) e estes usuários, todos com a senha **`FrotaDev!2026`**:

| E-mail | Empresa | Papel | Para testar |
|---|---|---|---|
| admin@frota.local | Rodoxisto (Dev) | Administrador da plataforma | tudo, inclusive a gestão de empresas |
| gestor@frota.local | Rodoxisto (Dev) | Gestor de frota | cadastros da frota e histórico |
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

**Fase 1 — Fundação: concluída** (2026-09-29). Próximo passo: Fase 2 — Controle operacional. Ver [CHANGELOG.md](CHANGELOG.md) e [ROADMAP.md](ROADMAP.md).

## Documentos

[ARCHITECTURE](ARCHITECTURE.md) · [DATABASE](DATABASE.md) · [DOMAIN](DOMAIN.md) · [SECURITY](SECURITY.md) · [UX_UI](UX_UI.md) · [DEVELOPMENT_GUIDELINES](DEVELOPMENT_GUIDELINES.md) · [ROADMAP](ROADMAP.md) · [DECISIONS](DECISIONS.md) · [CHANGELOG](CHANGELOG.md)
