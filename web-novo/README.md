# Previsio — painel web (Angular 21)

Painel do Previsio com o visual do **Material Dashboard PRO** (Creative Tim, v2.9.0), reconstruído em Angular 21 com componentes standalone.

## Requisitos

- **Node 22.13 ou superior** (ou 20.19+, ou 24+). Testado no Node 22.13.0.
- Ficou no Angular 21 porque o Angular 22 exige Node 22.22.3 ou mais novo.

## Rodar

```bash
npm install
npm start
```

Acesse `http://localhost:4200/#/pages/painel` (sem login, o painel abre a tela de entrada).

A API do Previsio precisa estar rodando. A URL dela fica em `src/app/core/previsio.config.ts`.
Na API, `Front:Url` precisa apontar para este endereço (`http://localhost:4200/#`), senão o navegador barra as chamadas (CORS).

## Login

O painel entra com e-mail e senha em `POST /api/auth/login` e guarda o token JWT no `localStorage`
(o login sobrevive à ida e volta do OAuth do Bling e a novas abas).

| Peça | Arquivo | O que faz |
|---|---|---|
| Sessão | `core/sessao.service.ts` | Login, sair, token, usuário logado e cliente exibido |
| Interceptor | `core/autenticacao.interceptor.ts` | Envia `Authorization: Bearer` às chamadas da API; um `401` encerra o login |
| Guardas | `core/autenticacao.guards.ts` | Telas internas exigem login; Clientes exige admin; a tela de login leva quem já entrou ao painel |
| Clientes | `core/clientes.service.ts` | Lista de clientes do admin (barra do topo e tela Clientes) e cadastro |

- **Cliente exibido:** o usuário de cliente vê só o próprio. O admin escolhe o cliente numa lista na barra do topo
  (a escolha fica guardada no navegador).
- **Clientes (só admin):** cadastro do cliente (a rede) com a caixa de e-mail dedicada; em **Editar**,
  correção dos dados (senha em branco mantém a atual), ativar/desativar e a lista de empresas dele.
  Sem nenhum cliente, as outras telas ficam escondidas e o painel mostra o botão **Cadastrar cliente**.
- **Usuários (só admin):** cadastro, ativar/desativar e redefinir a senha na própria linha.
- **Empresas:** editar o nome na própria linha, desativar/ativar e desconectar do Bling.
  Desativar e desconectar pedem confirmação na linha ("Confirmar?" Sim/Não), sem janelas do navegador.
- Os erros de regra que a API devolve (BadRequest, Conflict) aparecem na tela como vieram.
- **Sessão terminada:** token vencido, senha trocada ou usuário/cliente desativado fazem a API responder `401`.
  O painel volta para o login com o aviso "Sua sessão terminou" e, depois de entrar, retorna à tela onde estava.
- **Sair:** pelo ícone na barra do topo ou pelo menu do usuário, no menu lateral.
- **Só admin:** os botões "Sincronizar contas agora" e "Buscar e-mails agora" (os jobs passam por todos os clientes).

| Comando | O que faz |
|---|---|
| `npm start` | Servidor de desenvolvimento |
| `npm run build` | Build de produção em `dist/web-novo/browser` |
| `npm test` | Testes (Vitest) |

## Estrutura

```
src/
├── index.html              ← fontes Roboto + Material Icons (Google Fonts)
├── styles.scss             ← importa o SCSS do Material Dashboard
├── assets/scss/            ← SCSS original do template, sem alterações
└── app/
    ├── app.config.ts       ← rotas com "#", HttpClient, locale pt-BR
    ├── app.routes.ts       ← redireciona tudo para /pages
    ├── core/
    │   ├── previsio.service.ts   ← chamadas à API
    │   ├── previsio.models.ts
    │   ├── previsio.config.ts    ← URL da API
    │   ├── sessao.service.ts     ← login e usuário logado
    │   ├── clientes.service.ts   ← clientes do admin
    │   ├── mensagem-erro.ts      ← texto de erro devolvido pela API
    │   ├── campos-preenchidos.ts ← rótulos flutuantes de campos preenchidos pelo código
    │   ├── autenticacao.interceptor.ts
    │   └── autenticacao.guards.ts
    ├── layouts/
    │   ├── admin/          ← menu lateral + topo + rodapé
    │   └── auth/           ← tela cheia (login)
    ├── shared/
    │   ├── sidebar/        ← menu lateral (itens em menu.ts)
    │   ├── navbar/         ← barra do topo
    │   └── footer/
    └── pages/
        ├── pages.routes.ts       ← rotas de todas as telas e o layout de cada uma
        ├── painel/
        ├── notas-pendentes/
        ├── empresas/
        ├── empresa-detalhe/
        ├── clientes/             ← cadastro de clientes (só admin)
        ├── cliente-detalhe/      ← edição do cliente (só admin)
        ├── usuarios/             ← cadastro de usuários (só admin)
        └── login/                ← entrada com e-mail e senha (JWT)
```

| Endereço | Tela | Layout |
|---|---|---|
| `/#/pages/painel` | Painel de pagamentos | admin |
| `/#/pages/notas-pendentes` | Notas fora do Bling | admin |
| `/#/pages/empresas` | Empresas e conexão com o Bling | admin |
| `/#/pages/empresas/:id` | Detalhe da empresa | admin |
| `/#/pages/clientes` | Clientes (só perfil Admin) | admin |
| `/#/pages/clientes/:id` | Edição do cliente (só perfil Admin) | admin |
| `/#/pages/usuarios` | Usuários (só perfil Admin) | admin |
| `/#/pages/login` | Login (sem exigir login) | tela cheia |

As telas do layout admin exigem login.

Imagens em `public/assets/img` (servidas como `assets/img/...`).

## Padrões usados

- Componentes standalone, um por pasta, com `.component.ts` e `.component.html` (sem `.css` próprio).
- Todas as telas em `pages/`, com as rotas num único arquivo (`pages.routes.ts`) e `loadComponent` (carregamento sob demanda).
- `@if` / `@for` nos templates, `inject()` e signals para o estado das telas.
- Classes visuais do template: `card-stats`, `card-header-icon`, `card-chart`, `btn btn-info` etc.
- Cor principal azul (`info`): cabeçalhos dos cards (`card-header-info`), tabelas (`thead.text-info`),
  botões principais (`btn-info`) e menu lateral (`data-color="azure"` sobre fundo `black`).
  Cores com significado: laranja (`warning`) = pendência, verde (`success`) = conectado/ok, vermelho (`danger`) = erro.

## jQuery (mínimo)

Carregado como script global em `angular.json`, só para o **Bootstrap Material Design**
(efeito "ripple" dos botões e labels flutuantes dos campos):

| Script | Para quê |
|---|---|
| `jquery` | Requisito do Bootstrap Material Design |
| `popper.js` | Requisito do Bootstrap 4 |
| `arrive` | Aplica o efeito em elementos que aparecem depois do carregamento |
| `bootstrap-material-design` | Ripple e campos |
| `chartist` (0.11) | Gráfico de barras do painel, mesma versão do template |

A única chamada a jQuery no código é `$('body').bootstrapMaterialDesign()` em `app.ts`.
Menu lateral, barra do topo e menu do celular foram reescritos em Angular, sem jQuery.

## O que ficou de fora do template

Páginas de demonstração (mapas, calendário, wizard, widgets, timeline, tabelas de exemplo),
o painel de troca de cores (engrenagem) e as bibliotecas usadas só por elas:
`@agm/core`, `ng2-select`, `ng2-nouislider`, `ngx-chips`, `angular-calendar`, `datatables`,
`fullcalendar`, `jqvmap`, `sweetalert2`. Também saíram TSLint, Karma e Protractor (o projeto usa Vitest).
