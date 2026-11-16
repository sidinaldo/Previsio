#!/usr/bin/env bash
#
# Confere de ponta a ponta que o backup chega no R2: roda um backup agora e procura o arquivo lá.
# (Na Nexora, um R2_ACCOUNT_ID com dois caracteres trocados deixou o backup 12 horas só no disco
# local sem aviso nenhum. Por isso a checagem é de verdade, e não de formato.)

set -euo pipefail

cd "$(dirname "$0")"
set -a
# shellcheck source=/dev/null
. ./.env
set +a

if [ -z "${R2_BUCKET_BACKUP:-}" ]; then
    echo "AVISO: R2_BUCKET_BACKUP vazio — o backup fica SÓ NESTA MÁQUINA." >&2
    exit 0
fi

echo "Rodando um backup agora..."
if ! docker exec previsio-backup backup.sh; then
    echo "ERRO: o backup falhou. Veja a saída acima (R2_ACCOUNT_ID, chaves do R2, bucket)." >&2
    exit 1
fi

PASTA="${R2_BUCKET_BACKUP}/${R2_PASTA:-previsio}/$(date +%Y/%m)/"
ENCONTRADOS=$(docker exec \
    -e RCLONE_CONFIG_R2_TYPE=s3 \
    -e RCLONE_CONFIG_R2_PROVIDER=Cloudflare \
    -e RCLONE_CONFIG_R2_ACCESS_KEY_ID="$R2_ACCESS_KEY_ID" \
    -e RCLONE_CONFIG_R2_SECRET_ACCESS_KEY="$R2_SECRET_ACCESS_KEY" \
    -e RCLONE_CONFIG_R2_ENDPOINT="https://${R2_ACCOUNT_ID}.r2.cloudflarestorage.com" \
    -e RCLONE_CONFIG_R2_REGION=auto \
    previsio-backup rclone lsf "r2:$PASTA" --max-age 30m --include "previsio-*.dump.enc" | wc -l)

if [ "$ENCONTRADOS" -lt 1 ]; then
    echo "ERRO: nenhum dump dos últimos 30 minutos em r2:$PASTA." >&2
    exit 1
fi
echo "Backup no R2: OK ($ENCONTRADOS arquivo(s) recente(s) em r2:$PASTA)."
