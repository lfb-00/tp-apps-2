# Build multi-etapa, igual que Catalogo.Api: se compila con el SDK y se publica sobre la imagen
# de runtime.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Primero solo los .csproj: si no cambian, Docker reutiliza la capa de restore.
COPY src/RepMatch.Domain/RepMatch.Domain.csproj                 src/RepMatch.Domain/
COPY src/RepMatch.Common/RepMatch.Common.csproj                 src/RepMatch.Common/
COPY src/RepMatch.Contracts/RepMatch.Contracts.csproj           src/RepMatch.Contracts/
COPY src/RepMatch.Persistence/RepMatch.Persistence.csproj       src/RepMatch.Persistence/
COPY src/RepMatch.Aplicacion/RepMatch.Aplicacion.csproj         src/RepMatch.Aplicacion/
COPY src/RepMatch.Catalogo.Soap/RepMatch.Catalogo.Soap.csproj   src/RepMatch.Catalogo.Soap/
RUN dotnet restore src/RepMatch.Catalogo.Soap/RepMatch.Catalogo.Soap.csproj

COPY src/ src/
RUN dotnet publish src/RepMatch.Catalogo.Soap/RepMatch.Catalogo.Soap.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Las imagenes de runtime de .NET no traen curl: lo necesita el HEALTHCHECK.
USER root
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build --chown=app:app /app ./

EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

HEALTHCHECK --interval=10s --timeout=3s --start-period=25s --retries=6 \
  CMD curl -fsS http://localhost:8080/salud || exit 1

USER app

ENTRYPOINT ["dotnet", "RepMatch.Catalogo.Soap.dll"]
