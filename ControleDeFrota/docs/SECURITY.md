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
  - **Risco identificado na Fase 2**: como o refresh divide o limite com o login, vários recarregamentos seguidos (ou vários usuários atrás do mesmo NAT) podem receber 429 e perder a sessão. Recomendação: uma política separada e mais generosa para o refresh, que usa um token aleatório de 64 bytes e não é atacável por força bruta (ver DECISIONS, pontos em aberto).
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

### Autorização na Fase 2

- Permissões novas: `assignments.*`, `mileage.record/manage`, `documents.view/manage/delete`, `checklists.view/execute`, `occurrences.view/create/manage` e `operations.configure` (matriz por papel em DOMAIN.md).
- Todo endpoint novo tem `[HasPermission]`. As regras que dependem do alvo são checadas **no serviço**, não na rota:
  - a correção de hodômetro exige `mileage.manage`, mesmo que a rota aceite `mileage.record`;
  - baixar um arquivo exige a permissão de visualização do registro dono (documento, ocorrência ou checklist); um arquivo ainda sem dono só é visível para quem o enviou;
  - ao vincular arquivos a um registro, só são aceitos arquivos **enviados pelo próprio usuário e ainda sem dono**: conhecer o id de um arquivo não dá acesso a ele;
  - ninguém remove as fotos de um checklist já enviado.
- O job de vencimentos roda sem usuário e ignora os filtros de tenant de propósito: ele só lê documentos e grava eventos com o `CompanyId` do próprio documento (revisado e comentado no código).
- Os testes de `OperationsApiTests` cobrem o 403 por papel (Visualizador, Manutenção, Operações, Motorista), o 404 entre empresas para documentos e arquivos e o 403 na correção de hodômetro feita por Operações.

### Autorização na Fase 3

- Permissões novas: `maintenance.view`, `maintenance.createrequest`, `maintenance.manageplans`, `maintenance.manageworkorders`, `maintenance.manageworkshops` e `maintenance.viewcosts` (matriz por papel em DOMAIN.md). O papel **Manutenção**, que na Fase 2 só via veículos e ocorrências, ganhou o conjunto completo; **Operações** ganhou só `view` e `createrequest` (relatar um problema, não geri-lo); **Financeiro** ganhou `view` e `viewcosts`.
- `maintenance.viewcosts` é checada **no serviço**, não só na rota: sem ela, `WorkOrderService` zera `partsCost/laborCost/otherCost/totalCost` e o custo unitário de peças e mão de obra na resposta, em vez de recusar o acesso à ordem de serviço inteira — o técnico de campo vê o que precisa fazer sem ver quanto custa.
- Checada no serviço (não dá para expressar como atributo): o início de uma ordem de serviço recusa um veículo em viagem ou inativo; a conclusão exige todos os itens obrigatórios resolvidos.
- `MaintenanceApiTests` cobre o 403 por papel (Operações não aprova nem gerencia oficinas) e o 404 entre empresas para ordens de serviço.

## Upload de arquivos

- O tipo é detectado pelos **magic bytes** (PDF, JPEG, PNG). A extensão e o `Content-Type` enviados pelo cliente são ignorados: um executável renomeado para `.pdf` é recusado.
- O limite de 10 MB é aplicado duas vezes: `RequestSizeLimit` no endpoint e cópia limitada no serviço.
- O nome original é sanitizado (sem caminho nem caracteres reservados) e usado **só para exibição**. A chave no storage é gerada pelo servidor, e o `LocalFileStorage` recusa qualquer caminho fora da raiz (defesa contra *path traversal*).
- Os arquivos ficam fora do web root e só são servidos pela API autenticada, com o `Content-Type` validado, `X-Content-Type-Options: nosniff` (middleware) e `Cache-Control: private, no-store`.
- Pendências: antivírus no upload e limpeza de arquivos órfãos.

## Proteção de dados (LGPD)

- CPF, RG, CNH, telefone, e-mail e endereço de motoristas são **dados pessoais**. O acesso exige `drivers.view`, e toda alteração é auditada.
- Logs de aplicação **não** registram dados pessoais nem corpos de requisição.
- Minimização na Fase 2: no contexto do veículo (lista, hub, histórico) aparece só o **nome** do motorista alocado, para quem tem `vehicles.view`, porque ele é necessário para operar. CPF, CNH e contatos continuam exigindo `drivers.view`. Nos alertas de documento do dashboard, o nome do motorista só aparece com `drivers.view`. O payload JSON dos eventos operacionais leva ids, não dados pessoais.
- Documentos de motorista (exames, ASO) podem conter dados de saúde, que são **dados sensíveis** (LGPD, art. 11). O acesso aos arquivos exige `documents.view`. Pendência: definir a base legal e a retenção com o jurídico.
- Em produção, a connection string e a chave JWT vêm de variáveis de ambiente ou de um cofre (Azure Key Vault ou equivalente), **nunca do repositório**. `appsettings.Development.json` contém apenas segredos de desenvolvimento.
- HTTPS obrigatório fora de Development (HSTS + redirecionamento).
- Pendências futuras: base legal documentada, política de retenção e anonimização de motoristas desligados, e exportação dos dados do titular.

## Auditoria

- Toda criação, alteração e exclusão (soft delete) de Company, User, Driver, Vehicle e Implement (e, na Fase 2, também de VehicleAssignment, OdometerReading — inclusive revisão e correção —, DocumentType, Document, StoredFile, ChecklistTemplate, ChecklistExecution e Occurrence, inclusive a mudança de situação; e, na Fase 3, Workshop, MaintenancePlan, HourMeterReading, MaintenanceRequest e WorkOrder, inclusive as transições de situação) grava `AuditLogs` com o usuário, a data, os campos e os valores antigo e novo. Ver DATABASE.md.
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
