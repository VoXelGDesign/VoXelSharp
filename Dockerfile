# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore layer: copy only solution + project files so source edits don't bust the cache.
COPY McServer.slnx global.json Directory.Build.props ./
COPY src/Server/Server.csproj src/Server/
COPY src/Server.Protocol/Server.Protocol.csproj src/Server.Protocol/
COPY src/Server.World/Server.World.csproj src/Server.World/
COPY src/Server.PluginApi/Server.PluginApi.csproj src/Server.PluginApi/
RUN dotnet restore src/Server/Server.csproj

COPY src/ src/
RUN dotnet publish src/Server/Server.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Chiseled Ubuntu 24.04: no shell, non-root user `app` (UID 1654). Pure console app => runtime, not aspnet.
FROM mcr.microsoft.com/dotnet/runtime:10.0-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app/publish .
USER app
EXPOSE 25565/tcp
ENTRYPOINT ["dotnet", "Server.dll"]
