# Backup do Previsio: pg_dump + chaves do Data Protection, cifrados e enviados para o R2.
#
# ⚠️ O CLIENTE ACOMPANHA A VERSÃO DO SERVIDOR: `pg_dump` mais velho que o banco recusa rodar.
# O `db` é Postgres 16, e o Alpine 3.21 traz o postgresql16-client.
FROM alpine:3.21

RUN apk add --no-cache postgresql16-client rclone openssl curl tzdata bash

ENV TZ=America/Sao_Paulo

COPY backup.sh restore.sh /usr/local/bin/
COPY crontab /etc/crontabs/root
RUN chmod +x /usr/local/bin/backup.sh /usr/local/bin/restore.sh

# crond em primeiro plano (processo 1) com o log no stdout do contêiner
CMD ["crond", "-f", "-d", "8"]
