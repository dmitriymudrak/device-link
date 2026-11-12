#!/usr/bin/env bash
# Adds the confidential "station-runner" client (service account with the telemetry-sender role)
# to an ALREADY IMPORTED realm. Needed because `start --import-realm` skips realms that exist,
# so changes in realm-export.json do not reach a Keycloak whose database volume already has the realm.
# Idempotent: does nothing if the client already exists.
#
# Usage (Keycloak from docker-compose.yml must be running):
#   ./realms/add-station-runner.sh
set -euo pipefail
cd "$(dirname "$0")/.."

REALM=device-link-realm
CLIENT=station-runner
SECRET=${STATION_RUNNER_SECRET:-station-runner-dev-secret}

# MSYS_NO_PATHCONV stops Git Bash on Windows from rewriting /opt/... paths.
kcadm() { MSYS_NO_PATHCONV=1 docker compose exec -T keycloak /opt/keycloak/bin/kcadm.sh "$@"; }

kcadm config credentials --server http://localhost:8080 --realm master \
  --user "${KC_ADMIN:-admin}" --password "${KC_ADMIN_PASSWORD:-admin}" >/dev/null

if kcadm get clients -r "$REALM" -q clientId="$CLIENT" --fields id | grep -q '"id"'; then
  echo "Client $CLIENT already exists in $REALM, nothing to do."
  exit 0
fi

kcadm create clients -r "$REALM" -f - <<JSON
{
  "clientId": "$CLIENT",
  "name": "Headless ground station (DeviceLink.GroundStation.Runner)",
  "enabled": true,
  "clientAuthenticatorType": "client-secret",
  "secret": "$SECRET",
  "publicClient": false,
  "standardFlowEnabled": false,
  "directAccessGrantsEnabled": false,
  "serviceAccountsEnabled": true,
  "protocol": "openid-connect",
  "protocolMappers": [
    {
      "name": "realm roles",
      "protocol": "openid-connect",
      "protocolMapper": "oidc-usermodel-realm-role-mapper",
      "config": { "multivalued": "true", "claim.name": "roles", "jsonType.label": "String",
                  "access.token.claim": "true", "id.token.claim": "true", "userinfo.token.claim": "true",
                  "introspection.token.claim": "true" }
    },
    {
      "name": "dashboard-api audience",
      "protocol": "openid-connect",
      "protocolMapper": "oidc-audience-mapper",
      "config": { "included.client.audience": "dashboard-api", "access.token.claim": "true",
                  "id.token.claim": "false", "introspection.token.claim": "true" }
    }
  ]
}
JSON

kcadm add-roles -r "$REALM" --uusername "service-account-$CLIENT" --rolename telemetry-sender
echo "Client $CLIENT created with role telemetry-sender."
