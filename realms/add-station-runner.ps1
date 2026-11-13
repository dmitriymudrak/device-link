<#
.SYNOPSIS
    Adds the confidential "station-runner" client (service account with the telemetry-sender role)
    to an ALREADY IMPORTED realm.

.DESCRIPTION
    `start --import-realm` skips realms that already exist, so changes in realm-export.json do not reach
    a Keycloak whose database volume already has the realm. Idempotent: does nothing if the client exists.
    Keycloak from docker-compose.yml must be running.

.EXAMPLE
    ./realms/add-station-runner.ps1
#>
param(
    [string]$Secret = $(if ($env:STATION_RUNNER_SECRET) { $env:STATION_RUNNER_SECRET } else { 'station-runner-dev-secret' }),
    [string]$AdminUser = 'admin',
    [string]$AdminPassword = 'admin'
)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

$Realm = 'device-link-realm'
$Client = 'station-runner'

function Invoke-Kcadm {
    $output = docker compose exec -T keycloak /opt/keycloak/bin/kcadm.sh @args
    if ($LASTEXITCODE -ne 0) { throw "kcadm.sh $($args -join ' ') failed" }
    $output
}

Invoke-Kcadm config credentials --server http://localhost:8080 --realm master --user $AdminUser --password $AdminPassword | Out-Null

$existing = Invoke-Kcadm get clients -r $Realm -q "clientId=$Client" --fields id
if (($existing -join '') -match '"id"') {
    Write-Host "Client $Client already exists in $Realm, nothing to do."
    exit 0
}

$definition = @{
    clientId                  = $Client
    name                      = 'Headless ground station (DeviceLink.GroundStation.Runner)'
    enabled                   = $true
    clientAuthenticatorType   = 'client-secret'
    secret                    = $Secret
    publicClient              = $false
    standardFlowEnabled       = $false
    directAccessGrantsEnabled = $false
    serviceAccountsEnabled    = $true
    protocol                  = 'openid-connect'
    protocolMappers           = @(
        @{
            name           = 'realm roles'
            protocol       = 'openid-connect'
            protocolMapper = 'oidc-usermodel-realm-role-mapper'
            config         = @{
                'multivalued' = 'true'; 'claim.name' = 'roles'; 'jsonType.label' = 'String'
                'access.token.claim' = 'true'; 'id.token.claim' = 'true'; 'userinfo.token.claim' = 'true'
                'introspection.token.claim' = 'true'
            }
        },
        @{
            name           = 'dashboard-api audience'
            protocol       = 'openid-connect'
            protocolMapper = 'oidc-audience-mapper'
            config         = @{
                'included.client.audience' = 'dashboard-api'; 'access.token.claim' = 'true'
                'id.token.claim' = 'false'; 'introspection.token.claim' = 'true'
            }
        }
    )
} | ConvertTo-Json -Depth 5

# Pass the JSON as a file: piping from Windows PowerShell adds a BOM that kcadm rejects.
$tmp = Join-Path ([IO.Path]::GetTempPath()) "$Client-client.json"
[IO.File]::WriteAllText($tmp, $definition, (New-Object System.Text.UTF8Encoding $false))
try {
    docker compose cp $tmp "keycloak:/tmp/$Client-client.json" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "docker compose cp failed" }
    Invoke-Kcadm create clients -r $Realm -f "/tmp/$Client-client.json" | Out-Null
}
finally {
    Remove-Item $tmp -ErrorAction SilentlyContinue
}
Invoke-Kcadm add-roles -r $Realm --uusername "service-account-$Client" --rolename telemetry-sender | Out-Null
Write-Host "Client $Client created with role telemetry-sender."
