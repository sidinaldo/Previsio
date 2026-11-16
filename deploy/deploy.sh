#!/usr/bin/env bash
#
# Sobe ou atualiza o Previsio. `/opt/previsio/deploy/deploy.sh`, de qualquer diretório.
#
# As migrations rodam sozinhas: a API aplica o que faltar ao subir (Program.cs).

set -euo pipefail

cd "$(dirname "$0")"
COMPOSE="docker compose --env-file .env"

if [ ! -f .env ]; then
    echo "ERRO: falta o deploy/.env (copie o .env.example e preencha)." >&2
    exit 1
fi

# O Tunnel da Nexora entrega o tráfego pela rede dela; sem ela o painel fica fora do ar
if ! docker network inspect nexora_borda > /dev/null 2>&1; then
    echo "ERRO: a rede nexora_borda não existe. Suba a Nexora antes (ela cria a rede)." >&2
    exit 1
fi

echo "== 1/5 build =="
$COMPOSE build

echo "== 2/5 banco de pé =="
$COMPOSE up -d db
$COMPOSE exec -T db sh -c 'until pg_isready -U previsio -d previsio -q; do sleep 1; done'

echo "== 3/5 subindo API, painel e backup =="
$COMPOSE up -d --remove-orphans

echo "== 4/5 esperando a API responder (migrations incluídas) =="
# Espera o /health, e não o contêiner existir: a API ainda aplica as migrations e abre o banco.
NO_AR=nao
for i in $(seq 1 60); do
    if docker run --rm --network previsio_interna curlimages/curl:8.11.1 \
            -fsS --max-time 5 http://previsio-api:8080/health > /dev/null 2>&1; then
        echo "API respondendo depois de $(( i * 2 ))s."
        NO_AR=sim
        break
    fi
    sleep 2
done

if [ "$NO_AR" = nao ]; then
    echo "ERRO: a API não respondeu em 120s. Log:" >&2
    $COMPOSE logs --tail 80 api >&2
    exit 1
fi

# O painel pelo mesmo caminho que o Tunnel usa: rede da Nexora, nome do contêiner
if ! docker run --rm --network nexora_borda curlimages/curl:8.11.1 \
        -fsS --max-time 5 http://previsio-web/ > /dev/null 2>&1; then
    echo "ERRO: o painel não responde em http://previsio-web/ pela rede nexora_borda." >&2
    $COMPOSE logs --tail 40 web >&2
    exit 1
fi
echo "Painel respondendo pela rede do Tunnel."

docker image prune -f > /dev/null

echo "== 5/5 o backup está chegando no R2? =="
./verificar-backup.sh
