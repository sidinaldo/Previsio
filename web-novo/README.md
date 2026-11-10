# Previsio — painel web (Angular 22)

Painel do Previsio com o visual do **Material Dashboard PRO** (Creative Tim, v2.9.0), reconstruído em Angular 22 com componentes standalone.

## Requisitos

- **Node 22.22.3 ou superior** (ou 24.15+). O Angular 22 não roda no Node 20.

## Rodar

```bash
npm install
npm start
```

Acesse `http://localhost:4200/#/pages/painel`.

A API do Previsio precisa estar rodando. Ajuste a URL, a chave e o id do cliente em
`src/app/core/previsio.config.ts`.

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
    │   └── previsio.config.ts    ← URL da API, chave e id do cliente
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
        └── login/                ← só o visual, sem autenticação ainda
```

| Endereço | Tela | Layout |
|---|---|---|
| `/#/pages/painel` | Painel de pagamentos | admin |
| `/#/pages/notas-pendentes` | Notas fora do Bling | admin |
| `/#/pages/empresas` | Empresas e conexão com o Bling | admin |
| `/#/pages/empresas/:id` | Detalhe da empresa | admin |
| `/#/pages/login` | Login | tela cheia |

Imagens em `public/assets/img` (servidas como `assets/img/...`).

## Padrões usados

- Componentes standalone, um por pasta, com `.component.ts` e `.component.html` (sem `.css` próprio).
- Todas as telas em `pages/`, com as rotas num único arquivo (`pages.routes.ts`) e `loadComponent` (carregamento sob demanda).
- `@if` / `@for` nos templates, `inject()` e signals para o estado das telas.
- Classes visuais do template: `card-stats`, `card-header-icon`, `card-chart`, `btn btn-info` etc.

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
