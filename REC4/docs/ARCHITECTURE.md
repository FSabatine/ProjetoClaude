# REC4 — Arquitetura (Fundação: Auth, Usuários, Grupos, Permissões, Pessoas)

Este documento descreve a fundação construída na primeira etapa da reconstrução do REC4 (OutSystems → .NET + React). Cobre apenas o que foi implementado até aqui — Financeiro, Conciliação, Pedidos, Documentos (CT-e/MDF-e) e demais módulos **não foram implementados** e não fazem parte deste documento.

## Camadas (Clean Architecture)

```
REC4.Domain          entidades e enums puros, sem dependências externas
REC4.Application      DTOs, regras de negócio, interfaces (IRec4DbContext, IPasswordHasher, ITokenService)
REC4.Infrastructure    EF Core (SQL Server), hashing de senha, geração de JWT
REC4.Api                controllers, autenticação/autorização ASP.NET Core, middleware de erros
frontend/                React + TypeScript (Vite), consome a API via REST
```

Cada camada só conhece a de dentro dela (Api → Infrastructure → Application → Domain). `IRec4DbContext`, `IPasswordHasher` e `ITokenService` são abstrações definidas em `Application` e implementadas em `Infrastructure` — o mesmo padrão usado desde o módulo de Pessoa.

## Autenticação

- **Login**: `POST /api/auth/login` recebe `{ login, senha }`, valida contra `Usuario.SenhaHash` e retorna um JWT (`REC4.Application/Auth/AuthService.cs`).
- **Hash de senha**: `PasswordHasher<TUser>` do `Microsoft.Extensions.Identity.Core` (PBKDF2-HMACSHA256, o mesmo algoritmo do ASP.NET Core Identity), sem trazer todo o `UserManager`/EF Identity stores — `REC4.Infrastructure/Security/Rec4PasswordHasher.cs`. Nunca armazenamos a senha em texto puro.
- **Token**: JWT assinado (HMAC-SHA256), emitido por `REC4.Infrastructure/Security/JwtTokenService.cs`, validado pelo `Microsoft.AspNetCore.Authentication.JwtBearer` em `Program.cs`. Expira em 60 minutos (configurável em `Jwt:ExpiryMinutes`).
- **Claims do token**: `sub` (Id do usuário), `name`, `login`, `grupo` (**apenas informativo/exibição — nunca usado em decisão de autorização**), e um claim `perm` por permissão efetiva do usuário.
- **Por que embutir as permissões no token em vez de consultar o banco a cada request**: evita round-trip ao banco em toda chamada autenticada. O custo é que, se um admin mudar o grupo/permissões de alguém, o token já emitido continua com o conjunto antigo até expirar — por isso o expiry é curto (60 min). Não há refresh token nesta etapa.

## Autorização (permissões granulares)

**Nunca** verificamos o nome do grupo no código (proibido explicitamente pelo cliente: `if (user.Group == "Financeiro")`). Toda decisão de acesso passa pelo conjunto de permissões do usuário.

- `REC4.Api/Authorization/PermissionRequirement.cs` — um requirement simples carregando a chave da permissão.
- `PermissionAuthorizationHandler.cs` — sucede se o `ClaimsPrincipal` tiver um claim `perm` igual à permissão exigida.
- `PermissionPolicyProvider.cs` — um `IAuthorizationPolicyProvider` que trata **qualquer** nome de policy como uma chave de permissão. Isso significa que `[Authorize(Policy = "Pessoa.Editar")]` funciona para qualquer string do catálogo sem precisar registrar cada uma manualmente em `AddPolicy`.

Resultado prático: **criar um grupo novo ou mudar as permissões de um grupo é só dado** (inserir linhas em `Grupos`/`GrupoPermissoes`), nunca exige alterar código. Adicionar uma **permissão nova** ainda exige código (um atributo `[Authorize(Policy="...")]` em algum endpoint — inerente, já que uma permissão só existe para proteger algo).

## Modelo de dados novo

```
Usuario (Id, Nome, Login [único], SenhaHash, IsActive, GrupoId, PessoaId?, TipoUsuario, LimiteDiasEdicaoFinanceiro?, TemPermissaoPersonalizada, CreatedAt, UpdatedAt)
Grupo (Id, Nome [único])
Permissao (Id, Chave [única, ex. "Pessoa.Editar"], Descricao)
GrupoPermissao (GrupoId, PermissaoId) — chave composta, M:N entre Grupo e Permissao
UsuarioPermissao (UsuarioId, PermissaoId) — override pessoal, só relevante quando TemPermissaoPersonalizada = true
Escritorio (Id, Nome, IsActive, CreatedAt, UpdatedAt)
UsuarioEscritorio (UsuarioId, EscritorioId, IsPrincipal) — M:N, no máximo um IsPrincipal=true por Usuario
UsuarioEmitente (UsuarioId, PessoaId) — M:N, PessoaId aponta para uma Pessoa com o perfil Emitente
```

Usuario → Grupo é uma FK simples (um usuário pertence a **um** grupo, conforme especificado). Diferente de `Pessoa`, `Usuario` **não tem soft delete** — um usuário inativo continua visível nas listagens administrativas via `IsActive`, não desaparece via query filter. `Usuario.PessoaId` é o "vínculo colaborador" pedido pelo cliente — respondeu o TODO que existia aqui antes ("Usuario não tem vínculo com Pessoa").

**`Usuario.TipoUsuario` (Administrador/Gestor/Operador) é só descritivo.** Nunca é lido em nenhuma decisão de autorização — a única fonte de autorização continua sendo o conjunto de permissões efetivas (do Grupo, ou personalizado). Isso é deliberado: o cliente proibiu explicitamente decisões de acesso baseadas em nome de grupo/papel, e `TipoUsuario` é exatamente esse tipo de campo — fácil de ficar tentado a usar num `if`, então documentando aqui como guarda-corrado.

## Permissões personalizadas por usuário

Além do Grupo, um usuário pode ter seu próprio conjunto de permissões, substituindo (não somando) o do grupo:

- `PUT /api/usuarios/{id}/permissoes` body `{ personalizado, permissaoIds[] }` — `personalizado=true` grava `permissaoIds` em `UsuarioPermissao` e liga a flag; `personalizado=false` limpa `UsuarioPermissao` e desliga a flag (restaura para o grupo).
- `REC4.Application/Common/PermissaoEfetivaResolver.cs` é a **única** fonte da regra "de onde vem a permissão efetiva" — usada tanto no login (`AuthService`) quanto no `GET /api/usuarios/{id}`/`GET /api/usuarios/me` (`UsuarioService`), para as duas rotas nunca divergirem.
- No frontend (`UsuarioFormPage.tsx`), cada toggle do grid de permissões já dispara `personalizado=true` imediatamente (mesmo padrão de salvar-ao-marcar já usado nos perfis de Pessoa); um link "Restaurar permissões do grupo" volta para `personalizado=false`. Trocar o Grupo no formulário **não** faz preview ao vivo de outro conjunto de permissões — isso só é recalculado depois que os dados básicos são salvos (corte de escopo deliberado, ver TODO).

## Catálogo inicial de permissões (seed via `HasData`)

Dado literalmente pelo cliente: `Pessoa.Visualizar/Criar/Editar`, `Usuario.Visualizar/Criar/Editar`, `Financeiro.Visualizar/Criar/Editar`, `Conciliacao.Visualizar/Editar`, `Documento.Emitir/Visualizar`. `Escritorio.Visualizar/Criar/Editar` foi adicionado depois, seguindo o mesmo padrão, quando o cadastro de Escritório foi pedido (não fazia parte do catálogo original do cliente).

| Grupo | Permissões |
|---|---|
| Controladoria | todas as 16 (interpretação de "acesso amplo" — ver TODO abaixo) |
| Financeiro | `Financeiro.*`, `Conciliacao.*`, `Pessoa.Visualizar` (só leitura de Pessoa) |
| Logística | `Documento.Emitir`, `Documento.Visualizar`, `Pessoa.Visualizar` (só leitura de Pessoa) |

Ver `REC4.Domain/Seed/PermissoesPadrao.cs` e `GruposPadrao.cs`.

## Superfície da API (Usuários/Auth/Grupos/Permissões/Escritórios)

```
POST   /api/auth/login                            [AllowAnonymous]
GET    /api/usuarios/me                            [Authorize]                          — próprio usuário + permissões efetivas
GET    /api/usuarios, /{id}                        [Authorize(Policy="Usuario.Visualizar")]
POST   /api/usuarios                               [Authorize(Policy="Usuario.Criar")]
PUT    /api/usuarios/{id}                          [Authorize(Policy="Usuario.Editar")]
PUT    /api/usuarios/{id}/permissoes               [Authorize(Policy="Usuario.Editar")]  — personalizar/restaurar
POST   /api/usuarios/{id}/escritorios              [Authorize(Policy="Usuario.Editar")]
PUT    /api/usuarios/{id}/escritorios/{eid}/principal
DELETE /api/usuarios/{id}/escritorios/{eid}
POST   /api/usuarios/{id}/emitentes                [Authorize(Policy="Usuario.Editar")]
DELETE /api/usuarios/{id}/emitentes/{pessoaId}
GET    /api/grupos, /{id}                          [Authorize(Policy="Usuario.Visualizar")]
POST   /api/grupos                                 [Authorize(Policy="Usuario.Criar")]
PUT    /api/grupos/{id}                            [Authorize(Policy="Usuario.Editar")]
GET    /api/permissoes                             [Authorize(Policy="Usuario.Visualizar")]  — leitura, catálogo fixo
GET    /api/escritorios, /{id}                     [Authorize(Policy="Escritorio.Visualizar")]
POST   /api/escritorios                            [Authorize(Policy="Escritorio.Criar")]
PUT    /api/escritorios/{id}                       [Authorize(Policy="Escritorio.Editar")]
```

Grupo e Escritório têm CRUD completo. Permissões continuam somente leitura — o catálogo é fixo, seedado via migration (`PermissoesPadrao`/`GruposPadrao`). Sem edição do próprio perfil por senha/dados administrativos (`PUT /me` não existe — evita qualquer risco de auto-escalação de privilégio antes de existirem regras claras para isso); a troca de senha em si também não existe ainda (botão "Trocar senha" no frontend fica desabilitado).

A tela de Grupo (`GrupoFormPage.tsx`) e a aba "Grupo de permissões" da tela de Usuário (`UsuarioFormPage.tsx`) reusam o mesmo componente `PermissionModuleGrid.tsx`: permissões agrupadas por módulo (prefixo antes do "." na chave, ex. `Pessoa.Editar` → módulo "Pessoa") com um toggle por ação que **realmente existe no catálogo** — não um grid fixo de N colunas, para não sugerir uma permissão (ex. `Excluir`) que ainda não existe em nenhum módulo.

`UsuarioEscritorio` segue exatamente o mesmo padrão "só um principal por dono" que `ContaBancaria`/`Endereco` já usavam para Pessoa — mesmo índice único filtrado + mesmo helper de duas fases (`REC4.Application/Common/PrincipalSwapHelper.cs`, extraído do código que antes vivia só em `PessoaService` porque agora tem um terceiro uso). `UsuarioEmitente` é M:N simples, sem "principal".

`PessoasController`/`PerfisController` (já existentes) têm `[Authorize(Policy="Pessoa.Visualizar"/"Pessoa.Criar"/"Pessoa.Editar")]` conforme a ação. `PessoaService.ListAsync` ganhou um filtro opcional `perfilId` (`GET /api/pessoas?perfilId=4`) — usado pelo frontend para popular os dropdowns "Vínculo Pessoa" (perfil Colaborador) e "Emitentes" (perfil Emitente) sem precisar de um endpoint novo.

## Frontend (React)

SPA separada em `REC4/frontend` (Vite + React + TypeScript), consumindo a API via REST. Token guardado **apenas em memória** (contexto React), nunca em `localStorage`/`sessionStorage` — evita exposição via XSS; o custo aceito é que um F5 força novo login (revisável se isso incomodar demais no uso real). Todo controle de acesso no React (esconder botão, bloquear rota) é **conveniência de UX** — a autorização real e definitiva acontece no backend, nunca confiar apenas na UI escondida.

## Usuários de desenvolvimento (seed automático, só em Development)

`REC4.Infrastructure/Data/DevDataSeeder.cs` cria 3 usuários na primeira vez que a API sobe em Development (idempotente — só roda se a tabela `Usuarios` estiver vazia), um por grupo, para permitir o primeiro login sem precisar de uma tela de administração ainda:

| Login | Senha | Grupo |
|---|---|---|
| admin@rec4.local | Rec4!Admin123 | Controladoria |
| financeiro@rec4.local | Rec4!Financeiro123 | Financeiro |
| logistica@rec4.local | Rec4!Logistica123 | Logística |

**Nunca usar essas credenciais em produção.** A chave de assinatura JWT em `appsettings.Development.json` também é um valor de desenvolvimento gerado localmente — produção precisa de um segredo real vindo de um cofre de segredos (Key Vault/variável de ambiente), nunca commitado.

## TODO — Regras de negócio a confirmar com o cliente

Nada abaixo foi decidido silenciosamente; são pontos onde uma escolha técnica reversível foi feita só para não travar o desenvolvimento. Os itens que o cliente já respondeu numa mensagem posterior (vínculo com Pessoa, permissões personalizadas) foram removidos desta lista.

- **Controladoria = "acesso amplo"**: hoje inclui todas as 16 permissões, inclusive `Usuario.*` e `Escritorio.*`. Confirmar se é isso mesmo ou se administração de usuários deveria ser um nível à parte.
- **Gate da área administrativa de usuários**: hoje reaproveita `Usuario.Visualizar` em vez de uma permissão `Admin.Access` dedicada. Confirmar se é o gate certo.
- **"Movimentos financeiros próprios" da Logística**: não é representável como uma permissão binária hoje (o modelo atual é tudo-ou-nada por módulo). Precisa de permissão com escopo (ex. `Financeiro.VisualizarProprio`) ou filtro por dono na query — decisão adiada para quando o módulo Financeiro for desenhado.
- **`Pessoa.Excluir`**: não foi dado pelo cliente; o soft-delete de Pessoa (`DELETE /api/pessoas/{id}`) está usando `Pessoa.Editar`. Confirmar se deveria ser uma permissão separada.
- **Usuario pertence a um único Grupo** (hoje FK simples, não M:N). Confirmar se isso deve continuar assim — mudar para M:N depois é uma migração com breaking change, não é trivial.
- **Edição do próprio perfil / troca de senha**: não implementada nesta etapa (sem `PUT /api/usuarios/me`, botão "Trocar senha" desabilitado no frontend). Regras de quais campos um usuário pode editar em si mesmo, e o fluxo de troca/reset de senha, ainda não foram definidos.
- **Política de senha**: só um tamanho mínimo (8 caracteres) como placeholder. Sem regra de complexidade, sem fluxo de "esqueci minha senha" ainda.
- **Duração do token (60 min) e refresh token**: valor padrão razoável, não confirmado com o cliente. Se o re-login a cada hora incomodar no uso real, o próximo passo natural é um fluxo de refresh token.
- **"Usuário REC" (mapeamento para o REC1 legado)**: não há fonte de dados do REC1 disponível. O campo aparece na tela como um dropdown desabilitado ("Não mapeado") e **não é persistido no backend** — mesmo tratamento dado a "Sincronizar REC1"/"Tipo Contribuinte" na tela de Pessoa. Quando houver integração real com o REC1, isso vira um campo de verdade.
- **`Escritorio` — campos além de Nome**: o cliente só mostrou o nome na tela de referência; o cadastro ficou deliberadamente mínimo (Nome + Ativo). Se o Escritório precisar de endereço, CNPJ etc., isso é um próximo passo, não foi inventado agora.
- **"Adicionar todos escritórios"**: implementado como ação em massa (liga a todos os escritórios que existem *agora*), não como uma flag "sempre todos, incluindo futuros". Se o comportamento pretendido for o segundo, é uma mudança de modelo (um flag em `Usuario`, não uma linha por escritório).
- **Troca de Grupo no formulário de Usuário não faz preview do novo conjunto de permissões**: o grid mostra as permissões efetivas *salvas* (do grupo atual ou personalizadas); escolher outro Grupo no dropdown só tem efeito depois de salvar os dados básicos. A tela de referência do cliente sugeria algo mais parecido com um preview ao vivo por grupo (dropdown "Selecione o Grupo de permissões" + "Exibir personalização") — simplificado deliberadamente porque o pedido em texto do cliente descrevia o fluxo mais simples implementado aqui. Confirmar se o preview ao vivo é realmente necessário.
