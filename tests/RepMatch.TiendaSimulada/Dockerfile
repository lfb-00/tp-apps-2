# Simulador de tiendas VTEX para las pruebas de punta a punta (ver Program.cs). Lo levanta
# docker compose con el perfil "simulacion"; scripts/e2e.sh apunta la Web a este contenedor.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY tests/RepMatch.TiendaSimulada/RepMatch.TiendaSimulada.csproj tests/RepMatch.TiendaSimulada/
RUN dotnet restore tests/RepMatch.TiendaSimulada/RepMatch.TiendaSimulada.csproj

COPY tests/fixtures/ tests/fixtures/
COPY tests/RepMatch.TiendaSimulada/ tests/RepMatch.TiendaSimulada/
RUN dotnet publish tests/RepMatch.TiendaSimulada/RepMatch.TiendaSimulada.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

USER root
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build --chown=app:app /app ./

EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

HEALTHCHECK --interval=5s --timeout=3s --start-period=10s --retries=6 \
  CMD curl -fsS http://localhost:8080/salud || exit 1

USER app

ENTRYPOINT ["dotnet", "RepMatch.TiendaSimulada.dll"]
