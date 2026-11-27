#!/usr/bin/env bash
# Copies the data of the core database (users, herds, animals, health events, ...) from one MySQL server to another,
# for the move of the core database from Aiven to Azure (Sprint 4, T-55). The source is only READ.
#
#   ./migrate-core-data.sh --create-schema    creates the schema in the TARGET by running the core image against it
#                                             (EF Core migrations), then stops. Changes only the target.
#   ./migrate-core-data.sh                    check only: compares both databases and reports, changes nothing
#   ./migrate-core-data.sh --apply            runs the checks and, only if all of them pass, copies the rows and then
#                                             verifies the copy (row counts and sums of the ids per table)
#
# Connection data comes from environment variables (never from arguments, so they do not show in process lists):
#   SOURCE_HOST SOURCE_USER SOURCE_PASSWORD [SOURCE_PORT=3306] [SOURCE_DATABASE=anitec-platform] [SOURCE_SSL_MODE=REQUIRED]
#   TARGET_HOST TARGET_USER TARGET_PASSWORD [TARGET_PORT=3306] [TARGET_DATABASE=anitec-platform] [TARGET_SSL_MODE=REQUIRED]
#   DOCKER_NETWORK   optional: docker network of the databases (local compose: anitec_default)
#   CORE_IMAGE       image of the core used by --create-schema (default anitec-api:local)
#
# The MySQL client runs in a mysql:8.4 container, so only Docker is needed. Rows keep their ids. The tables of the
# Subscriptions service (subscription_plans, subscriptions, payments) are NOT copied: they live in their own database
# since Sprint 3. The script never deletes anything in the source and refuses to write into a target table that has rows.
set -euo pipefail
export MSYS_NO_PATHCONV=1

MODE=check
case "${1:-}" in
  --apply) MODE=apply ;;
  --create-schema) MODE=schema ;;
  "") ;;
  *) echo "Unknown option ${1}" >&2; exit 2 ;;
esac

: "${SOURCE_HOST:?set SOURCE_HOST}" "${SOURCE_USER:?set SOURCE_USER}" "${SOURCE_PASSWORD:?set SOURCE_PASSWORD}"
: "${TARGET_HOST:?set TARGET_HOST}" "${TARGET_USER:?set TARGET_USER}" "${TARGET_PASSWORD:?set TARGET_PASSWORD}"
SOURCE_PORT="${SOURCE_PORT:-3306}"; SOURCE_DATABASE="${SOURCE_DATABASE:-anitec-platform}"; SOURCE_SSL_MODE="${SOURCE_SSL_MODE:-REQUIRED}"
TARGET_PORT="${TARGET_PORT:-3306}"; TARGET_DATABASE="${TARGET_DATABASE:-anitec-platform}"; TARGET_SSL_MODE="${TARGET_SSL_MODE:-REQUIRED}"
IMAGE="mysql:8.4"
CORE_IMAGE="${CORE_IMAGE:-anitec-api:local}"
NETWORK_ARGS=()
[ -n "${DOCKER_NETWORK:-}" ] && NETWORK_ARGS=(--network "$DOCKER_NETWORK")

# Tables that are not copied: the migrations history of the target is its own, and Subscriptions has its own database.
SKIP="'__EFMigrationsHistory','subscription_plans','subscriptions','payments'"

mysql_cmd() { # <side> reads SQL from stdin
  local side=$1 host port user pass ssl db
  if [ "$side" = src ]; then host=$SOURCE_HOST port=$SOURCE_PORT user=$SOURCE_USER pass=$SOURCE_PASSWORD ssl=$SOURCE_SSL_MODE db=$SOURCE_DATABASE
  else host=$TARGET_HOST port=$TARGET_PORT user=$TARGET_USER pass=$TARGET_PASSWORD ssl=$TARGET_SSL_MODE db=$TARGET_DATABASE; fi
  docker run --rm -i "${NETWORK_ARGS[@]}" -e MYSQL_PWD="$pass" "$IMAGE" \
    mysql -h "$host" -P "$port" -u "$user" --ssl-mode="$ssl" -D "$db" -N -B 2> >(grep -vi "warning" >&2)
}
src_sql() { printf '%s' "$1" | mysql_cmd src; }
tgt_sql() { printf '%s' "$1" | mysql_cmd tgt; }

FAILED=0
problem() { echo "  PROBLEM: $1"; FAILED=1; }

if [ "$MODE" = schema ]; then
  echo "== Creating the schema in the target with the migrations of the core ($CORE_IMAGE)"
  # Production mode (no demo data), no scanner, throwaway JWT secret: the container only applies the migrations.
  CID=$(docker run -d "${NETWORK_ARGS[@]}" -e ASPNETCORE_ENVIRONMENT=Production -e Notifications__Enabled=false \
    -e TokenSettings__Secret="migration-run-only-$(date +%s)-0123456789abcdef" \
    -e ConnectionStrings__DefaultConnection="server=$TARGET_HOST;port=$TARGET_PORT;user=$TARGET_USER;password=$TARGET_PASSWORD;database=$TARGET_DATABASE;SslMode=$TARGET_SSL_MODE" \
    "$CORE_IMAGE")
  trap 'docker rm -f "$CID" >/dev/null 2>&1 || true' EXIT
  for _ in $(seq 1 60); do
    if tgt_sql "SELECT COUNT(*) FROM __EFMigrationsHistory;" >/dev/null 2>&1; then
      sleep 5   # let the last migration finish
      echo "  migrations applied: $(tgt_sql 'SELECT COUNT(*) FROM __EFMigrationsHistory;')"
      echo "  tables: $(tgt_sql "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE();")"
      exit 0
    fi
    sleep 3
  done
  echo "The core did not create the schema in time. Container logs:" >&2; docker logs "$CID" 2>&1 | tail -20 >&2; exit 2
fi

echo "== Connection"
src_sql "SELECT CONCAT('source ok: ', DATABASE());" || { echo "Cannot read the source."; exit 2; }
tgt_sql "SELECT CONCAT('target ok: ', DATABASE());" || { echo "Cannot read the target."; exit 2; }

echo "== Tables to copy (present in both databases)"
# Only the tables that the source and the target share and that are not skipped.
SRC_TABLES=$(src_sql "SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name NOT IN ($SKIP) ORDER BY table_name;")
TGT_TABLES=$(tgt_sql "SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name NOT IN ($SKIP) ORDER BY table_name;")
[ -n "$TGT_TABLES" ] || { echo "The target has no tables: run --create-schema first." >&2; exit 2; }
TABLES=$(comm -12 <(printf '%s\n' "$SRC_TABLES" | sort) <(printf '%s\n' "$TGT_TABLES" | sort))
ONLY_SRC=$(comm -23 <(printf '%s\n' "$SRC_TABLES" | sort) <(printf '%s\n' "$TGT_TABLES" | sort) | tr '\n' ' ')
[ -z "$ONLY_SRC" ] || problem "tables only in the source (they would be lost): $ONLY_SRC"
echo "  $(printf '%s\n' "$TABLES" | tr '\n' ' ')"

echo "== Rows (source / target) and sum of the ids"
declare -A SCOUNT TCOUNT SSUM
for t in $TABLES; do
  has_id=$(tgt_sql "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = '$t' AND column_name = 'id';")
  idexpr="0"; [ "$has_id" = 1 ] && idexpr="COALESCE(SUM(id), 0)"
  SCOUNT[$t]=$(src_sql "SELECT COUNT(*) FROM \`$t\`;")
  SSUM[$t]=$(src_sql "SELECT $idexpr FROM \`$t\`;")
  TCOUNT[$t]=$(tgt_sql "SELECT COUNT(*) FROM \`$t\`;")
  printf '  %-26s source=%-6s target=%-6s\n' "$t" "${SCOUNT[$t]}" "${TCOUNT[$t]}"
  if [ "${TCOUNT[$t]}" != "0" ]; then
    problem "the target table $t already has rows; copying again would duplicate data (or it was already copied)."
  fi
done

echo "== Orphan rows in the source (they would be rejected by the foreign keys of the target)"
ORPHANS=$(src_sql "SELECT 'animals without herd', COUNT(*) FROM animals a LEFT JOIN herds h ON a.herd_id = h.id WHERE h.id IS NULL
UNION ALL SELECT 'health events without animal', COUNT(*) FROM health_events e LEFT JOIN animals a ON e.animal_id = a.id WHERE a.id IS NULL;" 2>/dev/null || true)
printf '%s\n' "$ORPHANS" | while IFS=$'\t' read -r label n; do [ -n "$label" ] && echo "  $label: $n"; done
if printf '%s\n' "$ORPHANS" | awk -F'\t' '$2 > 0 { bad = 1 } END { exit !bad }'; then problem "orphan rows in the source."; fi

if [ "$FAILED" != 0 ]; then echo; echo "Checks failed: nothing was copied."; exit 1; fi
if [ "$MODE" = check ]; then echo; echo "All checks passed. Run with --apply to copy."; exit 0; fi

echo "== Copying (mysqldump of the source -> mysql of the target)"
TABLE_ARGS=$(printf '%s ' $TABLES)
docker run --rm "${NETWORK_ARGS[@]}" -e MYSQL_PWD="$SOURCE_PASSWORD" "$IMAGE" \
  mysqldump -h "$SOURCE_HOST" -P "$SOURCE_PORT" -u "$SOURCE_USER" --ssl-mode="$SOURCE_SSL_MODE" \
  --no-create-info --skip-triggers --complete-insert --single-transaction --set-gtid-purged=OFF --column-statistics=0 \
  "$SOURCE_DATABASE" $TABLE_ARGS 2> >(grep -vi "warning" >&2) | mysql_cmd tgt
echo "  done"

echo "== Verifying the copy"
for t in $TABLES; do
  has_id=$(tgt_sql "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = '$t' AND column_name = 'id';")
  idexpr="0"; [ "$has_id" = 1 ] && idexpr="COALESCE(SUM(id), 0)"
  c=$(tgt_sql "SELECT COUNT(*) FROM \`$t\`;"); s=$(tgt_sql "SELECT $idexpr FROM \`$t\`;")
  # CHECKSUM TABLE reads the content of every row. It is reported, but a difference is only a warning: servers of
  # different MySQL versions can store the same rows differently, and the rows and ids are already compared.
  sc=$(src_sql "CHECKSUM TABLE \`$t\`;" | cut -f2); tc=$(tgt_sql "CHECKSUM TABLE \`$t\`;" | cut -f2)
  note="checksum equal"; [ "$sc" = "$tc" ] || note="WARNING checksum differs ($sc / $tc)"
  if [ "$c" = "${SCOUNT[$t]}" ] && [ "$s" = "${SSUM[$t]}" ]; then
    printf '  OK   %-26s rows=%s idsum=%s %s\n' "$t" "$c" "$s" "$note"
  else
    printf '  FAIL %-26s source rows=%s idsum=%s / target rows=%s idsum=%s\n' "$t" "${SCOUNT[$t]}" "${SSUM[$t]}" "$c" "$s"; FAILED=1
  fi
done
[ "$FAILED" = 0 ] && echo "The copy matches the source." || { echo "The copy does NOT match the source."; exit 1; }
