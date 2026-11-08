-- Drops the tables of Subscriptions from the CORE database (anitec-platform), once the service has its own copy.
--
-- THIS IS IRREVERSIBLE and it is NOT part of any automatic migration on purpose: the core applies its migrations
-- when it starts, so a migration that drops these tables would delete them in production at the next deploy.
-- Run it by hand, against the core database only, and only after every point of README.md ("Antes de borrar") holds.
--
-- It is the same operation that "dotnet ef migrations add" generated for the model without Subscriptions
-- (DropTable payments, subscriptions, subscription_plans, in that order because of the foreign keys). The model
-- snapshot of the core already describes the model without these tables, so no migration is pending.
--
-- Usage (replace the placeholders; the password is read from the environment, never typed in the command line):
--   docker run --rm -i -e MYSQL_PWD="$CORE_PASSWORD" mysql:8.4 \
--     mysql -h <core-host> -P <port> -u <user> --ssl-mode=REQUIRED -D anitec-platform < drop-core-subscriptions-tables.sql

DROP TABLE IF EXISTS `payments`;
DROP TABLE IF EXISTS `subscriptions`;
DROP TABLE IF EXISTS `subscription_plans`;
