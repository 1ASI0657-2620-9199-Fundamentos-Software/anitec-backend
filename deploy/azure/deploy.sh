#!/usr/bin/env bash
# Deployment of AniTec to Azure Container Apps (Sprint 3, T-43). Run it from Git Bash with `az` signed in.
#
#   deploy/azure/deploy.sh smoke       resource group + Log Analytics + Container Apps environment (nothing else)
#   deploy/azure/deploy.sh infra       smoke + container registry + MySQL flexible server (Subscriptions database)
#   deploy/azure/deploy.sh images      builds the three images and pushes them to the registry
#   deploy/azure/deploy.sh database    creates the Subscriptions database and its own user on the Azure MySQL server
#   deploy/azure/deploy.sh apps        creates or updates the three Container Apps (core, subscriptions, gateway)
#   deploy/azure/deploy.sh webhook     loads STRIPE_WEBHOOK_SECRET_AZURE into the Subscriptions app
#   deploy/azure/deploy.sh frontend-url   points the Stripe return pages to FRONTEND_URL (the published frontend)
#   deploy/azure/deploy.sh status      shows the gateway URL, the state of each app and the health of the system
#   deploy/azure/deploy.sh myip-open|myip-close   opens or closes the MySQL firewall for this computer
#   deploy/azure/deploy.sh destroy yes-delete-<resource group>   deletes EVERYTHING in the resource group
#
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
  local server tag; server=$ACR.azurecr.io; tag=s3-$(git rev-parse --short HEAD)
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
  local subs_fqdn; subs_fqdn=$(az mysql flexible-server show -g "$RG" -n "$MYSQL" --query fullyQualifiedDomainName -o tsv)
  local subs_conn="server=$subs_fqdn;port=3306;user=$SUBS_USER;password=$(val SUBSCRIPTIONS_DB_PASSWORD);database=$SUBS_DB;SslMode=Required"
  local whsec; whsec=$(val STRIPE_WEBHOOK_SECRET_AZURE); [ -n "$whsec" ] || whsec=not-configured-yet

  deploy_app ca-core "$server/anitec-api:$tag" internal 8080 0.5 1.0Gi 2 2 \
    "db-conn=$(val AIVEN_CONNECTION_STRING)" "jwt=$(val AZURE_JWT_SECRET)" -- \
    ASPNETCORE_ENVIRONMENT=Production ConnectionStrings__DefaultConnection=secretref:db-conn TokenSettings__Secret=secretref:jwt

  deploy_app ca-subscriptions "$server/anitec-subscriptions:$tag" internal 8080 0.25 0.5Gi 1 1 \
    "db-conn=$subs_conn" "jwt=$(val AZURE_JWT_SECRET)" "stripe-key=$(val STRIPE_SECRET_KEY)" "stripe-whsec=$whsec" -- \
    ASPNETCORE_ENVIRONMENT=Production ConnectionStrings__DefaultConnection=secretref:db-conn TokenSettings__Secret=secretref:jwt \
    StripeSettings__SecretKey=secretref:stripe-key StripeSettings__WebhookSecret=secretref:stripe-whsec \
    "StripeSettings__SuccessUrl=$FRONTEND_URL/subscriptions/success?session_id={CHECKOUT_SESSION_ID}" \
    "StripeSettings__CancelUrl=$FRONTEND_URL/subscriptions/cancel"

  # Inside the environment each app has its own load balancer: the gateway reaches the core replicas through one address.
  local core_url="https://$(app_fqdn ca-core)" subs_url="https://$(app_fqdn ca-subscriptions)"
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
  webhook) cmd_webhook ;; frontend-url) cmd_frontend_url ;; status) cmd_status ;; myip-open) cmd_myip_open ;; myip-close) cmd_myip_close ;;
  destroy) shift; cmd_destroy "$@" ;;
  *) sed -n '2,17p' "$0"; exit 1 ;;
esac
