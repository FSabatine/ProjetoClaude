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
- O status é sempre um `Badge` colorido **com texto** e consistente em todo o sistema (`STATUS_COLOR` em `theme.ts`):
  - verde = disponível, ativo, válido, conforme, aprovado, resolvida;
  - ciano = alocado;
  - azul = em viagem, em uso, em análise;
  - amarelo/laranja = vencendo, em manutenção, afastado, gravidade média/alta, leitura em revisão;
  - vermelho = vencido, indisponível, não conforme, ocorrência aberta, gravidade crítica;
  - cinza = inativo, sem validade, substituído, cancelada, não se aplica.
- Os rótulos e cores dos enums da Fase 2 ficam em `features/operations/labels.ts`.

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


## Central de Ajuda (manual do usuário)

- Ícone `?` (`IconHelpCircle`) no cabeçalho, ao lado do seletor de tema — mesmo `Group` de ações, sempre visível, nunca navega para outra rota.
- Abre um `Drawer` à direita (`size="md"` no desktop, `size="100%"` no celular, mesmo ponto de quebra de 768px das listas): busca sempre visível no topo, depois a tela atual (início, categoria ou artigo), com navegação em pilha interna (botão "Voltar") — fechar e reabrir sempre volta para o início.
- Categorias aparecem **na ordem mais relevante para a tela atual**: a aba aberta no hub do veículo/motorista prioriza a categoria correspondente (`features/help/context.ts`).
- Um artigo é texto curto, nunca um bloco só de parágrafo: resumo, "por que importa", passo a passo numerado, um exemplo em bloco destacado, notas importantes e "Relacionados" (que navegam sem fechar a ajuda).
- Conteúdo só do que já existe no sistema — nunca uma funcionalidade planejada. Cada artigo tem `requiredPermission` opcional (o mesmo `PERMISSIONS` do app): por padrão todo artigo é visível a qualquer usuário autenticado; só os de Empresas/Usuários/Papéis exigem a permissão de visualização do módulo, por relevância (não há segredo nenhum no manual).
- **Diferente de `docs/`**: o manual é em pt-BR, linguagem de negócio, sem nenhum detalhe de implementação — nunca um link direto para a documentação técnica.

## Fase 2 — padrões operacionais

### Página "hub" do registro (veículo e motorista)
- `/veiculos/:id` e `/motoristas/:id` são o **centro** do registro. A edição do cadastro foi para `/…/:id/editar`, e salvar volta ao hub.
- Estrutura: `PageHeader` (título, breadcrumb, Histórico de auditoria, Editar e **uma** ação primária, como "Realizar checklist") → **cabeçalho operacional** (situação, placa, hodômetro com a data e motorista atual) → **abas** (`DetailTabs`).
- Abas do veículo: Visão geral (alertas do veículo + dados gerais), Motorista, Quilometragem, Documentos, Checklists, Ocorrências (com contador das abertas), **Manutenção** (Fase 3: próximas, ordens de serviço, problemas recorrentes) e Histórico. Abas do motorista: Visão geral, Veículos, Documentos, Checklists, Ocorrências e Histórico. Cada aba só aparece com a permissão do módulo.
- A aba ativa fica na URL (`?aba=documentos`): os alertas do dashboard levam direto à seção certa e o botão voltar funciona.
- Não há abas de módulos futuros (combustível, pneus, viagens…): elas entram quando existirem.

### Confirmações e ações de risco
- A troca de motorista pede confirmação com a frase do servidor ("o veículo está com Maria…"). Nada é encerrado em silêncio.
- Encerrar uma alocação, resolver ou cancelar uma ocorrência e excluir um documento pedem confirmação. Encerrar uma ocorrência avisa que a ação é final.
- As ações possíveis de uma ocorrência vêm da API (`nextStatuses`): a tela nunca oferece uma transição proibida.

### Feedback imediato (sem esperar o servidor)
- No registro de hodômetro e no checklist, a tela avisa **enquanto se digita**: "menor que a última leitura", "+470 km desde a última leitura" ou "aumento suspeito: ficará em revisão" (`lib/mileage.ts`, espelho da regra do servidor).
- Leitura aceita como suspeita: notificação explicando que um gestor precisa confirmar.
- O seletor de motorista mostra quem já tem veículo e desabilita quem está afastado, desligado ou com a CNH vencida, com o motivo no próprio item.

### Checklist no celular (prioridade da fase)
- Fluxo: abrir o veículo → "Realizar checklist" → responder → fotografar o problema → enviar. Com um único modelo ativo, ele é escolhido sozinho.
- Cada item tem **três botões grandes** (48px de altura, largura total dividida em três): Conforme, Não conforme e N/A. Não há texto obrigatório para responder.
- **"Marcar restantes como conforme"** responde de uma vez o que ficou sem resposta; a pessoa só toca nos itens com problema.
- Ao marcar "Não conforme", abrem a gravidade (chips, já com o padrão do item), a descrição opcional e o botão **Tirar foto**, que abre a câmera traseira (`capture="environment"`). "(obrigatória)" aparece quando o modelo exige foto.
- As fotos são reduzidas no aparelho para no máximo 1600px antes do envio (`lib/images.ts`), o que economiza dados móveis.
- Progresso visível ("8 de 11 respondidos") e **barra de envio fixa** no rodapé, com a contagem de não conformes.
- Se o envio falhar, os erros aparecem no item (borda vermelha) e a tela rola até o primeiro.
- Tela final: aprovado, ou "N ocorrência(s) aberta(s)", com os atalhos "Ver checklist", "Novo checklist" e "Voltar ao veículo".
- Sair com respostas preenchidas pede confirmação (guarda de alterações não salvas).

### Anexos
- `UploadButton` ("Anexar arquivo" ou "Tirar foto") envia na hora e mostra o arquivo; ele só é vinculado ao salvar.
- As fotos aparecem como miniaturas (toque para ampliar) e os PDFs como link que abre em nova aba. Como a API exige o token, o arquivo é baixado como blob.

### Listas operacionais e filtros
- Os filtros de todas as listas ficam na URL e são aplicados no servidor. Cada KPI do dashboard é um link para a lista já filtrada.
- Veículos: situação operacional, tipo, motorista, faixa de km e "sem leitura há 7 dias"; a coluna Motorista foi incluída.
- Motoristas: situação, alerta de CNH, categoria e com/sem veículo; a coluna Veículo foi incluída. A busca aceita a placa do veículo atual.
- Documentos: situação, dono, tipo e período de vencimento.
- Ocorrências: situação, gravidade, tipo, veículo, motorista e período.
- Checklists: resultado, modelo, veículo e período, com os **pendentes de hoje** em cards no topo e o botão "Iniciar".
- Veículo e motorista são escolhidos com `VehiclePicker`/`DriverPicker`, com busca no servidor (a frota pode ter milhares de registros).

### Dashboard
- Três blocos, sem gráficos: **Frota** (6 cards por situação operacional), **Atenção** (documentos vencidos e vencendo, checklists pendentes, ocorrências abertas e sem leitura recente de km) e **Quilometragem do mês** (total, média e maior hodômetro). Abaixo, **Alertas**, com os críticos primeiro.
- Cards sem permissão do módulo não aparecem (não se mostra "0" enganoso).

### Erros inesperados
- Uma falha de renderização mostra "Algo deu errado nesta tela" com o botão "Recarregar a página" (`UnexpectedErrorPage` como `errorElement` da rota raiz), nunca a página técnica do roteador.

## Fase 4 — combustível

### Registro de abastecimento (uso frequente, mobile-first)
- Ordem dos campos pela frequência de uso (seção 46): **veículo → hodômetro → combustível → quantidade → preço → total**; depois posto, motorista, data, tanque cheio, pagamento, cupom e foto. Inputs `size="md"` (≥ 40px), teclado numérico (`inputMode`), barra de salvar fixa (`FormActions`).
- **Reaproveitar sem induzir erro** (seção 27): ao escolher o veículo, sugere o motorista alocado, o último combustível e o último posto (com a legenda "confira"); mostra o último hodômetro como referência. **Hodômetro e preço nunca vêm preenchidos** — são o fato sendo registrado. O preço conhecido do posto aparece como dica com o link "usar".
- Feedback antes de salvar, espelhando o Domain (`lib/fuel.ts`, `lib/mileage.ts`): total calculado ao vivo, aviso de km menor que a última leitura (será recusado), de salto suspeito (será salvo para revisão) e de quantidade acima do tanque (será salvo para revisão).
- Salvou com alerta? Notificação laranja explicando que o registro foi salvo e ficou para revisão, e a tela do abastecimento abre com o motivo.
- A mesma tela serve para a **correção** (`/abastecimentos/:id/corrigir`): veículo travado, data travada quando o abastecimento já gerou leitura de hodômetro, motivo obrigatório.

### Alertas e revisão (linguagem neutra)
- Situação "**Requer revisão**" (laranja, com texto). Nunca "suspeito de fraude", "erro" ou ranking de motorista (seção 48). O detalhe explica que "um alerta não significa erro nem irregularidade" e oferece as três saídas: Revisar (está certo), Corrigir, Cancelar.
- Ações (`actions` da API) decidem quais botões aparecem. Cancelar e revisar pedem texto num modal.

### Números, gráficos e ajuda contextual
- Consumo sempre em **km/unidade** (km/L); L/100 km só no detalhe do abastecimento. Desvio com sinal ("-26%"), nunca só por cor.
- Ausência de número é explicada (`CONSUMPTION_RESULT`): "Primeiro tanque cheio…", "Abastecimento parcial…", "Não calculado…". Períodos sem trecho medido mostram "—", nunca zero ou estimativa.
- `[?]` (`InfoHint`) ao lado de consumo e custo/km, com a explicação em linguagem simples (seção 49).
- Gráficos (`components/ColumnChart`, regras do skill de dataviz): uma série, uma cor validada (indigo 6 no claro / 5 no escuro), colunas ≤ 24px com topo arredondado de 4px, linhas de grade finas, sem legenda (o título nomeia a série), um único rótulo direto no maior valor, tooltip por coluna no hover **e no foco do teclado**, e o botão "**Ver tabela**" com todos os números. Nenhum gráfico de dois eixos.
- Comparações entre veículos sempre nomeiam a medida e o período ("Custo total no período (01/09 a 30/09)").
- Valores em R$ somem (colunas, cards e gráficos) para quem não tem `fuel.viewcosts`; quem registrou vê o próprio valor.

### Painel e relatórios
- Painel de Combustível responde, nessa ordem: o que aconteceu (KPIs do período), o que requer atenção (fila de revisão), o que custa (gasto mensal, maior custo) e o consumo (mensal, abaixo do esperado).
- Período na URL (`?de=&ate=`) com atalhos de 30/90/365 dias; o link "Relatórios de combustível" leva o mesmo período.
- Relatórios em abas na URL (`?relatorio=`), ordenação no cabeçalho e paginação no servidor.

### Revisão desta fase
- Revisão de UX e responsividade feita no código (cada tela com `PageHeader`, estados de carregando/vazio/erro, cards no celular via `DataTable`, `SimpleGrid` responsivo). **Sem revisão visual automatizada** (o navegador headless travou a máquina na Fase 1): conferir manualmente em 375px, tablet e desktop, nos temas claro e escuro.
