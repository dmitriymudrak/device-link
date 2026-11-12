<#
.SYNOPSIS
    Starts the desktop apps on the host: ground station window, viewer window and a local drone simulator.

.DESCRIPTION
    Expects Keycloak (localhost:8080) and the Api (localhost:5100) to be running already, e.g.:
        docker compose --profile apps up -d
    The simulator is started only after you have logged in to the station (its drone port opens then).
    Close either window (or press Ctrl+C) to stop everything this script started.

.PARAMETER SkipBuild
    Do not build; run the existing Release output.

.PARAMETER NoSimulator
    Do not start the local simulator.
#>
param(
    [switch]$SkipBuild,
    [switch]$NoSimulator
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$KeycloakUrl = 'http://localhost:8080/realms/device-link-realm/.well-known/openid-configuration'
$ApiPort = 5100
$StationPort = 5000
$Projects = 'DeviceLink.GroundStation.Desktop', 'DeviceLink.Viewer.Desktop', 'DeviceLink.Simulator'

function Test-Port([int]$port) {
    # Look at listening sockets instead of connecting: a probe connection to the station port
    # would be taken by the station as a drone connecting.
    $listeners = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()
    return [bool]($listeners | Where-Object { $_.Port -eq $port })
}

function Start-App([string]$project) {
    # Find the built dll whatever the target framework, run from its folder
    # so each app finds its own appsettings.json.
    $dll = Get-ChildItem "src/$project/bin/Release" -Recurse -Filter "$project.dll" | Select-Object -First 1
    if (-not $dll) { throw "Build output for $project not found (run without -SkipBuild)" }
    Write-Host "Starting $project"
    Start-Process dotnet -NoNewWindow -PassThru -WorkingDirectory $dll.DirectoryName -ArgumentList $dll.Name
}

# 1. Infrastructure must be up.
try { Invoke-WebRequest $KeycloakUrl -UseBasicParsing -TimeoutSec 3 | Out-Null }
catch {
    Write-Host "Keycloak is not reachable at localhost:8080. Start it first:" -ForegroundColor Red
    Write-Host "    docker compose --profile apps up -d"
    exit 1
}
if (-not (Test-Port $ApiPort)) {
    Write-Host "Api is not listening on localhost:$ApiPort. Start it first:" -ForegroundColor Red
    Write-Host "    docker compose --profile apps up -d"
    exit 1
}
if (Test-Port $StationPort) {
    Write-Host "Port $StationPort is already in use; the desktop station needs it for the drone." -ForegroundColor Red
    exit 1
}

# 2. Build.
if (-not $SkipBuild) {
    foreach ($project in $Projects) {
        dotnet build "src/$project" -c Release --nologo -v q
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
}

# 3. Run.
$apps = @()
try {
    $station = Start-App 'DeviceLink.GroundStation.Desktop'
    $viewer = Start-App 'DeviceLink.Viewer.Desktop'
    $apps += $station, $viewer

    if (-not $NoSimulator) {
        Write-Host "Log in to the station in the browser; the simulator starts once the station listens on port $StationPort..."
        while (-not $station.HasExited -and -not (Test-Port $StationPort)) { Start-Sleep -Seconds 1 }
        if (-not $station.HasExited) { $apps += Start-App 'DeviceLink.Simulator' }
    }

    while (-not $station.HasExited -and -not $viewer.HasExited) { Start-Sleep -Seconds 1 }
}
finally {
    foreach ($app in $apps) {
        if (-not $app.HasExited) { Stop-Process -Id $app.Id -Force }
    }
}
