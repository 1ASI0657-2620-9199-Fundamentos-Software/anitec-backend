#!/usr/bin/env bash
# Copies the subscriptions and payments of the core database into the database of the Subscriptions service.
#
#   ./copy-subscriptions-data.sh           check only: reads both databases and reports, changes nothing
#   ./copy-subscriptions-data.sh --apply   runs the checks and, only if all of them pass, copies the rows
#
# Connection data comes from environment variables (never from arguments, so they do not show in process lists):
#   SOURCE_HOST SOURCE_USER SOURCE_PASSWORD [SOURCE_PORT=3306] [SOURCE_DATABASE=anitec-platform] [SOURCE_SSL_MODE=PREFERRED]
#   TARGET_HOST TARGET_USER TARGET_PASSWORD [TARGET_PORT=3306] [TARGET_DATABASE=anitec-subscriptions] [TARGET_SSL_MODE=PREFERRED]
#   DOCKER_NETWORK   optional: docker network of the databases (local compose: anitec_default)
#
# The MySQL client runs in a mysql:8.4 container, so only Docker is needed. Rows keep their ids. The plans are NOT
# copied: the Subscriptions service creates its own (ids 1 to 3) and every plan of the source must match one of them.
# The script never deletes anything. Dropping the tables of the core is a separate, manual step (see README.md).
set -euo pipefail

APPLY=0
[ "${1:-}" = "--apply" ] && APPLY=1

: "${SOURCE_HOST:?set SOURCE_HOST}" "${SOURCE_USER:?set SOURCE_USER}" "${SOURCE_PASSWORD:?set SOURCE_PASSWORD}"
: "${TARGET_HOST:?set TARGET_HOST}" "${TARGET_USER:?set TARGET_USER}" "${TARGET_PASSWORD:?set TARGET_PASSWORD}"
SOURCE_PORT="${SOURCE_PORT:-3306}"; SOURCE_DATABASE="${SOURCE_DATABASE:-anitec-platform}"; SOURCE_SSL_MODE="${SOURCE_SSL_MODE:-PREFERRED}"
TARGET_PORT="${TARGET_PORT:-3306}"; TARGET_DATABASE="${TARGET_DATABASE:-anitec-subscriptions}"; TARGET_SSL_MODE="${TARGET_SSL_MODE:-PREFERRED}"
IMAGE="mysql:8.4"
NETWORK_ARGS=()
[ -n "${DOCKER_NETWORK:-}" ] && NETWORK_ARGS=(--network "$DOCKER_NETWORK")

# MSYS_NO_PATHCONV avoids that Git Bash rewrites arguments that look like paths.
export MSYS_NO_PATHCONV=1

# run_mysql <side> <extra docker args...> -- <command and arguments>
src_sql() { printf '%s' "$1" | docker run --rm -i "${NETWORK_ARGS[@]}" -e MYSQL_PWD="$SOURCE_PASSWORD" "$IMAGE" \
  mysql -h "$SOURCE_HOST" -P "$SOURCE_PORT" -u "$SOURCE_USER" --ssl-mode="$SOURCE_SSL_MODE" -D "$SOURCE_DATABASE" -N -B 2> >(grep -vi "warning" >&2); }
tgt_sql() { printf '%s' "$1" | docker run --rm -i "${NETWORK_ARGS[@]}" -e MYSQL_PWD="$TARGET_PASSWORD" "$IMAGE" \
  mysql -h "$TARGET_HOST" -P "$TARGET_PORT" -u "$TARGET_USER" --ssl-mode="$TARGET_SSL_MODE" -D "$TARGET_DATABASE" -N -B 2> >(grep -vi "warning" >&2); }

FAILED=0
problem() { echo "  PROBLEMA: $1"; FAILED=1; }

echo "== Conexion"
src_sql "SELECT 'origen ok: ', DATABASE();" | tr '\t' ' ' || { echo "No se pudo leer el origen."; exit 2; }
tgt_sql "SELECT 'destino ok: ', DATABASE();" | tr '\t' ' ' || { echo "No se pudo leer el destino."; exit 2; }

echo "== Filas en el origen"
S_PLANS=$(src_sql "SELECT COUNT(*) FROM subscription_plans;")
S_SUBS=$(src_sql "SELECT COUNT(*) FROM subscriptions;")
S_PAYS=$(src_sql "SELECT COUNT(*) FROM payments;")
echo "  planes=$S_PLANS suscripciones=$S_SUBS pagos=$S_PAYS"

echo "== Filas en el destino"
T_PLANS=$(tgt_sql "SELECT COUNT(*) FROM subscription_plans;")
T_SUBS=$(tgt_sql "SELECT COUNT(*) FROM subscriptions;")
T_PAYS=$(tgt_sql "SELECT COUNT(*) FROM payments;")
echo "  planes=$T_PLANS suscripciones=$T_SUBS pagos=$T_PAYS"
if [ "$T_SUBS" != "0" ] || [ "$T_PAYS" != "0" ]; then
  problem "el destino ya tiene suscripciones o pagos; copiar de nuevo duplicaria datos (o ya se copio)."
fi

echo "== Valores repetidos que el destino rechazaria (indices unicos)"
DUP_SUBS=$(src_sql "SELECT CONCAT('[', stripe_subscription_id, '] x', COUNT(*)) FROM subscriptions GROUP BY stripe_subscription_id HAVING COUNT(*) > 1;")
DUP_PAYS=$(src_sql "SELECT CONCAT('[', provider_payment_id, '] x', COUNT(*)) FROM payments GROUP BY provider_payment_id HAVING COUNT(*) > 1;")
[ -n "$DUP_SUBS" ] && problem "stripe_subscription_id repetido en suscripciones: $(echo "$DUP_SUBS" | tr '\n' ' ')"
[ -n "$DUP_PAYS" ] && problem "provider_payment_id repetido en pagos: $(echo "$DUP_PAYS" | tr '\n' ' ')"
[ -z "$DUP_SUBS" ] && [ -z "$DUP_PAYS" ] && echo "  ninguno"

echo "== Referencias huerfanas en el origen"
ORPH_SUBS=$(src_sql "SELECT COUNT(*) FROM subscriptions s LEFT JOIN subscription_plans p ON p.id = s.plan_id WHERE p.id IS NULL;")
ORPH_PAYS=$(src_sql "SELECT COUNT(*) FROM payments y LEFT JOIN subscriptions s ON s.id = y.subscription_id WHERE s.id IS NULL;")
echo "  suscripciones sin plan=$ORPH_SUBS pagos sin suscripcion=$ORPH_PAYS"
[ "$ORPH_SUBS" != "0" ] && problem "hay suscripciones que apuntan a un plan inexistente."
[ "$ORPH_PAYS" != "0" ] && problem "hay pagos que apuntan a una suscripcion inexistente."

echo "== Planes del origen frente a los del destino (id, nombre, precio, max. animales)"
PLAN_COLS="CONCAT_WS('|', id, name, price, max_animals)"
SRC_PLANS=$(src_sql "SELECT $PLAN_COLS FROM subscription_plans ORDER BY id;")
TGT_PLANS=$(tgt_sql "SELECT $PLAN_COLS FROM subscription_plans ORDER BY id;")
MISSING=$(comm -23 <(echo "$SRC_PLANS" | sort) <(echo "$TGT_PLANS" | sort) | sed '/^$/d' || true)
if [ -n "$MISSING" ]; then
  problem "planes del origen que no existen igual en el destino (decidir a mano antes de copiar): $(echo "$MISSING" | tr '\n' ' ')"
else
  echo "  todos coinciden"
fi

echo
if [ "$FAILED" != "0" ]; then
  echo "RESULTADO: hay problemas; no se copia nada."
  exit 1
fi
if [ "$APPLY" != "1" ]; then
  echo "RESULTADO: las comprobaciones pasan. Para copiar: ejecute de nuevo con --apply."
  exit 0
fi

echo "== Copia"
# The dump goes straight into the target; both tables are loaded in one transaction (--single-transaction reads a
# consistent snapshot of the source, and the target script is wrapped in a transaction by the session settings).
{
  echo "SET autocommit=0; SET foreign_key_checks=1; START TRANSACTION;"
  docker run --rm "${NETWORK_ARGS[@]}" -e MYSQL_PWD="$SOURCE_PASSWORD" "$IMAGE" \
    mysqldump -h "$SOURCE_HOST" -P "$SOURCE_PORT" -u "$SOURCE_USER" --ssl-mode="$SOURCE_SSL_MODE" \
    --no-create-info --complete-insert --skip-triggers --skip-add-locks --skip-comments --no-tablespaces \
    --single-transaction --set-gtid-purged=OFF "$SOURCE_DATABASE" subscriptions payments
  echo "COMMIT;"
} | docker run --rm -i "${NETWORK_ARGS[@]}" -e MYSQL_PWD="$TARGET_PASSWORD" "$IMAGE" \
    mysql -h "$TARGET_HOST" -P "$TARGET_PORT" -u "$TARGET_USER" --ssl-mode="$TARGET_SSL_MODE" -D "$TARGET_DATABASE"

echo "== Verificacion despues de copiar"
SUBS_COLS="CONCAT_WS('|', id, user_id, plan_id, stripe_customer_id, stripe_subscription_id, status, started_at, IFNULL(ends_at,'null'))"
PAYS_COLS="CONCAT_WS('|', id, user_id, subscription_id, amount, currency, provider, provider_payment_id, status, paid_at)"
check() { # name, table, columns
  local s t
  s=$(src_sql "SELECT COUNT(*), IFNULL(SUM(CRC32($3)),0) FROM $2;")
  t=$(tgt_sql "SELECT COUNT(*), IFNULL(SUM(CRC32($3)),0) FROM $2;")
  if [ "$s" = "$t" ]; then echo "  $1: igual (filas y suma de control: $(echo "$s" | tr '\t' ' '))"
  else echo "  PROBLEMA $1: origen=[$s] destino=[$t]"; FAILED=1; fi
}
check suscripciones subscriptions "$SUBS_COLS"
check pagos payments "$PAYS_COLS"

if [ "$FAILED" != "0" ]; then echo "RESULTADO: la verificacion fallo; revise antes de continuar."; exit 1; fi
echo "RESULTADO: copia completada y verificada. El origen no se modifico."
