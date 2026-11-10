# Previsio — MVP

Lê as notas fiscais (XML) que chegam numa caixa de e-mail dedicada, lança cada parcela como
**conta a pagar no Bling** da empresa certa e mostra um **painel consolidado** dos próximos 30 dias
de todas as empresas do cliente.

```
E-mail dedicado (IMAP) ──→ Leitura do XML ──→ Conta a pagar no Bling (CNPJ certo)
                                         └──→ Sincronização ──→ Painel 30 dias + notas pendentes
```

## Estrutura

| Pasta | Conteúdo |
|---|---|
| `src/Previsio.Core` | Entidades, leitura de NF-e (`LeitorNFe`) e cálculo do painel (`CalculadoraPainel`) |
| `src/Previsio.Bling` | Cliente da API v3 do Bling: OAuth, contatos, contas a pagar, limite de 3 req/s por conta |
| `src/Previsio.Data` | EF Core + PostgreSQL |
| `src/Previsio.Api` | API REST + jobs Hangfire (leitura de e-mail, lançamento, sincronização) |
| `tests/Previsio.Core.Tests` | Testes do leitor de NF-e e do painel |
| `web` | Painel Angular 15 (template Material Dashboard Pro); telas do Previsio em `web/src/app/previsio` |

## Jobs (Hangfire)

| Job | Frequência | O que faz |
|---|---|---|
| `leitura-email` | 10 min | Lê e-mails não lidos, extrai XMLs (inclusive em `.zip`), registra notas novas |
| `lancamento` | 10 min | Cria fornecedor (se preciso) e uma conta a pagar por parcela no Bling |
| `sincronizacao` | 30 min | Traz as contas a pagar do Bling (inclusive as lançadas à mão) para o painel |

Painel do Hangfire: `http://localhost:5080/hangfire` (abre só na própria máquina).

### Regras importantes

- **Sem duplicidade:** a chave de acesso da NF-e é única por cliente. A mesma nota em dois e-mails é lançada uma vez.
- **Retomada segura:** cada parcela guarda o id da conta criada no Bling. Se cair no meio, a próxima tentativa continua de onde parou.
- **Rastreio:** o Bling não tem campo para a chave da NF-e, então ela vai no `historico` da conta a pagar.
- **Nº do documento:** `48213` (parcela única) ou `48213/2` (segunda parcela).
- **Nota sem duplicatas no XML:** fica como pendente ("Sem vencimentos no XML") para lançamento manual.

---

## 1. Pré-requisitos

- .NET 8 SDK
- Docker (para o PostgreSQL) ou um PostgreSQL 14+
- Ferramenta do EF: `dotnet tool install --global dotnet-ef`

## 2. Banco de dados

```bash
docker compose up -d
```

## 3. Aplicativo no Bling

1. Acesse a **Área do integrador** em https://developer.bling.com.br e cadastre um aplicativo.
2. Em **Link de redirecionamento**, informe: `http://localhost:5080/api/bling/callback`
3. Escopos necessários: **Contatos** e **Contas a pagar** (leitura e escrita).
4. Copie o **Client ID** e o **Client Secret**.

## 4. Configuração

Crie `src/Previsio.Api/appsettings.Development.json` (fica fora do Git):

```json
{
  "Bling": {
    "ClientId": "SEU_CLIENT_ID",
    "ClientSecret": "SEU_CLIENT_SECRET",
    "RedirectUri": "http://localhost:5080/api/bling/callback"
  },
  "Jwt": { "Chave": "COLE_AQUI_UMA_CHAVE_ALEATORIA_LONGA" },
  "AdminInicial": {
    "Nome": "Seu nome",
    "Email": "voce@softioconsultoria.com.br",
    "Senha": "uma-senha-forte-provisoria"
  },
  "Front": { "Url": "http://localhost:4200/#" }
}
```

> `Jwt:Chave` assina os tokens de login: precisa ter **pelo menos 32 caracteres** e a API não sobe sem ela.
> Gere uma com `openssl rand -base64 48` (Git Bash). Trocar a chave derruba todos os logins abertos.

> `AdminInicial` cria o primeiro administrador quando o banco ainda não tem nenhum.
> Depois do primeiro login, troque a senha em `POST /api/auth/trocar-senha` e **apague** `AdminInicial:Senha` do arquivo.

> `Front:Url` termina com `/#` porque o Material Dashboard Pro usa rotas com hash. Se o seu template não usa, tire o `/#`.

> A pasta `chaves-dataprotection` guarda as chaves que criptografam tokens e senhas.
> **Não apague** — se ela sumir, será preciso reconectar o Bling e recadastrar a senha do IMAP.

## 5. Migration inicial e execução

```bash
dotnet ef migrations add Inicial --project src/Previsio.Data --startup-project src/Previsio.Api
dotnet test
dotnet run --project src/Previsio.Api
```

A API aplica as migrations ao subir e abre o Swagger em `http://localhost:5080/swagger`.

## Autenticação

Todas as rotas `/api` exigem login, menos `POST /api/auth/login` e o callback do Bling.

1. `POST /api/auth/login` com `{ "email": "...", "senha": "..." }` devolve `token`, `expiraEm` e os dados do usuário.
2. Envie o token em todas as chamadas: `Authorization: Bearer {token}`. No Swagger, use o botão **Authorize**.
3. O token vale 8 horas (`Jwt:ValidadeMinutos`). Depois disso, faça login de novo.

| Perfil | Acesso |
|---|---|
| `Admin` | Tudo: todos os clientes, cadastro de clientes e usuários, execução manual dos jobs |
| `Cliente` | Só os dados do próprio cliente (painel, empresas, notas, conexão com o Bling) |

Pedido de outro cliente responde `403` (rotas com `{clienteId}`) ou `404` (rotas com `{empresaId}` ou `{notaId}`, para não revelar ids).

- **Senhas:** guardadas com hash PBKDF2 (ASP.NET Core Identity). Mínimo de 8 caracteres.
- **Revogação na hora:** desativar o usuário ou o cliente, trocar ou redefinir a senha invalida os tokens já emitidos.
- **Tentativas de login:** 10 por minuto por IP (`Login:TentativasPorMinuto`); acima disso a API responde `429`.
  Atrás de um proxy reverso todos os acessos chegam com o IP do proxy: configure os *forwarded headers* antes de publicar.

## 6. Cadastrar o cliente piloto

**Caixa de e-mail:** crie um endereço dedicado (ex.: `notas@softioconsultoria.com.br`) com IMAP habilitado.
No Gmail do cliente, um filtro `filename:xml OR filename:zip` encaminha as notas para esse endereço.

Entre com o administrador (`POST /api/auth/login`) e cadastre o cliente:

```http
POST /api/clientes
Authorization: Bearer TOKEN_DO_ADMIN
Content-Type: application/json

{
  "nome": "Autopeças (piloto)",
  "imapHost": "imap.gmail.com",
  "imapPorta": 993,
  "imapSsl": true,
  "imapUsuario": "notas@softioconsultoria.com.br",
  "imapSenha": "SENHA_DE_APP"
}
```

Crie o usuário do cliente (use o `id` retornado acima):

```http
POST /api/usuarios
Authorization: Bearer TOKEN_DO_ADMIN
Content-Type: application/json

{
  "nome": "Financeiro Autopeças",
  "email": "financeiro@autopecas.com.br",
  "senha": "senha-provisoria",
  "perfil": "Cliente",
  "clienteId": 1
}
```

Passe a senha provisória ao cliente e peça para ele trocá-la em `POST /api/auth/trocar-senha`.

Depois, cadastre os 5 CNPJs pela tela **Empresas** do painel e clique em **Conectar Bling** em cada um
(o cliente faz login na conta Bling daquela empresa e autoriza).

## 7. Painel Angular (Material Dashboard Pro)

A pasta `web` é o template Material Dashboard Pro Angular v2.9.0 (Angular 15) com o módulo do Previsio já integrado:

| Arquivo | O que foi feito |
|---|---|
| `web/src/app/previsio/` | Telas: painel, notas pendentes, empresas e detalhe da empresa |
| `web/src/app/app.routing.ts` | Rota lazy `previsio` dentro do `AdminLayoutComponent` |
| `web/src/app/sidebar/sidebar.component.ts` | Item "Previsio" no menu lateral |

> **Atenção:** o painel ainda não tem tela de login. Ele envia o header `X-Api-Key`, que a API não aceita mais,
> então todas as chamadas voltam `401` até o painel passar a fazer login e enviar o token JWT.

1. Ajuste `web/src/app/previsio/previsio.config.ts` (URL da API, chave e id do cliente).
2. Rode o painel:

```bash
cd web
npm install
npm start
```

3. Acesse `http://localhost:4200/#/previsio/painel`.

## Endpoints

| Método | Rota | Uso |
|---|---|---|
| GET/POST | `/api/clientes` | Listar / cadastrar cliente (com IMAP) |
| GET/POST | `/api/clientes/{id}/empresas` | Listar / cadastrar CNPJs |
| GET | `/api/empresas/{id}/bling/conectar` | URL de autorização do Bling |
| GET | `/api/bling/callback` | Retorno do OAuth (sem chave de API) |
| GET | `/api/clientes/{id}/painel?dias=30` | Painel consolidado |
| GET | `/api/empresas/{id}/vencimentos?dias=30` | Vencimentos por dia de uma empresa |
| GET | `/api/clientes/{id}/notas/pendentes` | Notas fora do Bling |
| POST | `/api/notas/{id}/reprocessar` | Tentar lançar de novo |
| POST | `/api/jobs/{leitura-email\|lancamento\|sincronizacao}/executar` | Rodar um job agora (só admin) |
| POST | `/api/auth/login` | Login: devolve o token (sem login) |
| GET | `/api/auth/eu` | Dados do usuário logado |
| POST | `/api/auth/trocar-senha` | Trocar a própria senha (devolve um token novo) |
| GET/POST | `/api/usuarios` | Listar (`?clienteId=`) / cadastrar usuários (só admin) |
| PUT | `/api/usuarios/{id}` | Alterar nome, ativar/desativar, redefinir senha (só admin) |

`GET/POST /api/clientes` também são só para admin.

## Primeiro teste recomendado

1. Conecte **uma** empresa à sua conta Bling de teste.
2. Envie para a caixa dedicada um e-mail com um XML real cujo destinatário seja o CNPJ cadastrado.
3. `POST /api/jobs/leitura-email/executar` e depois `POST /api/jobs/lancamento/executar`.
4. Confira no Bling: fornecedor criado e uma conta a pagar por parcela, com a chave no histórico.
5. Se der erro, a mensagem completa do Bling aparece em **Notas pendentes**.

## Próximos passos para venda

| Item | Por quê |
|---|---|
| Tela de login no painel Angular | A API já exige JWT; o painel ainda manda a chave de API antiga e recebe `401` |
| Resumo semanal por WhatsApp | Diferencial: "R$ X vencem esta semana, 3 notas fora do Bling" |
| Leitura de PDF com IA | Notas que chegam sem XML |
| Busca de notas na SEFAZ | Pegar a nota que o fornecedor não enviou por e-mail |
