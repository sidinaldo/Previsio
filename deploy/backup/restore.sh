#!/usr/bin/env bash
#
# Restaura um backup do Previsio. Rodar DENTRO do contêiner de backup, com a API PARADA:
#
#   docker compose --env-file .env stop api
#   docker exec -it previsio-backup restore.sh /tmp/backup/previsio-AAAA-MM-DD-HHMM.dump.enc \
#                                              /tmp/backup/chaves-AAAA-MM-DD-HHMM.tar.gz.enc
#   docker compose --env-file .env start api
#
# ⚠️ Os dois arquivos do MESMO horário: banco e chaves andam juntos.
# ⚠️ O banco atual é substituído (--clean). As chaves são gravadas por cima das atuais.

set -euo pipefail

DUMP="${1:?informe o arquivo previsio-*.dump.enc}"
CHAVES="${2:?informe o arquivo chaves-*.tar.gz.enc}"

: "${BACKUP_SENHA:?BACKUP_SENHA ausente}"
: "${POSTGRES_PASSWORD:?POSTGRES_PASSWORD ausente}"

export PGPASSWORD="$POSTGRES_PASSWORD"
export PGHOST="${PGHOST:-db}"
export PGUSER=previsio

decifrar() { openssl enc -d -aes-256-cbc -pbkdf2 -pass env:BACKUP_SENHA; }

echo "1/2 banco (substitui o atual)"
decifrar < "$DUMP" | pg_restore --clean --if-exists --no-owner -d previsio

echo "2/2 chaves: o volume está só leitura aqui; extraindo para /tmp/backup/chaves-restauradas"
rm -rf /tmp/backup/chaves-restauradas && mkdir -p /tmp/backup/chaves-restauradas
decifrar < "$CHAVES" | tar xzf - -C /tmp/backup/chaves-restauradas

cat <<'FIM'

Banco restaurado. Agora copie as chaves para o volume da API, fora do contêiner:

  docker run --rm -v previsio_backup_local:/origem:ro -v previsio_chaves_prod:/destino alpine \\
    sh -c 'cp -a /origem/chaves-restauradas/. /destino/ && chown -R 1654:1654 /destino'

(1654 é o usuário `app` da imagem .NET.) Depois: docker compose --env-file .env start api
FIM
