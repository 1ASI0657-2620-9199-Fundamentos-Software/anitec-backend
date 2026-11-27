#!/usr/bin/env bash
# Deployment of AniTec to Azure Container Apps (Sprint 3, T-43; Sprint 4 additions marked S4). Run it from Git Bash with `az` signed in.
#
#   deploy/azure/deploy.sh smoke       resource group + Log Analytics + Container Apps environment (nothing else)
#   deploy/azure/deploy.sh infra       smoke + container registry + MySQL flexible server (Subscriptions database)
#   deploy/azure/deploy.sh images      builds the three images and pushes them to the registry
#   deploy/azure/deploy.sh database    creates the Subscriptions database and its own user on the Azure MySQL server
#   deploy/azure/deploy.sh apps        creates or updates the three Container Apps (core, subscriptions, gateway)
#   deploy/azure/deploy.sh webhook     loads STRIPE_WEBHOOK_SECRET_AZURE into the Subscriptions app
#   deploy/azure/deploy.sh frontend-url   points the Stripe return pages to FRONTEND_URL (the published frontend)
#   deploy/azure/deploy.sh core-database      S4: creates the core database and its own user on the Azure MySQL server (empty)
#   deploy/azure/deploy.sh core-db azure|aiven   S4: points ca-core to the Azure MySQL database or back to Aiven (the cutover)
#   deploy/azure/deploy.sh uploads            S4: Azure Files share in ca-core (NOT supported in express environments: it fails and cleans up)
#   deploy/azure/deploy.sh alert-rule         S4: alert when the gateway answers 5xx (needs ALERT_EMAIL in the environment)
#   deploy/azure/deploy.sh stop | start       S4: scales the apps to zero and stops MySQL / starts everything again (saves credit)
#   deploy/azure/deploy.sh status      shows the gateway URL, the state of each app and the health of the system
#   deploy/azure/deploy.sh myip-open|myip-close   opens or closes the MySQL firewall for this computer
#   deploy/azure/deploy.sh destroy yes-delete-<resource group>   deletes EVERYTHING in the resource group
#
# Environment variables: FRONTEND_URL (return pages and links in e-mails), INGRESS_MODE=external|internal for ca-core and
# ca-subscriptions (default external: with internal ingress the active health checks of the gateway got 404, see 5.3.4),
# CORE_DB=aiven|azure (which database ca-core uses when `apps` creates or updates it; default aiven until the cutover).
# Secrets live only in deploy/azure/.secrets.env (ignored by git) and in Azure secrets. This script never prints them.
# Generated values (passwords, JWT secret, name suffix) are written to that same file on the first run.
set -euo pipefail
export MSYS_NO_PATHCONV=1
cd "$(dirname "$0")/../.."          # anitec-backend

SECRETS=deploy/azure/.secrets.env
LOCATION=${LOCATION:-eastus2}        # one of the regions allowed by the subscription policy
MYSQL_LOCATION=${MYSQL_LOCATION:-mexicocentral}   # eastus2 has no MySQL Flexible Server SKUs for this subscription
RG=${RG:-rg-anitec-sprint3}
LAW=log-anitec-sprint3
CAE=cae-anitec-sprint3
FRONTEND_URL=${FRONTEND_URL:-http://localhost:5173}
MYSQL_ADMIN=anitec_admin
SUBS_DB=anitec-subscriptions
SUBS_USER=anitec_subscriptions
CORE_DB_NAME=anitec-platform          # S4: database of the core on the Azure MySQL server
CORE_DB_USER=anitec_core
INGRESS_MODE=${INGRESS_MODE:-external}
CORE_DB=${CORE_DB:-aiven}

command -v docker >/dev/null 2>&1 || export PATH="$PATH:/c/Users/melga/AppData/Local/Programs/DockerDesktop/resources/bin"

val()    { [ -f "$SECRETS" ] && { tr -d '\r' < "$SECRETS" | grep -E "^$1=" | tail -1 | cut -d= -f2-; } || true; }
put()    { local tmp; tmp=$(mktemp); { [ -f "$SECRETS" ] && tr -d '\r' < "$SECRETS" | grep -vE "^$1=" || true; } > "$tmp"; printf '%s=%s\n' "$1" "$2" >> "$tmp"; cat "$tmp" > "$SECRETS"; rm -f "$tmp"; }
need()   { [ -n "$(val "$1")" ] || { echo "Missing $1 in $SECRETS" >&2; exit 1; }; }
ensure() { [ -n "$(val "$1")" ] || put "$1" "$2"; }
# Azure MySQL asks for upper case, lower case and digits; only letters and digits keep the connection strings safe.
gen_password() { python -c "import secrets;print('Aa1'+secrets.token_hex(20))"; }
gen_hex()      { python -c "import secrets;print(secrets.token_hex(32))"; }
log() { printf '\n== %s\n' "$*"; }

[ -f "$SECRETS" ] || { echo "Create $SECRETS from deploy/azure/.secrets.env.example first." >&2; exit 1; }
ensure AZURE_SUFFIX "$(python -c "import secrets;print(secrets.token_hex(3))")"
SUFFIX=$(val AZURE_SUFFIX)
ACR=acranitec$SUFFIX
STORAGE=stanitec$SUFFIX
MYSQL=mysql-anitec-$SUFFIX

az config set extension.use_dynamic_install=yes_without_prompt extension.dynamic_install_allow_preview=true core.only_show_errors=true >/dev/null
az extension add --name containerapp --upgrade -y >/dev/null 2>&1 || true

cmd_smoke() {
  log "Resource group $RG ($LOCATION)"
  az group create -n "$RG" -l "$LOCATION" --tags project=anitec sprint=3 -o none
  log "Log Analytics workspace (daily cap 0.5 GB)"
  az monitor log-analytics workspace create -g "$RG" -n "$LAW" -l "$LOCATION" --quota 0.5 --retention-time 30 -o none
  local id key
  id=$(az monitor log-analytics workspace show -g "$RG" -n "$LAW" --query customerId -o tsv)
  key=$(az monitor log-analytics workspace get-shared-keys -g "$RG" -n "$LAW" --query primarySharedKey -o tsv)
  log "Container Apps environment (Consumption)"
  az containerapp env create -g "$RG" -n "$CAE" -l "$LOCATION" --logs-workspace-id "$id" --logs-workspace-key "$key" -o none
  az containerapp env show -g "$RG" -n "$CAE" --query "{name:name,state:properties.provisioningState,domain:properties.defaultDomain,staticIp:properties.staticIp,location:location}" -o table
}

cmd_infra() {
  cmd_smoke
  log "Container registry $ACR (Basic)"
  az acr create -g "$RG" -n "$ACR" -l "$LOCATION" --sku Basic --admin-enabled true -o none
  log "MySQL flexible server $MYSQL (Burstable B1ms, MySQL 8.4, 20 GB, no high availability)"
  ensure MYSQL_ADMIN_PASSWORD "$(gen_password)"
  ensure SUBSCRIPTIONS_DB_PASSWORD "$(gen_password)"
  az mysql flexible-server create -g "$RG" -n "$MYSQL" -l "$MYSQL_LOCATION" --tier Burstable --sku-name Standard_B1ms \
    --storage-size 20 --version 8.4 --admin-user "$MYSQL_ADMIN" --admin-password "$(val MYSQL_ADMIN_PASSWORD)" \
    --public-access 0.0.0.0 --high-availability Disabled --backup-retention 7 -y -o none
  az mysql flexible-server show -g "$RG" -n "$MYSQL" --query "{name:name,state:state,version:version,sku:sku.name,fqdn:fullyQualifiedDomainName}" -o table
}

cmd_myip_open() {
  local ip; ip=$(curl -s https://api.ipify.org)
  [ -n "$ip" ] || { echo "Could not detect the public IP" >&2; exit 1; }
  az mysql flexible-server firewall-rule create -g "$RG" -n "$MYSQL" --rule-name my-computer --start-ip-address "$ip" --end-ip-address "$ip" -o none
  echo "MySQL firewall open for this computer ($ip)."
}
cmd_myip_close() {
  az mysql flexible-server firewall-rule delete -g "$RG" -n "$MYSQL" --rule-name my-computer -y -o none 2>/dev/null || true
  echo "MySQL firewall rule for this computer removed."
}

cmd_images() {
  need AIVEN_CONNECTION_STRING
  local server tag; server=$ACR.azurecr.io; tag=s4-$(git rev-parse --short HEAD)
  az acr login -n "$ACR" >/dev/null
  log "Images $server/*:$tag (linux/amd64)"
  docker build --platform linux/amd64 -t "$server/anitec-api:$tag" .
  docker build --platform linux/amd64 -t "$server/anitec-subscriptions:$tag" ./Anitec.Subscriptions
  docker build --platform linux/amd64 -t "$server/anitec-gateway:$tag" ./Anitec.Gateway
  for i in anitec-api anitec-subscriptions anitec-gateway; do docker push "$server/$i:$tag" >/dev/null; done
  put IMAGE_TAG "$tag"
  az acr repository list -n "$ACR" -o table
}

cmd_database() {
  need SUBSCRIPTIONS_DB_PASSWORD; need MYSQL_ADMIN_PASSWORD
  local fqdn; fqdn=$(az mysql flexible-server show -g "$RG" -n "$MYSQL" --query fullyQualifiedDomainName -o tsv)
  cmd_myip_open
  trap 'cmd_myip_close' EXIT
  log "Database $SUBS_DB and user $SUBS_USER on $fqdn"
  MYSQL_PWD=$(val MYSQL_ADMIN_PASSWORD) docker run --rm -i -e MYSQL_PWD mysql:8.4 mysql -h "$fqdn" -u "$MYSQL_ADMIN" --ssl-mode=REQUIRED <<SQL
CREATE DATABASE IF NOT EXISTS \`$SUBS_DB\` CHARACTER SET utf8mb4;
CREATE USER IF NOT EXISTS '$SUBS_USER'@'%' IDENTIFIED BY '$(val SUBSCRIPTIONS_DB_PASSWORD)';
ALTER USER '$SUBS_USER'@'%' IDENTIFIED BY '$(val SUBSCRIPTIONS_DB_PASSWORD)';
GRANT ALL PRIVILEGES ON \`$SUBS_DB\`.* TO '$SUBS_USER'@'%';
FLUSH PRIVILEGES;
SQL
  echo "Database and user ready."
}

cmd_core_database() {
  # S4: an empty database for the core on the same MySQL server as Subscriptions (no new server, no new cost). The schema
  # is created later by scripts/core-migration/migrate-core-data.sh --create-schema and the data copied by --apply.
  need MYSQL_ADMIN_PASSWORD
  ensure CORE_DB_PASSWORD "$(gen_password)"
  local fqdn; fqdn=$(az mysql flexible-server show -g "$RG" -n "$MYSQL" --query fullyQualifiedDomainName -o tsv)
  cmd_myip_open
  trap 'cmd_myip_close' EXIT
  log "Database $CORE_DB_NAME and user $CORE_DB_USER on $fqdn"
  MYSQL_PWD=$(val MYSQL_ADMIN_PASSWORD) docker run --rm -i -e MYSQL_PWD mysql:8.4 mysql -h "$fqdn" -u "$MYSQL_ADMIN" --ssl-mode=REQUIRED <<SQL
CREATE DATABASE IF NOT EXISTS \`$CORE_DB_NAME\` CHARACTER SET utf8mb4;
CREATE USER IF NOT EXISTS '$CORE_DB_USER'@'%' IDENTIFIED BY '$(val CORE_DB_PASSWORD)';
ALTER USER '$CORE_DB_USER'@'%' IDENTIFIED BY '$(val CORE_DB_PASSWORD)';
GRANT ALL PRIVILEGES ON \`$CORE_DB_NAME\`.* TO '$CORE_DB_USER'@'%';
FLUSH PRIVILEGES;
SQL
  echo "Database and user ready (empty). The firewall rule of this computer is removed when the command ends."
}

core_azure_conn() {
  local fqdn; fqdn=$(az mysql flexible-server show -g "$RG" -n "$MYSQL" --query fullyQualifiedDomainName -o tsv)
  echo "server=$fqdn;port=3306;user=$CORE_DB_USER;password=$(val CORE_DB_PASSWORD);database=$CORE_DB_NAME;SslMode=Required"
}

cmd_core_db() {
  # S4: the cutover. Run it only after migrate-core-data.sh --apply reported that the copy matches the source.
  # Going back is the same command with `aiven`: the Aiven database is never touched.
  case "${1:-}" in
    azure) need CORE_DB_PASSWORD; need MYSQL_ADMIN_PASSWORD
           az containerapp secret set -g "$RG" -n ca-core --secrets "db-conn=$(core_azure_conn)" -o none ;;
    aiven) need AIVEN_CONNECTION_STRING
           az containerapp secret set -g "$RG" -n ca-core --secrets "db-conn=$(val AIVEN_CONNECTION_STRING)" -o none ;;
    *) echo "Usage: deploy.sh core-db azure|aiven" >&2; exit 1 ;;
  esac
  az containerapp update -g "$RG" -n ca-core --set-env-vars "DEPLOY_STAMP=$(date +%s)" -o none   # new revision reads the secret
  echo "ca-core now uses the $1 database; a new revision is starting. Check it with: deploy.sh status"
}

# deploy_app <name> <image> <ingress> <target-port> <cpu> <memory> <min> <max> <secrets...> -- <env vars...>
deploy_app() {
  local name=$1 image=$2 ingress=$3 port=$4 cpu=$5 mem=$6 min=$7 max=$8; shift 8
  local secrets=() envs=() mode=secrets
  for a in "$@"; do if [ "$a" = "--" ]; then mode=envs; elif [ $mode = secrets ]; then secrets+=("$a"); else envs+=("$a"); fi; done
  local user pass; user=$(az acr credential show -n "$ACR" --query username -o tsv); pass=$(az acr credential show -n "$ACR" --query "passwords[0].value" -o tsv)
  if az containerapp show -g "$RG" -n "$name" >/dev/null 2>&1; then
    log "Updating $name"
    [ ${#secrets[@]} -eq 0 ] || az containerapp secret set -g "$RG" -n "$name" --secrets "${secrets[@]}" -o none
    az containerapp update -g "$RG" -n "$name" --image "$image" --min-replicas "$min" --max-replicas "$max" --set-env-vars "${envs[@]}" -o none
  else
    log "Creating $name ($ingress ingress, $min-$max replicas)"
    az containerapp create -g "$RG" -n "$name" --environment "$CAE" --image "$image" \
      --registry-server "$ACR.azurecr.io" --registry-username "$user" --registry-password "$pass" \
      --ingress "$ingress" --target-port "$port" --transport http --cpu "$cpu" --memory "$mem" \
      --min-replicas "$min" --max-replicas "$max" ${secrets[@]+--secrets "${secrets[@]}"} --env-vars "${envs[@]}" -o none
  fi
}

app_fqdn() { az containerapp show -g "$RG" -n "$1" --query properties.configuration.ingress.fqdn -o tsv; }

cmd_apps() {
  need AIVEN_CONNECTION_STRING; need STRIPE_SECRET_KEY; need SUBSCRIPTIONS_DB_PASSWORD; need IMAGE_TAG
  ensure AZURE_JWT_SECRET "$(gen_hex)"
  local server=$ACR.azurecr.io tag; tag=$(val IMAGE_TAG)
  local mysql_fqdn; mysql_fqdn=$(az mysql flexible-server show -g "$RG" -n "$MYSQL" --query fullyQualifiedDomainName -o tsv)
  local subs_conn="server=$mysql_fqdn;port=3306;user=$SUBS_USER;password=$(val SUBSCRIPTIONS_DB_PASSWORD);database=$SUBS_DB;SslMode=Required"
  local whsec; whsec=$(val STRIPE_WEBHOOK_SECRET_AZURE); [ -n "$whsec" ] || whsec=not-configured-yet

  # Subscriptions first: the core needs its address (limit of animals of the plan, S4).
  deploy_app ca-subscriptions "$server/anitec-subscriptions:$tag" "$INGRESS_MODE" 8080 0.25 0.5Gi 1 1 \
    "db-conn=$subs_conn" "jwt=$(val AZURE_JWT_SECRET)" "stripe-key=$(val STRIPE_SECRET_KEY)" "stripe-whsec=$whsec" -- \
    ASPNETCORE_ENVIRONMENT=Production ConnectionStrings__DefaultConnection=secretref:db-conn TokenSettings__Secret=secretref:jwt \
    StripeSettings__SecretKey=secretref:stripe-key StripeSettings__WebhookSecret=secretref:stripe-whsec \
    "StripeSettings__SuccessUrl=$FRONTEND_URL/subscriptions/success?session_id={CHECKOUT_SESSION_ID}" \
    "StripeSettings__CancelUrl=$FRONTEND_URL/subscriptions/cancel"
  local subs_url="https://$(app_fqdn ca-subscriptions)"

  # Which database the core uses: Aiven until the cutover (core-db azure), then Azure MySQL.
  local core_conn
  if [ "$CORE_DB" = azure ]; then need CORE_DB_PASSWORD; core_conn=$(core_azure_conn); else core_conn=$(val AIVEN_CONNECTION_STRING); fi
  # E-mail of the alerts (Resend): only when the key is in the secrets file; without it the alerts still work.
  local core_secrets=("db-conn=$core_conn" "jwt=$(val AZURE_JWT_SECRET)") core_env=(
    ASPNETCORE_ENVIRONMENT=Production ConnectionStrings__DefaultConnection=secretref:db-conn TokenSettings__Secret=secretref:jwt
    "Subscriptions__BaseUrl=$subs_url" "Notifications__AppUrl=$FRONTEND_URL")
  if [ -n "$(val RESEND_API_KEY)" ]; then
    core_secrets+=("resend-key=$(val RESEND_API_KEY)")
    core_env+=(Resend__ApiKey=secretref:resend-key)
    [ -z "$(val RESEND_FROM)" ] || core_env+=("Resend__From=$(val RESEND_FROM)")
  fi
  deploy_app ca-core "$server/anitec-api:$tag" "$INGRESS_MODE" 8080 0.5 1.0Gi 2 2 "${core_secrets[@]}" -- "${core_env[@]}"

  # Inside the environment each app has its own load balancer: the gateway reaches the core replicas through one address.
  local core_url="https://$(app_fqdn ca-core)"
  deploy_app ca-gateway "$server/anitec-gateway:$tag" external 8080 0.25 0.5Gi 1 2 \
    -- \
    Gateway__ExposeStatus=true \
    "ReverseProxy__Clusters__core__Destinations__api1__Address=$core_url" \
    "ReverseProxy__Clusters__core__Destinations__api2__Address=$core_url" \
    "ReverseProxy__Clusters__subscriptions__Destinations__subscriptions__Address=$subs_url"
  echo; echo "Gateway: https://$(app_fqdn ca-gateway)"
}

cmd_webhook() {
  need STRIPE_WEBHOOK_SECRET_AZURE
  az containerapp secret set -g "$RG" -n ca-subscriptions --secrets "stripe-whsec=$(val STRIPE_WEBHOOK_SECRET_AZURE)" -o none
  az containerapp update -g "$RG" -n ca-subscriptions --set-env-vars "DEPLOY_STAMP=$(date +%s)" -o none   # new revision reads the secret
  echo "Webhook secret loaded; a new revision of ca-subscriptions is starting."
}

cmd_frontend_url() {
  # Points the Stripe return pages of the Subscriptions service to the frontend that is published.
  # Usage: FRONTEND_URL=https://frontend.example bash deploy/azure/deploy.sh frontend-url
  case "$FRONTEND_URL" in https://*|http://localhost*) ;; *) echo "Set FRONTEND_URL to the address of the frontend (https://... or http://localhost:5173)." >&2; exit 1 ;; esac
  FRONTEND_URL=${FRONTEND_URL%/}
  log "Stripe return pages of ca-subscriptions -> $FRONTEND_URL"
  az containerapp update -g "$RG" -n ca-subscriptions --set-env-vars     "StripeSettings__SuccessUrl=$FRONTEND_URL/subscriptions/success?session_id={CHECKOUT_SESSION_ID}"     "StripeSettings__CancelUrl=$FRONTEND_URL/subscriptions/cancel" -o none
  echo "Done. The gateway allows the origins of Anitec.Gateway/appsettings.json (Cors:AllowedOrigins); add any other frontend there."
}

cmd_uploads() {
  # S4: the images that the users upload are written to wwwroot/uploads of the container, so they are lost when a
  # replica restarts and the two replicas do not share them. An Azure Files share mounted there fixes both without
  # changing the code. Costs a few cents a month.
  log "Storage account $STORAGE (Standard LRS) and file share uploads"
  az storage account create -g "$RG" -n "$STORAGE" -l "$LOCATION" --sku Standard_LRS --kind StorageV2 \
    --min-tls-version TLS1_2 --allow-blob-public-access false -o none
  local key; key=$(az storage account keys list -g "$RG" -n "$STORAGE" --query "[0].value" -o tsv)
  az storage share-rm create -g "$RG" --storage-account "$STORAGE" -n uploads --quota 5 -o none
  log "Environment storage and volume of ca-core"
  # Not supported when the environment is an "express environment" (the kind that this subscription gets): Azure answers
  # ExpressEnvironmentResourceNotSupported (found on 27/11/2026). Then the images stay in the disk of the container and the
  # way out is object storage with code changes, not a mount. The storage account that was created is removed.
  az containerapp env storage set -g "$RG" -n "$CAE" --storage-name uploads --azure-file-account-name "$STORAGE" \
    --azure-file-account-key "$key" --azure-file-share-name uploads --access-mode ReadWrite -o none || {
      echo "This environment does not accept Azure Files volumes. Removing the storage account $STORAGE." >&2
      az storage account delete -g "$RG" -n "$STORAGE" -y -o none
      exit 1
    }
  local tmp; tmp=$(mktemp --suffix=.yaml)
  az containerapp show -g "$RG" -n ca-core -o json > "$tmp"
  python - "$tmp" <<'PY'
import json, sys
path = sys.argv[1]
app = json.load(open(path))
template = app["properties"]["template"]
template["volumes"] = [{"name": "uploads", "storageType": "AzureFile", "storageName": "uploads"}]
for c in template["containers"]:
    c["volumeMounts"] = [{"volumeName": "uploads", "mountPath": "/app/wwwroot/uploads"}]
json.dump({"properties": {"template": template}}, open(path, "w"))
PY
  az containerapp update -g "$RG" -n ca-core --yaml "$tmp" -o none
  rm -f "$tmp"
  echo "ca-core mounts the share uploads in /app/wwwroot/uploads (new revision)."
}

cmd_alert_rule() {
  # S4: one alert for the error rate (TS-029): more than 5 answers 5xx of the gateway in 5 minutes. The e-mail address
  # comes from the environment (ALERT_EMAIL) and is not stored in any file of the repository.
  [ -n "${ALERT_EMAIL:-}" ] || { echo "Set ALERT_EMAIL to the address that receives the alert." >&2; exit 1; }
  local law_id; law_id=$(az monitor log-analytics workspace show -g "$RG" -n "$LAW" --query id -o tsv)
  log "Action group ag-anitec and alert rule alert-gateway-5xx"
  az monitor action-group create -g "$RG" -n ag-anitec --short-name anitec \
    --action email owner "$ALERT_EMAIL" -o none
  local ag_id; ag_id=$(az monitor action-group show -g "$RG" -n ag-anitec --query id -o tsv)
  az monitor scheduled-query create -g "$RG" -n alert-gateway-5xx --scopes "$law_id" --severity 2 \
    --evaluation-frequency 5m --window-duration 5m --action-groups "$ag_id" \
    --condition "count 'Errors5xx' > 5" \
    --condition-query Errors5xx="ContainerAppConsoleLogs_CL | where ContainerAppName_s == 'ca-gateway' | where Log_s matches regex @'responded 5[0-9][0-9] in'" \
    --description "More than 5 answers 5xx from the AniTec gateway in 5 minutes" -o none
  echo "Alert rule created."
}

cmd_stop() {
  # Saves credit: the apps scale to zero and the MySQL server is stopped (Azure starts it again by itself after 7 days).
  log "Scaling the apps to zero"
  for a in ca-gateway ca-core ca-subscriptions; do az containerapp update -g "$RG" -n "$a" --min-replicas 0 -o none; done
  log "Stopping MySQL $MYSQL"
  az mysql flexible-server stop -g "$RG" -n "$MYSQL" -o none
  echo "Stopped. The frontend will not log in until you run: deploy.sh start"
}

cmd_start() {
  log "Starting MySQL $MYSQL"
  az mysql flexible-server start -g "$RG" -n "$MYSQL" -o none
  for _ in $(seq 1 60); do [ "$(az mysql flexible-server show -g "$RG" -n "$MYSQL" --query state -o tsv)" = Ready ] && break; sleep 10; done
  log "Scaling the apps up (a new revision is forced: after a stop the replicas do not always appear by themselves)"
  local stamp; stamp=$(date +%s)
  az containerapp update -g "$RG" -n ca-subscriptions --min-replicas 1 --max-replicas 1 --set-env-vars "DEPLOY_STAMP=$stamp" -o none
  az containerapp update -g "$RG" -n ca-core --min-replicas 2 --max-replicas 2 --set-env-vars "DEPLOY_STAMP=$stamp" -o none
  az containerapp update -g "$RG" -n ca-gateway --min-replicas 1 --max-replicas 2 --set-env-vars "DEPLOY_STAMP=$stamp" -o none
  cmd_status
}

cmd_status() {
  az containerapp list -g "$RG" --query "[].{app:name,state:properties.runningStatus,ingress:properties.configuration.ingress.external,minReplicas:properties.template.scale.minReplicas}" -o table
  local gw; gw=$(app_fqdn ca-gateway || true)
  [ -n "$gw" ] || return 0
  echo; echo "Gateway: https://$gw"
  echo -n "/health            -> "; curl -s -o /dev/null -w '%{http_code}\n' "https://$gw/health"
  echo -n "/gateway/status    -> "; curl -s "https://$gw/gateway/status"; echo
}

cmd_destroy() {
  [ "${1:-}" = "yes-delete-$RG" ] || { echo "Refusing: pass 'yes-delete-$RG' to delete the resource group $RG and everything in it." >&2; exit 1; }
  az group delete -n "$RG" --yes --no-wait
  echo "Deletion of $RG started."
}

case "${1:-}" in
  smoke) cmd_smoke ;; infra) cmd_infra ;; images) cmd_images ;; database) cmd_database ;; apps) cmd_apps ;;
  core-database) cmd_core_database ;; core-db) shift; cmd_core_db "$@" ;; uploads) cmd_uploads ;; alert-rule) cmd_alert_rule ;;
  stop) cmd_stop ;; start) cmd_start ;;
  webhook) cmd_webhook ;; frontend-url) cmd_frontend_url ;; status) cmd_status ;; myip-open) cmd_myip_open ;; myip-close) cmd_myip_close ;;
  destroy) shift; cmd_destroy "$@" ;;
  *) sed -n '2,30p' "$0"; exit 1 ;;
esac
