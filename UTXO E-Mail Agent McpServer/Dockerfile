# Der McpServer kommuniziert über STDIO (stdin/stdout), nicht über HTTP.
# Daher kein EXPOSE und das schlanke runtime-Image statt aspnet.
# Betrieb z. B. als Subprozess eines MCP-Clients: docker run -i <image>
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["UTXO E-Mail Agent McpServer/UTXO E-Mail Agent McpServer.csproj", "UTXO E-Mail Agent McpServer/"]
COPY ["UTXO E-Mail Agent Shared/UTXO E-Mail Agent Shared.csproj", "UTXO E-Mail Agent Shared/"]
RUN dotnet restore "UTXO E-Mail Agent McpServer/UTXO E-Mail Agent McpServer.csproj"
COPY . .
WORKDIR "/src/UTXO E-Mail Agent McpServer"
RUN dotnet build "./UTXO E-Mail Agent McpServer.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./UTXO E-Mail Agent McpServer.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UTXO E-Mail Agent McpServer.dll"]
