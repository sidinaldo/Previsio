#!/usr/bin/env bash
#
# Backup do Previsio: o banco e as chaves do Data Protection, cifrados, com cópia fora da máquina.
#
# ==========================================================================================
#  ⚠️ DUAS PEÇAS, NÃO UMA. O banco guarda os tokens do Bling, as senhas de IMAP e os certificados
#  A1 CIFRADOS com as chaves do Data Protection. Restaurar o banco sem as chaves devolve tudo
#  ilegível: todo Bling reconectado, toda senha e certificado recadastrados.
#
#  ⚠️ CIFRADO: o dump tem notas, contas e dados financeiros das redes.
#
#  ⚠️ O SCRIPT NÃO APAGA NADA NO R2. A retenção é regra de ciclo de vida do bucket.
# ==========================================================================================

set -euo pipefail

RETENCAO_LOCAL_DIAS=2
DESTINO=/tmp/backup

: "${BACKUP_SENHA:?BACKUP_SENHA ausente — sem ela o backup sairia em claro}"
: "${POSTGRES_PASSWORD:?POSTGRES_PASSWORD ausente}"

export PGPASSWORD="$POSTGRES_PASSWORD"
export PGHOST="${PGHOST:-db}"
export PGUSER=previsio

D=$(date +%F-%H%M)
mkdir -p "$DESTINO"

# Cifra no fluxo: o dump nunca existe em claro no disco
cifrar() { openssl enc -aes-256-cbc -pbkdf2 -salt -pass env:BACKUP_SENHA; }

echo "[$(date +%T)] 1/2 banco previsio"
pg_dump -Fc previsio | cifrar > "$DESTINO/previsio-$D.dump.enc"

echo "[$(date +%T)] 2/2 chaves do Data Protection"
tar czf - -C /dados/chaves . | cifrar > "$DESTINO/chaves-$D.tar.gz.enc"

# Dump minúsculo = algo deu errado sem o pg_dump reclamar (ele sempre leva o schema)
tamanho=$(stat -c%s "$DESTINO/previsio-$D.dump.enc")
if [ "$tamanho" -lt 256 ]; then
    echo "ERRO: previsio-$D.dump.enc tem $tamanho bytes — dump suspeito, nada foi apagado." >&2
    exit 1
fi

# ---------------------------------------------------------------- fora da máquina
if [ -n "${R2_BUCKET_BACKUP:-}" ]; then
    echo "[$(date +%T)] enviando para o R2"
    export RCLONE_CONFIG_R2_TYPE=s3
    export RCLONE_CONFIG_R2_PROVIDER=Cloudflare
    export RCLONE_CONFIG_R2_ACCESS_KEY_ID="${R2_ACCESS_KEY_ID:?}"
    export RCLONE_CONFIG_R2_SECRET_ACCESS_KEY="${R2_SECRET_ACCESS_KEY:?}"
    export RCLONE_CONFIG_R2_ENDPOINT="https://${R2_ACCOUNT_ID:?}.r2.cloudflarestorage.com"
    export RCLONE_CONFIG_R2_REGION=auto

    rclone copy "$DESTINO" "r2:${R2_BUCKET_BACKUP}/${R2_PASTA:-previsio}/$(date +%Y/%m)/" \
        --include "*-$D.*.enc" --s3-no-check-bucket
else
    echo "AVISO: R2_BUCKET_BACKUP vazio — a cópia existe SÓ NESTA MÁQUINA." >&2
fi

# Expurgo local só DEPOIS do envio
find "$DESTINO" -name '*.enc' -mtime "+$RETENCAO_LOCAL_DIAS" -delete

# Heartbeat por último: o monitor externo alerta pela AUSÊNCIA do chamado
if [ -n "${BACKUP_PUSH_URL:-}" ]; then
    curl -fsS --max-time 10 "$BACKUP_PUSH_URL" > /dev/null || \
        echo "AVISO: heartbeat não foi entregue (o backup em si deu certo)." >&2
fi

echo "[$(date +%T)] pronto. Local ${RETENCAO_LOCAL_DIAS}d; remoto pela regra do bucket."
