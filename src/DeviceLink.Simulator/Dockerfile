# Build context: repository root.  docker build -f src/DeviceLink.Simulator/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached until a project file changes.
COPY src/DeviceLink.Protocol/DeviceLink.Protocol.csproj src/DeviceLink.Protocol/
COPY src/DeviceLink.Hosting/DeviceLink.Hosting.csproj src/DeviceLink.Hosting/
COPY src/DeviceLink.Simulator/DeviceLink.Simulator.csproj src/DeviceLink.Simulator/
RUN dotnet restore src/DeviceLink.Simulator/DeviceLink.Simulator.csproj

COPY src/DeviceLink.Protocol/ src/DeviceLink.Protocol/
COPY src/DeviceLink.Hosting/ src/DeviceLink.Hosting/
COPY src/DeviceLink.Simulator/ src/DeviceLink.Simulator/
RUN dotnet publish src/DeviceLink.Simulator/DeviceLink.Simulator.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .

# Logs go to stdout only (docker logs).
ENV LogFile__Enabled=false
USER $APP_UID
ENTRYPOINT ["dotnet", "DeviceLink.Simulator.dll"]
