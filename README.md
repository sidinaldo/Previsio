# Previsio — MVP

Lê as notas fiscais (XML) que chegam numa caixa de e-mail dedicada, lança cada parcela como
**conta a pagar no Bling** da empresa certa e mostra um **painel consolidado** dos próximos 30 dias
de todas as empresas do cliente.

```
E-mail dedicado (IMAP) ──→ Leitura do XML ──→ Conta a pagar no Bling (CNPJ certo)
                                         └──→ Sincronização ──→ Painel 30 dias + notas pendentes
```

## Estrutura

Camadas, da mais interna para a mais externa (nenhuma depende de uma que vem depois dela):

| Pasta | Conteúdo |
|---|---|
| `src/Previsio.Core` | Entidades e regras puras: leitura de NF-e (`LeitorNFe`), painel (`CalculadoraPainel`), chave de acesso, período de teste, conferência com o Bling. Interface `IRepositorio<T>`. Não depende de nada |
| `src/Previsio.Bling` | Cliente da API v3 do Bling: OAuth, contatos, contas a pagar, limite de 3 req/s por conta |
| `src/Previsio.Aplicacao` | Serviços por assunto (Notas, Empresas, LeituraEmail, Sincronizacao, Avisos...), DTOs e mapeamentos. As interfaces do que é externo (IMAP, SMTP, SEFAZ, Hangfire, criptografia) ficam em `Abstracoes` |
| `src/Previsio.Infra` | Implementações dessas interfaces: MailKit (IMAP e SMTP), SEFAZ (SOAP), Hangfire, Data Protection, hash de senha |
| `src/Previsio.Data` | EF Core + PostgreSQL: `PrevisioDbContext`, migrations e `Repositorio<T>` |
| `src/Previsio.Api` | Controllers, JWT e os jobs do Hangfire (só chamam o serviço da Aplicacao) |
| `tests/Previsio.Tests` | Todos os testes, com EF Core em memória; as pastas espelham as camadas (`Core/`, `Aplicacao/`, `Infra/`, `Data/`, `Bling/`, `Api/`) e `Apoio/` tem os dublês (Bling, IMAP, SMTP, SEFAZ, relógio) |
| `web-novo` | Painel Angular 21 (visual do Material Dashboard Pro), com login JWT |

Regra de onde pôr cada coisa: regra de negócio no serviço da `Aplicacao`; acesso ao banco só por `IRepositorio<T>`;
algo que fala com o mundo de fora vira uma interface em `Aplicacao/Abstracoes` implementada na `Infra`.

## Jobs (Hangfire)

| Job | Frequência | O que faz |
|---|---|---|
| `leitura-email` | 10 min | Lê e-mails não lidos, extrai XMLs (inclusive em `.zip`), registra notas novas |
| `lancamento` | 10 min | Cria fornecedor (se preciso) e uma conta a pagar por parcela no Bling |
| `sincronizacao` | 30 min | Traz as contas a pagar do Bling (inclusive as lançadas à mão) e, com o fluxo de caixa ligado na rede, as contas a receber. Também atualiza as categorias do Bling (uma vez por dia; o botão **Sincronizar com o Bling** busca de novo na hora) e confere a categoria das contas aos poucos |

Painel do Hangfire: `http://localhost:5080/hangfire` (abre só na própria máquina).

### Regras importantes

- **Sem duplicidade:** a chave de acesso da NF-e é única por cliente. A mesma nota em dois e-mails é lançada uma vez.
- **Retomada segura:** cada parcela guarda o id da conta criada no Bling. Se cair no meio, a próxima tentativa continua de onde parou.
- **Rastreio:** o Bling não tem campo para a chave da NF-e, então ela vai no `historico` da conta a pagar.
- **Nº do documento:** `48213` (parcela única) ou `48213/2` (segunda parcela).
- **Nota sem duplicatas no XML:** fica como pendente ("Sem vencimentos no XML") para lançamento manual.
- **Categoria da conta:** regra da rede para o fornecedor (pelo nome da categoria) → senão a categoria da última conta
  desse fornecedor no Bling → senão a categoria padrão da rede → senão a automática (`Categorias:PadraoAutomatico`,
  padrão "Compras de Mercadorias"; vazio desliga) → senão vai sem categoria. Configurado no card **Categorias** em Empresas.
- **Categoria das contas do Bling:** a lista de contas do Bling não traz a categoria, só o detalhe de cada conta. Por isso a
  sincronização busca o detalhe aos poucos: primeiro as contas novas ou que mudaram, depois as conferidas há mais de 7 dias,
  até `Sincronizacao:MaximoDetalhesPorExecucao` contas por empresa a cada execução (padrão 50). Conta já conferida é
  conferida de novo depois de `Sincronizacao:DiasReconferirCategoria` dias (padrão 1, de 1 a 30), para quem troca a
  categoria à mão no Bling. No painel, as que ainda não foram conferidas aparecem como "A conferir" e as que têm uma
  categoria fora da lista da empresa (falta o escopo ou a lista está desatualizada) como "Categoria não identificada".

---

## 1. Pré-requisitos

- .NET 8 SDK
- Docker (para o PostgreSQL) ou um PostgreSQL 14+
- Ferramenta do EF: `dotnet tool install --global dotnet-ef`

## 2. Banco de dados

```bash
docker compose up -d
```

Se a porta 5432 já for de um PostgreSQL instalado na máquina, use ele: crie o usuário e o banco `previsio`
e informe a conexão em `ConnectionStrings:Previsio` no `appsettings.Development.json`.

## 3. Aplicativo no Bling

1. Acesse a **Área do integrador** em https://developer.bling.com.br e cadastre um aplicativo.
2. Em **Link de redirecionamento**, informe: `http://localhost:5080/api/bling/callback`
3. Escopos necessários: **Contatos** e **Contas a pagar** (leitura e escrita) e **Contas a receber** (leitura, para o fluxo de caixa).
   Para as próximas funcionalidades, já marque também em leitura: **Categorias de receitas e despesas**, **Formas de pagamento**,
   **Pedidos de compra**, **Contas contábeis** e **Caixas e bancos**.
   > Mudar os escopos do aplicativo derruba a conexão de todas as empresas (o Bling revoga o token e o refresh token):
   > cada empresa precisa clicar em **Conectar Bling** de novo. Junte as mudanças de escopo numa vez só.
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

> `Front:Url` termina com `/#` porque o painel usa rotas com hash: é para lá que a API devolve quem volta do OAuth do Bling.
> Para o CORS a API usa só a origem (`http://localhost:4200`).

> A pasta `chaves-dataprotection` guarda as chaves que criptografam tokens e senhas.
> **Não apague** — se ela sumir, será preciso reconectar o Bling e recadastrar a senha do IMAP.

## 5. Execução

```bash
dotnet test
dotnet run --project src/Previsio.Api
```

A API aplica as migrations ao subir (a `Inicial` já está em `src/Previsio.Data/Migrations`) e abre o Swagger em `http://localhost:5080/swagger`.

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

Entre no painel com o administrador e cadastre o cliente (a rede) na tela **Clientes**, com os dados da caixa IMAP.
Enquanto não houver nenhum cliente, o painel mostra só o botão **Cadastrar cliente**.
Pela API:

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

Crie o usuário do cliente na tela **Usuários** do painel, ou pela API (use o `id` do cliente):

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

Os dados do cliente (inclusive a caixa IMAP) podem ser corrigidos depois em **Clientes → Editar**:
senha em branco mantém a atual.

Depois, cadastre os 5 CNPJs pela tela **Empresas** do painel e clique em **Conectar Bling** em cada um
(o cliente faz login na conta Bling daquela empresa e autoriza).

## 7. Painel Angular

A pasta `web-novo` é o painel em Angular 21 com o visual do Material Dashboard Pro
(detalhes em `web-novo/README.md`). Precisa de Node 22.13 ou superior.

1. Confira a URL da API em `web-novo/src/app/core/previsio.config.ts` (padrão `http://localhost:5080/api`).
2. Rode o painel:

```bash
cd web-novo
npm install
npm start
```

3. Acesse `http://localhost:4200/#/pages/painel` e entre com o e-mail e a senha de um usuário.
   O admin escolhe na barra do topo qual cliente quer ver; o usuário de um cliente vê só o dele.

> A API só aceita chamadas do endereço configurado em `Front:Url` (padrão `http://localhost:4200/#`).
> Se o painel rodar em outra porta ou domínio, ajuste `Front:Url`, senão o navegador barra as chamadas (CORS).

## Endpoints

| Método | Rota | Uso |
|---|---|---|
| GET/POST | `/api/clientes` | Listar / cadastrar cliente (com IMAP) |
| GET/PUT | `/api/clientes/{id}` | Ver (admin ou o próprio cliente) / alterar, ativar e desativar (só admin; `imapSenha` vazia mantém a atual) |
| GET/POST | `/api/clientes/{id}/empresas` | Listar / cadastrar CNPJs |
| PUT | `/api/empresas/{id}` | Alterar o nome, ativar ou desativar (o CNPJ não muda) |
| GET | `/api/empresas/{id}/bling/conectar` | URL de autorização do Bling |
| DELETE | `/api/empresas/{id}/bling` | Desconectar do Bling (apaga os tokens) |
| GET | `/api/bling/callback` | Retorno do OAuth (sem login) |
| GET | `/api/clientes/{id}/painel?dias=30` | Painel consolidado |
| GET | `/api/empresas/{id}/vencimentos?dias=30` | Vencimentos por dia de uma empresa |
| GET | `/api/clientes/{id}/notas` | Todas as notas lidas do e-mail, com as parcelas (`?empresaId=&situacao=&de=&ate=&pagina=`; `situacao` aceita o nome da situação ou `Pendentes`; 50 por página) |
| GET | `/api/clientes/{id}/notas/pendentes` | Notas fora do Bling |
| POST | `/api/notas/{id}/reprocessar` | Tentar lançar de novo |
| POST | `/api/jobs/{leitura-email\|lancamento\|sincronizacao}/executar` | Rodar um job agora (só admin) |
| GET | `/api/jobs/{leitura-email\|lancamento\|sincronizacao}/situacao` | Se o job ainda está na fila ou rodando (`emExecucao`); a tela consulta para atualizar sozinha quando ele termina (só admin) |
| POST | `/api/auth/login` | Login: devolve o token (sem login) |
| GET | `/api/auth/eu` | Dados do usuário logado |
| POST | `/api/auth/trocar-senha` | Trocar a própria senha (devolve um token novo) |
| GET/POST | `/api/usuarios` | Listar (`?clienteId=`) / cadastrar usuários (só admin) |
| PUT | `/api/usuarios/{id}` | Alterar nome, ativar/desativar, redefinir senha (só admin) |
| GET | `/api/clientes/{id}/categorias` | Card Categorias: padrão, opções do Bling, regras por fornecedor e avisos |
| PUT | `/api/clientes/{id}/categorias/padrao` | Categoria padrão da rede pelo nome (`{ "categoria": "" }` tira) |
| POST/DELETE | `/api/clientes/{id}/categorias/regras[/{regraId}]` | Criar (ou trocar a categoria) / remover a regra de um fornecedor |

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
| Troca da própria senha no painel | Hoje só pela API (`/api/auth/trocar-senha`) |
| Resumo semanal por WhatsApp | Diferencial: "R$ X vencem esta semana, 3 notas fora do Bling" |
| Leitura de PDF com IA | Notas que chegam sem XML |
| Busca de notas na SEFAZ | Pegar a nota que o fornecedor não enviou por e-mail |
