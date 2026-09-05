# Build multi-etapa: se compila con el SDK y se publica sobre la imagen de runtime,
# que es mucho mas chica y no lleva compilador adentro.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Primero solo los .csproj: si no cambian, Docker reutiliza la capa de restore.
# No hace falta copiar la solucion: el restore parte del csproj del host.
COPY src/RepMatch.Domain/RepMatch.Domain.csproj                 src/RepMatch.Domain/
COPY src/RepMatch.Common/RepMatch.Common.csproj                 src/RepMatch.Common/
COPY src/RepMatch.Contracts/RepMatch.Contracts.csproj           src/RepMatch.Contracts/
COPY src/RepMatch.Persistence/RepMatch.Persistence.csproj       src/RepMatch.Persistence/
COPY src/RepMatch.Aplicacion/RepMatch.Aplicacion.csproj         src/RepMatch.Aplicacion/
COPY src/RepMatch.Clientes.Rest/RepMatch.Clientes.Rest.csproj   src/RepMatch.Clientes.Rest/
COPY src/RepMatch.Catalogo.Api/RepMatch.Catalogo.Api.csproj     src/RepMatch.Catalogo.Api/
RUN dotnet restore src/RepMatch.Catalogo.Api/RepMatch.Catalogo.Api.csproj

COPY src/ src/
RUN dotnet publish src/RepMatch.Catalogo.Api/RepMatch.Catalogo.Api.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app ./

EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

# Usuario sin privilegios: la imagen de ASP.NET ya trae definido "app".
USER app

ENTRYPOINT ["dotnet", "RepMatch.Catalogo.Api.dll"]
