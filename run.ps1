<#
.SYNOPSIS
    Same as run-desktop.ps1, but runs the Api locally instead of in Docker.

.DESCRIPTION
    Use when only Keycloak runs in Docker (`docker compose up -d`, without the apps profile).
    Builds and starts DeviceLink.Api on localhost:5100, then runs run-desktop.ps1 with the same arguments.
#>
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$listening = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners() |
    Where-Object { $_.Port -eq 5100 }
if ($listening) {
    Write-Host "Port 5100 is already in use (Api container running?). Use ./run-desktop.ps1 instead." -ForegroundColor Red
    exit 1
}

dotnet build src/DeviceLink.Api -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$dll = Get-ChildItem 'src/DeviceLink.Api/bin/Release' -Recurse -Filter 'DeviceLink.Api.dll' | Select-Object -First 1
$api = Start-Process dotnet -NoNewWindow -PassThru -WorkingDirectory $dll.DirectoryName -ArgumentList $dll.Name
try {
    for ($i = 0; $i -lt 30; $i++) {
        $up = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners() |
            Where-Object { $_.Port -eq 5100 }
        if ($up) { break }
        Start-Sleep -Seconds 1
    }
    & "$PSScriptRoot/run-desktop.ps1" @args
}
finally {
    if (-not $api.HasExited) { Stop-Process -Id $api.Id -Force }
}
