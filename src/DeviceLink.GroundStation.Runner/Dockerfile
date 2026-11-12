# Build context: repository root.  docker build -f src/DeviceLink.GroundStation.Runner/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached until a project file changes.
COPY src/DeviceLink.Protocol/DeviceLink.Protocol.csproj src/DeviceLink.Protocol/
COPY src/DeviceLink.Hosting/DeviceLink.Hosting.csproj src/DeviceLink.Hosting/
COPY src/DeviceLink.GroundStation/DeviceLink.GroundStation.csproj src/DeviceLink.GroundStation/
COPY src/DeviceLink.GroundStation.Runner/DeviceLink.GroundStation.Runner.csproj src/DeviceLink.GroundStation.Runner/
RUN dotnet restore src/DeviceLink.GroundStation.Runner/DeviceLink.GroundStation.Runner.csproj

# GroundStation generates its gRPC client from the Api contract, so only the .proto is needed from the Api.
COPY src/DeviceLink.Api/Protos/ src/DeviceLink.Api/Protos/
COPY src/DeviceLink.Protocol/ src/DeviceLink.Protocol/
COPY src/DeviceLink.Hosting/ src/DeviceLink.Hosting/
COPY src/DeviceLink.GroundStation/ src/DeviceLink.GroundStation/
COPY src/DeviceLink.GroundStation.Runner/ src/DeviceLink.GroundStation.Runner/
RUN dotnet publish src/DeviceLink.GroundStation.Runner/DeviceLink.GroundStation.Runner.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .

# Accept the drone from other containers; logs go to stdout only (docker logs).
ENV Station__ListenAddress=0.0.0.0 \
    LogFile__Enabled=false
EXPOSE 5000
USER $APP_UID
ENTRYPOINT ["dotnet", "DeviceLink.GroundStation.Runner.dll"]
