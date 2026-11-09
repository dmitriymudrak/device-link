#!/usr/bin/env bash
# Builds the solution, starts the ground station, then the simulator. Ctrl+C stops both.
set -euo pipefail
cd "$(dirname "$0")"

dotnet build DeviceLink.sln -c Release --nologo -v q

dotnet src/DeviceLink.GroundStation.Runner/bin/Release/net9.0/DeviceLink.GroundStation.Runner.dll &
STATION=$!
trap 'kill "$STATION" 2>/dev/null || true' EXIT
sleep 2

dotnet src/DeviceLink.Simulator/bin/Release/net9.0/DeviceLink.Simulator.dll
