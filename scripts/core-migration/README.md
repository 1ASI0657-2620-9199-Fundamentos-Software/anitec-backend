# Move of the core database from Aiven to Azure (Sprint 4, T-55)

The free plan of Aiven retires on 2026-12-21. The core database goes to a new database (`anitec-platform`) on the Azure
MySQL Flexible Server that Subscriptions already uses, so there is no new server and almost no new cost.

`migrate-core-data.sh` copies the rows; `deploy/azure/deploy.sh` creates the database and does the cutover. The source
(Aiven) is **only read**: it is never changed, so going back is always possible until Aiven retires it.

## Order

1. `deploy.sh core-database` creates the empty database and its own user on the Azure server (`CORE_DB_PASSWORD` is generated
   in `deploy/azure/.secrets.env`).
2. `deploy.sh myip-open` opens the MySQL firewall for this computer (and `myip-close` closes it at the end).
3. `migrate-core-data.sh --create-schema` runs the core image against the empty database once, so the EF Core migrations
   create the schema (the same one that the core creates by itself on start). No demo data: Production mode.
4. `migrate-core-data.sh` (no option) compares both databases and changes nothing.
5. `migrate-core-data.sh --apply` copies the rows with `mysqldump --no-create-info`, keeps the ids and then compares, per
   table, the number of rows, the sum of the ids and `CHECKSUM TABLE`. It refuses to write into a table that has rows.
6. `deploy.sh core-db azure` points `ca-core` to the Azure database (new revision). Test login, herds, plan and a payment.
7. If anything is wrong: `deploy.sh core-db aiven` goes back at once.

## Variables

The connection data is read from environment variables, never from arguments:

```bash
export SOURCE_HOST=<aiven host> SOURCE_PORT=<port> SOURCE_USER=<user> SOURCE_PASSWORD=<password> SOURCE_DATABASE=<database>
export TARGET_HOST=<server>.mysql.database.azure.com TARGET_USER=anitec_core TARGET_PASSWORD=<CORE_DB_PASSWORD> TARGET_DATABASE=anitec-platform
export CORE_IMAGE=<registry>/anitec-api:<tag>   # only for --create-schema
```

Never write these values in a file of the repository, in the report or in a chat.

## Tested

The three steps were run against the Compose database as source and a second database of the same server as target
(14 tables, 16 animals, 7 users, 12 alerts, 32 audit entries): the copy matched in rows, ids and checksums, and a second
`--apply` was refused. The run against Aiven and Azure is described in section 5.3.4 of the report.
