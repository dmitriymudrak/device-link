# Builds the solution, starts the ground station, then the simulator. Ctrl+C stops both.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

dotnet build DeviceLink.sln -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$station = Start-Process dotnet -NoNewWindow -PassThru -ArgumentList `
    'src/DeviceLink.GroundStation.Runner/bin/Release/net9.0/DeviceLink.GroundStation.Runner.dll'
Start-Sleep -Seconds 2

try {
    dotnet src/DeviceLink.Simulator/bin/Release/net9.0/DeviceLink.Simulator.dll
}
finally {
    if (-not $station.HasExited) { Stop-Process -Id $station.Id }
}
