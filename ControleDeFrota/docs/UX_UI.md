# UX/UI

Princípio: **UX > quantidade de funcionalidades**. Uma tela simples e impecável vale mais que três telas confusas.

## Stack de interface

- **Mantine 7** (componentes, tema, formulários, notificações e modais), com acessibilidade de base (foco, ARIA, teclado) e visual moderno (ADR-010).
- **TanStack Query**: estados de carregamento, erro e cache padronizados em toda chamada.
- **Tabler Icons**: um único conjunto de ícones.
- **react-imask**: máscaras de CPF, CNPJ, CEP, telefone e placa.

## Identidade visual

- Tema em `frontend/src/theme.ts`, a **única** fonte de cores, raios e fontes. Nenhuma cor hex solta nos componentes.
- A cor primária é o vermelho da identidade Rodoxisto (`brand`), usada **com parcimônia**: ação principal, item ativo do menu e foco. O restante da interface é neutro, em cinzas frios.
- Tipografia Inter (fallback: fontes do sistema). Escala: título de página `h2`, seção `h4`, corpo 14px.
- Cantos arredondados `md` e sombras discretas (`xs`/`sm`). Espaçamento em múltiplos de 4px.
- Tema claro e escuro (Mantine `colorScheme`), respeitando a preferência do sistema operacional e alternável no cabeçalho.

## Hierarquia visual

- Cada página segue a mesma estrutura: **título + descrição curta + ação principal à direita** (`PageHeader`).
- Só existe **uma** ação primária (botão preenchido) por tela. As secundárias ficam em `default`/`subtle` e as destrutivas em vermelho, dentro de menus ou confirmações.
- Em listas, o identificador forte (placa, nome) vem em negrito e os dados auxiliares em `dimmed`.
- O status é sempre um `Badge` colorido e consistente: verde = disponível/ativo, azul = em viagem/em uso, laranja = manutenção/afastado, cinza = inativo, vermelho = alerta.

## Feedback

- Salvar: o botão entra em `loading` ("Salvando…") e fica desabilitado contra duplo clique. No sucesso, aparece uma notificação verde ("Veículo salvo com sucesso.") e o sistema volta à lista.
- Erro de API: notificação vermelha com **o que aconteceu + como resolver**, usando a mensagem do backend (`ProblemDetails.title`/`detail`). Erros de campo (400) são exibidos **no próprio campo**.
- Nunca mostrar "Error 500" ou uma stack trace. A mensagem genérica é: "Não foi possível concluir a operação. Tente novamente em instantes. Se persistir, informe o código {traceId} ao suporte."

## Estados obrigatórios de toda tela de dados

| Estado | Padrão |
|---|---|
| Carregando | `Skeleton` no formato do conteúdo (linhas de tabela, cards). Nunca uma tela branca |
| Vazio (sem cadastro) | ícone + título + explicação + botão da ação principal ("Nenhum veículo cadastrado. Cadastre o primeiro veículo para começar a gerenciar sua frota. [+ Novo veículo]") |
| Vazio (filtro sem resultado) | "Nenhum resultado para os filtros aplicados." + botão "Limpar filtros" |
| Erro | mensagem amigável + botão "Tentar novamente" |
| Sem permissão | página 403 explicando que o acesso depende do administrador |

## Formulários

- Campos **agrupados em seções** com título (Identificação, Características, Controle, Aquisição, Observações).
- Obrigatórios marcados com `*` (Mantine `withAsterisk`). Opcionais sem marca.
- Validação **inline** ao sair do campo e no envio. O foco vai para o primeiro campo inválido.
- Máscaras de CPF `000.000.000-00`, CNPJ `00.000.000/0000-00` (aceita letras no formato alfanumérico), CEP `00000-000`, telefone `(00) 00000-0000` e placa `AAA-0A00` (convertida em maiúsculas).
- `autocomplete` HTML correto (`email`, `tel`, `postal-code`, `address-line1`, `bday`), `inputMode="numeric"` em campos numéricos (teclado numérico no celular).
- Defaults úteis: status `Disponível`/`Ativo`, ano de fabricação = ano corrente, UF da empresa.
- Um formulário com alterações não salvas pede confirmação antes de sair.
- Textos de ajuda (`description`) onde há dúvida real (ex.: "EAR: marque se a CNH tem a observação 'Exerce Atividade Remunerada'").

## Listas

- **Busca** textual com debounce de 300ms, **filtros** (status, tipo), **ordenação** clicando no cabeçalho, **paginação** (20 por página) e total de registros.
- Os filtros ficam na URL (`?search=&status=`): o usuário pode compartilhar o link e o botão voltar preserva o estado.
- Ações por linha num menu `⋯` (Editar, Excluir). A linha inteira é clicável para abrir a edição (menos cliques).
- Exclusão sempre com **modal de confirmação** que nomeia o registro ("Excluir o veículo ABC-1D23? Esta ação pode ser revertida apenas pelo suporte.").

## Responsividade

| Largura | Comportamento |
|---|---|
| ≥ 1200px (desktop) | menu lateral fixo, tabelas completas |
| 768–1199px (laptop/tablet) | menu lateral recolhível, colunas secundárias ocultas |
| < 768px (celular) | menu em gaveta (burger), **tabelas viram cards**, formulários em uma coluna, botões de ação com largura total |

Alvos de toque de no mínimo 40px. O motorista vai usar o celular nas fases futuras, então nenhuma tela assume mouse.

## Acessibilidade (WCAG 2.1 AA como meta)

- Contraste mínimo de 4.5:1 no texto.
- Todo campo tem `label` visível (placeholder não substitui label).
- A navegação por teclado é completa, com foco visível.
- Ícones sem texto têm `aria-label`.
- O status não depende só da cor: o badge sempre tem texto.

## Textos (microcopy)

- Português do Brasil, tom profissional e direto, na segunda pessoa ("Cadastre", "Verifique").
- Botões com verbo + objeto ("Salvar veículo", "Novo motorista"), nunca apenas "OK".
- Datas em `dd/mm/aaaa`, números em `pt-BR` (`1.234,56`) e moeda em `R$`.
