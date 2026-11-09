# Builds the solution and starts Api, ground station, simulator and the desktop viewer.
# Close the viewer window (or press Ctrl+C) to stop everything.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

dotnet build DeviceLink.sln -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

function Start-App([string]$project) {
    # Find the built dll whatever the target framework (net8.0, net10.0, ...),
    # and run from its folder so each app finds its own appsettings.json.
    $dll = Get-ChildItem "src/$project/bin/Release" -Recurse -Filter "$project.dll" | Select-Object -First 1
    if (-not $dll) { throw "Build output for $project not found" }
    Start-Process dotnet -NoNewWindow -PassThru -WorkingDirectory $dll.DirectoryName -ArgumentList $dll.Name
}

$apps = @()
try {
    $apps += Start-App 'DeviceLink.Api'
    Start-Sleep -Seconds 3
    $apps += Start-App 'DeviceLink.GroundStation.Runner'
    Start-Sleep -Seconds 1
    $apps += Start-App 'DeviceLink.Simulator'

    $viewer = Start-App 'DeviceLink.Viewer.Desktop'
    $apps += $viewer
    $viewer.WaitForExit()
}
finally {
    foreach ($app in $apps) {
        if (-not $app.HasExited) { Stop-Process -Id $app.Id -Force }
    }
}