# Builds the solution and starts: Api, desktop ground station, simulator, desktop viewer.
# Close either window (or press Ctrl+C) to stop everything.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

dotnet build DeviceLink.sln -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

function Start-App([string]$project) {
    # Find the built dll whatever the target framework, run from its folder
    # so each app finds its own appsettings.json.
    $dll = Get-ChildItem "src/$project/bin/Release" -Recurse -Filter "$project.dll" | Select-Object -First 1
    if (-not $dll) { throw "Build output for $project not found" }
    Start-Process dotnet -NoNewWindow -PassThru -WorkingDirectory $dll.DirectoryName -ArgumentList $dll.Name
}

$apps = @()
try {
    $apps += Start-App 'DeviceLink.Api'
    Start-Sleep -Seconds 3

    $station = Start-App 'DeviceLink.GroundStation.Desktop'
    $viewer = Start-App 'DeviceLink.Viewer.Desktop'
    $apps += $station, $viewer
    Start-Sleep -Seconds 4    # the station window must be up and listening before the drone connects

    $apps += Start-App 'DeviceLink.Simulator'

    while (-not $station.HasExited -and -not $viewer.HasExited) { Start-Sleep -Seconds 1 }
}
finally {
    foreach ($app in $apps) {
        if (-not $app.HasExited) { Stop-Process -Id $app.Id -Force }
    }
}