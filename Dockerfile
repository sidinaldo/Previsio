# =============================================================================
#  Previsio — API (.NET 8), build multi-stage.
#  Contexto de build = raiz do repositório:  docker build -t previsio-api .
# =============================================================================

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Os .csproj primeiro, sozinhos: enquanto as dependências não mudarem, a camada do restore fica em
# cache e mudar código não baixa pacote de novo.
COPY Directory.Build.props ./
COPY src/Previsio.Core/Previsio.Core.csproj           src/Previsio.Core/
COPY src/Previsio.Bling/Previsio.Bling.csproj         src/Previsio.Bling/
COPY src/Previsio.Data/Previsio.Data.csproj           src/Previsio.Data/
COPY src/Previsio.Aplicacao/Previsio.Aplicacao.csproj src/Previsio.Aplicacao/
COPY src/Previsio.Infra/Previsio.Infra.csproj         src/Previsio.Infra/
COPY src/Previsio.Api/Previsio.Api.csproj             src/Previsio.Api/
RUN dotnet restore src/Previsio.Api/Previsio.Api.csproj

COPY src/ src/
RUN dotnet publish src/Previsio.Api/Previsio.Api.csproj -c Release -o /app/publish --no-restore

# ---------- runtime ----------
# Debian, e não Alpine: os e-mails formatam dinheiro e data em pt-BR (precisa do ICU) e os jobs
# usam o fuso America/Sao_Paulo (precisa do tzdata). A imagem Debian já traz os dois.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# A pasta das chaves do Data Protection (criptografam os tokens do Bling, a senha do IMAP e os
# certificados A1). Criada aqui, com dono: um volume vazio montado por cima herda o dono do
# diretório da imagem. Sem isso o volume nasce do root e a API, que roda como `app`, não grava.
RUN mkdir -p /app/chaves && chown app:app /app/chaves

USER app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

# Sem HEALTHCHECK: a imagem não traz curl. Quem confere é o deploy.sh, batendo em /health.
ENTRYPOINT ["dotnet", "Previsio.Api.dll"]
