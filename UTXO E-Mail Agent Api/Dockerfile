FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["UTXO E-Mail Agent Api/UTXO E-Mail Agent Api.csproj", "UTXO E-Mail Agent Api/"]
COPY ["UTXO E-Mail Agent Shared/UTXO E-Mail Agent Shared.csproj", "UTXO E-Mail Agent Shared/"]
RUN dotnet restore "UTXO E-Mail Agent Api/UTXO E-Mail Agent Api.csproj"
COPY . .
WORKDIR "/src/UTXO E-Mail Agent Api"
RUN dotnet build "./UTXO E-Mail Agent Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./UTXO E-Mail Agent Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UTXO E-Mail Agent Api.dll"]
