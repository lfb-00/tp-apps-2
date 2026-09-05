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
COPY src/RepMatch.Web/RepMatch.Web.csproj                       src/RepMatch.Web/
RUN dotnet restore src/RepMatch.Web/RepMatch.Web.csproj

COPY src/ src/
RUN dotnet publish src/RepMatch.Web/RepMatch.Web.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app ./

EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

USER app

ENTRYPOINT ["dotnet", "RepMatch.Web.dll"]
