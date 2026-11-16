# =============================================================================
#  Previsio — painel (Angular) servido pelo nginx, que também repassa /api para a API.
#  Contexto de build = web-novo/:  docker build -t previsio-web web-novo
# =============================================================================

# ---------- build ----------
FROM node:22-bookworm-slim AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
# Build de produção: troca o previsio.config.ts pelo .producao.ts (API em /api, mesmo endereço)
RUN npx ng build --configuration production

# ---------- runtime ----------
FROM nginx:1.27-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY nginx-seguranca.conf /etc/nginx/snippets/seguranca.conf
COPY --from=build /app/dist/web-novo/browser /usr/share/nginx/html
EXPOSE 80
