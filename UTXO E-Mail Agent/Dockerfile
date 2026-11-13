FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
# Der Agent bindet im Code fest auf Port 5051 (Program.cs: app.RunAsync("http://0.0.0.0:5051")).
EXPOSE 5051

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["UTXO E-Mail Agent/UTXO E-Mail Agent.csproj", "UTXO E-Mail Agent/"]
COPY ["UTXO E-Mail Agent Shared/UTXO E-Mail Agent Shared.csproj", "UTXO E-Mail Agent Shared/"]
RUN dotnet restore "UTXO E-Mail Agent/UTXO E-Mail Agent.csproj"
COPY . .
WORKDIR "/src/UTXO E-Mail Agent"
RUN dotnet build "./UTXO E-Mail Agent.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./UTXO E-Mail Agent.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UTXO E-Mail Agent.dll"]
