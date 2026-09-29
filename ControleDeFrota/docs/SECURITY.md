# Segurança

Princípio: **Segurança > Conveniência**. O backend é a única autoridade. Esconder um botão no frontend é UX, não controle de acesso.

## Autenticação

- Login por e-mail e senha: `POST /api/v1/auth/login`.
- **Access token**: JWT HS256, válido por **15 minutos**. Claims: `sub` (UserId), `name`, `email`, `company_id`, `perm` (um por permissão efetiva) e `jti`. Vai no header `Authorization: Bearer`.
- **Refresh token**: 64 bytes aleatórios (CSPRNG), válido por **7 dias**, entregue **apenas em cookie** `fleet_refresh` com `HttpOnly`, `Secure`, `SameSite=Strict` e `Path=/api/v1/auth`. No banco fica só o hash SHA-256.
  - **Rotação**: cada `POST /auth/refresh` invalida o token usado e emite outro.
  - **Detecção de reuso**: se um token já rotacionado for apresentado de novo (sinal de roubo), todas as sessões do usuário são revogadas.
  - O refresh relê do banco o status do usuário, o status da empresa e as permissões. Uma mudança de papel reflete em até 15 minutos, e uma inativação corta a sessão na próxima renovação.
- **Frontend**: o access token fica **somente em memória**, nunca em `localStorage` ou `sessionStorage`, o que mitiga o roubo via XSS. Ao recarregar a página, a SPA chama `/auth/refresh` e o cookie restaura a sessão sem novo login (ADR-007).
- `POST /api/v1/auth/logout` revoga o refresh token e apaga o cookie.

## Credenciais

- Hash **PBKDF2-HMAC-SHA256** (formato v3 do ASP.NET Core Identity, 100.000 iterações, salt por senha), via `Microsoft.Extensions.Identity.Core`. O `PasswordHasher` rehasheia automaticamente quando o algoritmo evoluir (`SuccessRehashNeeded`).
- Política (seguindo o NIST SP 800-63B, que valoriza comprimento acima de complexidade): **10 a 128 caracteres**, com pelo menos uma letra e um número, e sem conter o e-mail do usuário.
- Troca de senha exige a senha atual e revoga as outras sessões.
- Mensagem de login genérica ("E-mail ou senha inválidos"), para não revelar se o e-mail existe.
- **Bloqueio**: 5 falhas consecutivas bloqueiam a conta por 15 minutos (`Auth:MaxFailedAttempts`, `Auth:LockoutMinutes`).
- **Rate limiting**: `POST /auth/login` e `/auth/refresh` aceitam até 10 requisições por minuto por IP. Acima disso, a resposta é 429.
- Não há "esqueci minha senha" nesta fase. O administrador redefine a senha pela tela de usuários. O reset por e-mail depende de um serviço de e-mail (fase de integrações).

## Autorização

- Modelo **RBAC com permissões granulares**: User → Roles → Permissions. As permissões efetivas são a união das permissões dos papéis.
- Endpoints são protegidos com `[HasPermission(Permissions.Vehicles.Update)]`, que gera a policy `perm:vehicles.update`, resolvida por um `IAuthorizationPolicyProvider` dinâmico. Não é preciso registrar as policies uma a uma.
- **Proibido** decidir acesso pelo nome ou pela chave de papel. A única exceção é o seed, que define quais permissões cada papel do sistema tem.
- **Isolamento de tenant** (defesa em profundidade):
  1. filtro global do EF por `CompanyId` em toda entidade `ITenantScoped`;
  2. `CompanyId` carimbado pelo servidor na inclusão, ignorando o que vier do cliente;
  3. um ID de outra empresa responde 404.
- **Anti-escalonamento**: um usuário só atribui papéis cujas permissões ele próprio tem. Ninguém altera os próprios papéis nem o próprio status.

## Proteção de dados (LGPD)

- CPF, RG, CNH, telefone, e-mail e endereço de motoristas são **dados pessoais**. O acesso exige `drivers.view`, e toda alteração é auditada.
- Logs de aplicação **não** registram dados pessoais nem corpos de requisição.
- Em produção, a connection string e a chave JWT vêm de variáveis de ambiente ou de um cofre (Azure Key Vault ou equivalente), **nunca do repositório**. `appsettings.Development.json` contém apenas segredos de desenvolvimento.
- HTTPS obrigatório fora de Development (HSTS + redirecionamento).
- Pendências futuras: base legal documentada, política de retenção e anonimização de motoristas desligados, e exportação dos dados do titular.

## Auditoria

- Toda criação, alteração e exclusão (soft delete) de Company, User, Driver, Vehicle e Implement grava `AuditLogs` com o usuário, a data, os campos e os valores antigo e novo. Ver DATABASE.md.
- Os logins bem-sucedidos e as falhas de login também vão para o log da aplicação, no nível Information/Warning.
- A leitura do histórico exige `audit.view`.

## OWASP Top 10 (2021): como cada risco é tratado

| Risco | Medidas |
|---|---|
| A01 Broken Access Control | policies por permissão em **todo** endpoint (padrão `[Authorize]` global com fallback policy); filtro de tenant; 404 cross-tenant; anti-escalonamento; testes de autorização |
| A02 Cryptographic Failures | PBKDF2; refresh token com hash; HTTPS/HSTS; segredos fora do repositório |
| A03 Injection | EF Core parametrizado; nenhum SQL concatenado; validação de entrada (FluentValidation) e whitelist de colunas de ordenação |
| A04 Insecure Design | regras de negócio no backend; limites de paginação; bloqueio de conta; rate limit |
| A05 Security Misconfiguration | headers `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy`, CSP restritiva na API; Swagger só em Development; erros sem stack trace |
| A06 Vulnerable Components | `dotnet list package --vulnerable` e `npm audit` nos quality gates |
| A07 Identification & Auth Failures | política de senha; bloqueio; rotação e detecção de reuso de refresh token; mensagens genéricas |
| A08 Software & Data Integrity | lockfiles versionados; migrations revisadas |
| A09 Logging & Monitoring Failures | auditoria + logs estruturados com `traceId`; falhas de login registradas |
| A10 SSRF | a API não faz requisições a URLs fornecidas pelo usuário |

## CORS e CSRF

- Em desenvolvimento, o Vite faz proxy de `/api`, então tudo fica na mesma origem. Em produção, as origens permitidas vêm de `Cors:AllowedOrigins`, com `AllowCredentials` apenas para elas.
- O cookie de refresh é `SameSite=Strict` e restrito a `/api/v1/auth`. As demais rotas usam o Bearer token no header, que não é enviado automaticamente pelo navegador, então não há superfície de CSRF.

## Estado das dependências (2026-09-29)

- NuGet: `dotnet list package --vulnerable --include-transitive` sem vulnerabilidades, inclusive nos projetos de teste.
- npm (produção): `npm audit --omit=dev` sem vulnerabilidades.
- npm (desenvolvimento): 1 aviso moderado no Vitest 3.x (path traversal no mock redirect do runner de testes). Não vai para o bundle de produção. A correção exige Vitest 4, que por sua vez exige Node 20+ (ADR-015). **Ação recomendada**: atualizar o Node para 20 LTS ou superior.
