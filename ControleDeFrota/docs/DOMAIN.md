# Domínio

Linguagem ubíqua: o código usa **inglês** e a interface usa **português**. Esta tabela é o dicionário oficial.

| Código | Interface | Significado |
|---|---|---|
| Company | Empresa | Tenant: cada empresa enxerga apenas os próprios dados |
| User | Usuário | Pessoa que acessa o sistema |
| Role | Papel / Perfil de acesso | Conjunto nomeado de permissões |
| Permission | Permissão | Direito atômico de executar uma ação (`vehicles.update`) |
| Driver | Motorista | Condutor cadastrado (não é necessariamente usuário do sistema) |
| Vehicle | Veículo | Unidade motorizada da frota |
| Implement | Implemento | Reboque, semirreboque, tanque, baú, sider, dolly… |
| License (CNH) | CNH | Carteira Nacional de Habilitação |
| EAR | EAR | "Exerce Atividade Remunerada", anotação na CNH |
| Odometer | Hodômetro | Quilometragem atual |
| Hour meter | Horímetro | Horas de uso do motor |

## Mapa de relacionamentos

```
Company ─┬─< User >─< Role >─< Permission
         ├─< Driver
         ├─< Vehicle ···(futuro)··· Maintenance, Fueling, Tires, Documents, Trips, Fines, Accidents, Costs
         └─< Implement ···(futuro)··· VehicleImplementCoupling (vínculo veículo ↔ implemento com vigência)
```

## Company (Empresa)

- O CNPJ é obrigatório, com dígitos verificadores válidos e único entre empresas não excluídas. São aceitos o **CNPJ numérico e o alfanumérico** (novo formato da Receita Federal, vigente desde jul/2026).
- A razão social e o endereço completo (logradouro, número, bairro, cidade, UF, CEP) são obrigatórios.
- Uma empresa **inativa** impede o login e a renovação de sessão de todos os seus usuários.
- Um usuário não pode inativar nem excluir a **própria** empresa.
- Não se exclui uma empresa que ainda tem usuários ativos. O caminho normal é inativar.
- Só usuários com `companies.manage` (super-admin da plataforma) criam, listam todas e excluem empresas. Com `companies.update`, o usuário edita **apenas a própria** empresa.

## User (Usuário)

- O e-mail é o login: **único no sistema inteiro**, armazenado em minúsculas.
- Pertence a exatamente uma empresa (`CompanyId`). Só quem tem `companies.manage` pode criar ou mover usuários para outra empresa.
- Deve ter **pelo menos um papel**. As permissões efetivas são a **união** das permissões de todos os papéis.
- **Anti-escalonamento**: ninguém atribui um papel que contenha permissão que ele próprio não tem.
- Ninguém inativa, exclui ou remove os próprios papéis (evita perder o último acesso administrativo por acidente).
- Status: `Active` ou `Inactive`. Inativar ou excluir revoga imediatamente todas as sessões (refresh tokens).
- Senha: mínimo de 10 e máximo de 128 caracteres, com letras e números, e não pode conter o e-mail. Ver SECURITY.md.
- Bloqueio temporário de 15 minutos após 5 tentativas de login seguidas sem sucesso.

## Role e Permission (Papel e Permissão)

- **Toda decisão de acesso é por permissão, nunca pelo nome do papel.** Não pode existir `if (role == "Administrator")` no código.
- O catálogo de permissões está no código (`Fleet.Domain/Authorization/Permissions.cs`) e é semeado no banco. Criar uma permissão = constante + seed + migration + uso no endpoint.
- Nesta fase, papéis são do **sistema** (`IsSystem = true`) e somente leitura. Papéis personalizados por empresa estão previstos (coluna `Roles.CompanyId`).

### Catálogo de permissões (Fase 1)

| Módulo | Permissões |
|---|---|
| dashboard | `dashboard.view` |
| companies | `companies.view` (própria), `companies.update` (própria), `companies.manage` (todas; plataforma) |
| users | `users.view`, `users.manage` |
| roles | `roles.view` |
| drivers | `drivers.view`, `drivers.create`, `drivers.update`, `drivers.delete` |
| vehicles | `vehicles.view`, `vehicles.create`, `vehicles.update`, `vehicles.delete` |
| implements | `implements.view`, `implements.create`, `implements.update`, `implements.delete` |
| audit | `audit.view` |

### Papéis do sistema

| Papel (Key) | Nome na tela | Permissões |
|---|---|---|
| PlatformAdministrator | Administrador da plataforma | todas |
| Administrator | Administrador | todas, exceto `companies.manage` |
| FleetManager | Gestor de frota | dashboard, `companies.view`, `users.view`, `roles.view`, drivers/vehicles/implements completos, `audit.view` |
| Operations | Operações | dashboard; drivers view/create/update; vehicles e implements view/update |
| Maintenance | Manutenção | dashboard; vehicles e implements view/update |
| Finance | Financeiro | dashboard; drivers, vehicles e implements view |
| Driver | Motorista | nenhuma nesta fase (o app do motorista chega em fases futuras) |
| Viewer | Visualizador | dashboard, `companies.view`, drivers/vehicles/implements view |

## Driver (Motorista)

- CPF com DV válido, único por empresa. O mesmo motorista pode existir em duas empresas diferentes (tenants independentes).
- Número de registro da CNH com 11 dígitos, único por empresa.
- Categoria da CNH: `A, B, C, D, E, AB, AC, AD, AE`.
- Idade entre 18 e 100 anos na data do cadastro.
- **CNH vencida ou a vencer em até 30 dias gera alerta** no dashboard. Nesta fase não bloqueia o cadastro, porque o bloqueio de uso (ex.: não iniciar viagem) pertence ao módulo de Viagens.
- Status: `Active` (Ativo), `OnLeave` (Afastado: férias, licença), `Inactive` (Desligado).
- Endereço opcional. Se informados, o CEP e a UF precisam ser válidos.
- **Fora de escopo na Fase 1**: viagens, multas, sinistros, exames toxicológicos e vínculo motorista-veículo. Tudo isso virá como entidades próprias ligadas a `DriverId`.

## Vehicle (Veículo)

- Placa no padrão antigo (`ABC1234`) ou Mercosul (`ABC1D23`), gravada sem hífen e em maiúsculas.
- **Placa única por empresa entre veículos E implementos** (no Brasil, a placa identifica um único veículo registrado).
- RENAVAM com 11 dígitos e DV válido. Chassi (VIN) com 17 caracteres, sem I, O e Q. Ambos únicos por empresa.
- Ano de fabricação entre 1950 e o ano corrente + 1. Ano do modelo igual ao de fabricação ou ao seguinte.
- Hodômetro, horímetro, capacidade, tara e valor de aquisição ≥ 0. A data de aquisição não pode ser futura.
- Status (ADR-009):
  - `Available` (Disponível): pronto para uso.
  - `OnTrip` (Em viagem): em operação.
  - `UnderMaintenance` (Em manutenção): indisponível para uso.
  - `Inactive` (Inativo): fora da frota (vendido, sinistrado, baixado).
  - **"Ativo" é um conceito derivado** = qualquer status diferente de `Inactive`. É o que aparece como "Veículos ativos" no dashboard.
  - Na Fase 1 o status é alterado manualmente. A partir das Fases 2 e 3, `OnTrip` e `UnderMaintenance` passam a ser **controlados pelos módulos de Viagens e Manutenção**, e a edição manual desses dois valores deve ser restringida.
- Não se exclui um veículo `OnTrip`.
- O hodômetro pode ser corrigido livremente nesta fase. A regra "não retroceder" chega com o histórico de leituras (Fase 2), que também traz a correção auditada.

## Implement (Implemento)

- Tipos: `Trailer` (Reboque), `SemiTrailer` (Semirreboque), `Tanker` (Tanque), `BoxBody` (Baú), `Sider`, `Dolly`, `Other` (Outro).
- Capacidade com unidade explícita (`Kg`, `Liters`, `CubicMeters`), porque um tanque é medido em litros e um baú em kg ou m³.
- As mesmas regras de placa, RENAVAM, chassi e anos do veículo. A placa não pode colidir com a de um veículo.
- Status: `Available`, `InUse` (Em uso / engatado), `UnderMaintenance`, `Inactive`.
- **Preparação**: o vínculo veículo ↔ implemento será uma entidade própria com vigência (`VehicleImplementCoupling`: VehicleId, ImplementId, CoupledAt, UncoupledAt), não uma FK em `Implement`, para preservar o histórico de composições.

## Dashboard

- Indicadores da Fase 1: veículos ativos, motoristas ativos, veículos disponíveis e veículos em manutenção.
- Alertas recentes: CNH vencida e CNH vencendo em até 30 dias.
- Cada indicador é um método independente no `DashboardService`, para que novos KPIs (combustível, custos, documentos) sejam acrescentados sem mexer nos existentes.
