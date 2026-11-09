#!/usr/bin/env bash
# Builds the solution and starts: Api, desktop ground station, simulator, desktop viewer.
# Close either window (or press Ctrl+C) to stop everything.
set -euo pipefail
cd "$(dirname "$0")"

dotnet build DeviceLink.sln -c Release --nologo -v q

PIDS=()
cleanup() {
  for pid in ${PIDS[@]+"${PIDS[@]}"}; do kill "$pid" 2>/dev/null || true; done
}
trap cleanup EXIT

# Finds the built dll whatever the target framework and runs it from its folder,
# so each app finds its own appsettings.json. Sets LAST_PID.
start_app() {
  local dll
  dll=$(find "src/$1/bin/Release" -name "$1.dll" | head -n 1)
  if [ -z "$dll" ]; then echo "Build output for $1 not found" >&2; exit 1; fi
  (cd "$(dirname "$dll")" && exec dotnet "$(basename "$dll")") &
  LAST_PID=$!
  PIDS+=("$LAST_PID")
}

start_app DeviceLink.Api
sleep 3

start_app DeviceLink.GroundStation.Desktop; STATION=$LAST_PID
start_app DeviceLink.Viewer.Desktop;        VIEWER=$LAST_PID
sleep 4   # the station window must be up and listening before the drone connects

start_app DeviceLink.Simulator

while kill -0 "$STATION" 2>/dev/null && kill -0 "$VIEWER" 2>/dev/null; do sleep 1; done