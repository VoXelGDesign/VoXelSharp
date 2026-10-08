# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore layer: copy only solution + project files so source edits don't bust the cache.
COPY VoXelSharp.slnx global.json Directory.Build.props ./
COPY src/VoXelSharp.Server/VoXelSharp.Server.csproj src/VoXelSharp.Server/
COPY src/VoXelSharp.Protocol/VoXelSharp.Protocol.csproj src/VoXelSharp.Protocol/
COPY src/VoXelSharp.World/VoXelSharp.World.csproj src/VoXelSharp.World/
COPY src/VoXelSharp.PluginApi/VoXelSharp.PluginApi.csproj src/VoXelSharp.PluginApi/
RUN dotnet restore src/VoXelSharp.Server/VoXelSharp.Server.csproj

COPY src/ src/
RUN dotnet publish src/VoXelSharp.Server/VoXelSharp.Server.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Chiseled Ubuntu 24.04: no shell, non-root user `app` (UID 1654). Pure console app => runtime, not aspnet.
FROM mcr.microsoft.com/dotnet/runtime:10.0-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app/publish .
USER app
EXPOSE 25565/tcp
ENTRYPOINT ["dotnet", "VoXelSharp.Server.dll"]
