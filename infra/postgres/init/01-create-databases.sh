#!/bin/bash
# Cria os bancos e o usuario do Zabbix.
# A senha do Zabbix vem da variavel de ambiente ZABBIX_PASSWORD (definida no .env).
# NAO hardcode senhas aqui. Veja .env.example / SECURITY.md.
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" <<-EOSQL
    CREATE DATABASE identitydb;
    CREATE DATABASE campaignsdb;
    CREATE DATABASE zabbixdb;

    CREATE USER zabbix WITH PASSWORD '${ZABBIX_PASSWORD}';
    GRANT ALL PRIVILEGES ON DATABASE zabbixdb TO zabbix;
EOSQL

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname zabbixdb <<-EOSQL
    GRANT ALL ON SCHEMA public TO zabbix;
    ALTER SCHEMA public OWNER TO zabbix;
EOSQL
