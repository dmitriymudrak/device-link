# Build context: repository root.  docker build -f src/DeviceLink.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached until a project file changes.
COPY src/DeviceLink.Hosting/DeviceLink.Hosting.csproj src/DeviceLink.Hosting/
COPY src/DeviceLink.Api/DeviceLink.Api.csproj src/DeviceLink.Api/
RUN dotnet restore src/DeviceLink.Api/DeviceLink.Api.csproj

COPY src/DeviceLink.Hosting/ src/DeviceLink.Hosting/
COPY src/DeviceLink.Api/ src/DeviceLink.Api/
RUN dotnet publish src/DeviceLink.Api/DeviceLink.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# Listen on all interfaces; logs go to stdout only (docker logs).
ENV Urls=http://+:5100 \
    ASPNETCORE_HTTP_PORTS= \
    LogFile__Enabled=false
EXPOSE 5100
USER $APP_UID
ENTRYPOINT ["dotnet", "DeviceLink.Api.dll"]
