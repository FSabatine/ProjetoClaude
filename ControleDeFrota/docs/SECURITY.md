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

### Autorização na Fase 4

- Permissões novas: `fuel.view`, `fuel.create`, `fuel.correct`, `fuel.cancel`, `fuel.reviewanomalies`, `fuel.managestations`, `fuel.configure` e `fuel.viewcosts` (matriz por papel em DOMAIN.md). O papel Motorista continua sem permissões.
- **Custos de combustível são dado sensível** (seção 39): `fuel.viewcosts` é checada **no serviço**, por registro — sem ela, preço, total, custo do trecho, custo/km e todos os totais em R$ do painel e da aba do veículo voltam `null`; relatórios de custos e de preços exigem a permissão na rota; filtrar a lista por preço sem ela é 403 (o filtro revelaria valores). O **autor** de um abastecimento vê os valores do próprio registro.
- Comprovantes de abastecimento (`FileOwnerType.Fueling`): abrir exige `fuel.view` **e** (`fuel.viewcosts` ou ser o autor do abastecimento) — o cupom mostra o valor pago; remover exige `fuel.correct`; anexar depois exige ser o autor ou ter `fuel.correct`.
- Eventos da linha do tempo e mensagens de alerta de preço não carregam valores em R$ (são exibidos a quem vê o veículo). O preço de referência de posto (informação de mercado) pode aparecer no evento `FuelPriceChanged`, que não pertence a veículo.
- Regras que dependem do alvo, no serviço: corrigir o km já aplicado exige `mileage.manage`; revisar um abastecimento com hodômetro pendente exige `mileage.manage` (a revisão aprova a leitura).
- Isolamento de tenant: todas as tabelas novas são `ITenantScoped` (filtro global); id de outra empresa responde 404 (`FuelApiTests.OtherCompany_Gets404`, testes de serviço por entidade). Nenhum `IgnoreQueryFilters` novo fora do seed de desenvolvimento e da checagem de catálogo padrão (que filtra o `CompanyId` explicitamente, como o de tipos de documento).
- Auditoria automática (`IAuditable`): `Fueling`, `FuelStation`, `FuelType`, `FuelPrice`, `FuelSettings`. Correções têm ainda o histórico próprio com motivo (`FuelingCorrection`); alertas guardam quem revisou.

### Autorização na Fase 5

- Permissões novas `tires.*` (12; matriz em DOMAIN.md). Motorista sem acesso; Operações só vê e inspeciona.
- Regras que dependem do alvo, checadas **no serviço**: remover com destino conserto/recapagem/baixa exige `tires.repair`/`tires.retread`/`tires.dispose`; substituir e transferir exigem também `tires.remove`; enviar/concluir/cancelar serviço exige a permissão do **tipo** (conserto ou recapagem); custos manuais exigem `tires.edit` + `tires.viewcosts`.
- **Valores em R$** (compra, serviços, ciclo de vida, custo/km, ranking do painel, relatório de custos): só com `tires.viewcosts`; sem ela os campos voltam `null`. Informar qualquer valor sem a permissão é 403; na edição, o valor de compra de quem não vê custos é ignorado (não é apagado). Eventos e resumos não levam R$.
- Arquivos: fotos de inspeção (`TireInspection`) abrem com `tires.view`; nota, garantia e documentos de serviço/baixa (`Tire`, `TireServiceOrder`) exigem `tires.view` **e** (`tires.viewcosts` ou ser quem enviou) — mostram o valor pago. Remover: `tires.edit` (pneu/inspeção) ou `tires.repair|retread` (serviço). Upload liberado para quem opera pneus; o vínculo continua só pelo próprio autor (`AttachAsync`).
- Isolamento: todas as tabelas novas `ITenantScoped`; veículo, implemento, pneu, oficina ou ocorrência de outra empresa → 404 (testes de serviço e `TireApiTests.OtherCompany_Gets404`). Nenhum `IgnoreQueryFilters` novo fora do seed de desenvolvimento e da checagem de configurações padrão (filtra o `CompanyId` explicitamente).
- **Concorrência** (seção 52): o banco garante "um pneu, uma posição" (índices únicos filtrados) e o token `Tires.Version` impede que duas operações sobre o mesmo pneu se sobreponham; ambos viram 409 sem detalhe técnico.
- Auditoria automática (`IAuditable`): `Tire`, `TireModel`, `TireLayout`, `TireInstallation`, `TireRotation`, `TireInspection`, `TireServiceOrder`, `TireCost`, `TireSettings` (todas liberadas em `/audit/{entidade}/{id}`). A linha do tempo do pneu (eventos) responde "o que aconteceu"; a auditoria, "quem mudou qual campo".

### Autorização na Fase 6

- Permissões novas `finance.*` (10; matriz em DOMAIN.md). Operações/Manutenção/Motorista sem acesso — financeiro é o Gestor de frota e o papel Financeiro.
- **`finance.viewcosts` é o portão de todo R$ do módulo** — checada no serviço, por registro, igual ao padrão de `fuel.viewcosts`/`maintenance.viewcosts`/`tires.viewcosts`: sem ela, `Amount`/`PaidAmount` de uma despesa voltam `null` (exceto para quem registrou a despesa, que sempre vê o que digitou), e os relatórios/painel/custo-por-km/TCO exigem a permissão na rota.
- **Total combinado entre módulos**: o painel e o custo de um veículo misturam combustível, manutenção, pneus e despesas manuais. Cada fatia só entra na soma se o usuário tiver a permissão de custo **daquela fonte específica** (`fuel.viewcosts`/`maintenance.viewcosts`/`tires.viewcosts`) **e** `finance.viewcosts` — ter só `finance.viewcosts` não libera, por exemplo, o custo de combustível de outro módulo. Faltando qualquer uma, a fatia correspondente zera e a resposta marca `IsPartial = true`: a tela deve avisar "totais parciais", nunca mostrar um total menor como se fosse completo. Isto é checado inteiramente no `CostAggregationService`, nunca no atributo de rota.
- Categoria de sistema (`IsSystemCategory`): não pode ser excluída nem inativada por ninguém, nem com `finance.managecategories` — o valor dela vem de outro módulo, então inativá-la escondê-la do financeiro sem desligar a origem seria inconsistente.
- Anexos de despesa (`FileOwnerType.Expense`): abrir exige `finance.view` **e** (`finance.viewcosts` ou ser quem enviou) — mostram o valor pago (nota, recibo). Upload liberado para quem cria/edita despesas; o vínculo continua só pelo próprio autor (`AttachAsync`).
- Isolamento de tenant: todas as tabelas novas são `ITenantScoped`; despesa, categoria, centro de custo, recorrente ou orçamento de outra empresa → 404 (testes de serviço, `ExpenseService_PermissionAndTenantTests.Get_RecordFromAnotherCompany_ThrowsNotFound`). Nenhum `IgnoreQueryFilters` novo fora da checagem do catálogo padrão (filtra `CompanyId` explicitamente) e do job de geração de recorrentes (sem usuário/tenant, como o `DocumentExpirationScanner`).
- Auditoria automática (`IAuditable`): `Expense`, `ExpenseCategory`, `CostCenter`, `RecurringExpense`, `Budget` (liberadas em `/audit/{entidade}/{id}`).

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

- Toda criação, alteração e exclusão (soft delete) de Company, User, Driver, Vehicle e Implement (e, na Fase 2, também de VehicleAssignment, OdometerReading — inclusive revisão e correção —, DocumentType, Document, StoredFile, ChecklistTemplate, ChecklistExecution e Occurrence, inclusive a mudança de situação; e, na Fase 3, Workshop, MaintenancePlan, HourMeterReading, MaintenanceRequest e WorkOrder, inclusive as transições de situação; e, na Fase 6, Expense, ExpenseCategory, CostCenter, RecurringExpense e Budget) grava `AuditLogs` com o usuário, a data, os campos e os valores antigo e novo. Ver DATABASE.md.
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

### Autorização na fase final — alertas e automação (ADR-045/046)
- `alerts.view` abre a central, o sino e os grupos de alertas do "Requer atenção"; **cada alerta** ainda é filtrado pelo seu público (permissões do módulo de origem). Fora do público = 404, igual a registro de outra empresa. Alertas com R$ exigem todas as `*.viewcosts` (ADR-042); notificações nunca carregam R$.
- `alerts.manage` para mudar a situação; `automation.manage` para regras e "Verificar agora".
- Notificações: cada usuário só lê/marca as suas (`UserId` do token); id de outra pessoa = 404.
- Destinatário específico de uma regra precisa ser usuário ativo **da mesma empresa** (validado no serviço; `Users` não tem filtro de tenant).
- Jobs agem como "sistema" de uma empresa por vez (`SystemExecutionContext`), em escopo próprio; requisições nunca ativam esse modo.
- Links de notificação são rotas internas geradas pelo servidor (nunca URL externa).

### Fase final, etapa B — análises, exportação e busca
- Métricas cruzadas, comparação e destaques: cada número sob a permissão do seu módulo; dinheiro sob ADR-042 (o "E" das `*.viewcosts`), `null` quando oculto — exportações herdam o mesmo (célula vazia).
- Exportação usa os mesmos endpoints da tela (sem rota paralela que pudesse esquecer um filtro); CSV protegido contra injeção de fórmula; dependências de PDF fixadas em versão sem vulnerabilidades conhecidas.
- Busca global: cada tipo com a permissão da sua lista; documentos de motorista exigem `drivers.view`; despesas sem valor; alertas pelo público.
- **Correção**: a linha do tempo de veículo/motorista agora filtra eventos pelo módulo (ADR-049); antes, descrições de despesas, abastecimentos etc. apareciam para quem só via veículos.
