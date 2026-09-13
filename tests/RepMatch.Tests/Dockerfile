# Runner de pruebas: permite correr las 54 pruebas sin tener el SDK de .NET instalado.
#
#   docker compose --profile test run --rm tests
#
# Se queda en la imagen del SDK a proposito (no publica a runtime): necesita el compilador
# y el corredor de xUnit.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/RepMatch.Domain/RepMatch.Domain.csproj                 src/RepMatch.Domain/
COPY src/RepMatch.Common/RepMatch.Common.csproj                 src/RepMatch.Common/
COPY src/RepMatch.Contracts/RepMatch.Contracts.csproj           src/RepMatch.Contracts/
COPY src/RepMatch.Persistence/RepMatch.Persistence.csproj       src/RepMatch.Persistence/
COPY src/RepMatch.Aplicacion/RepMatch.Aplicacion.csproj         src/RepMatch.Aplicacion/
COPY src/RepMatch.Clientes.Rest/RepMatch.Clientes.Rest.csproj   src/RepMatch.Clientes.Rest/
COPY src/RepMatch.Catalogo.Api/RepMatch.Catalogo.Api.csproj     src/RepMatch.Catalogo.Api/
COPY src/RepMatch.Web/RepMatch.Web.csproj                       src/RepMatch.Web/
COPY tests/RepMatch.Tests/RepMatch.Tests.csproj                 tests/RepMatch.Tests/
RUN dotnet restore tests/RepMatch.Tests/RepMatch.Tests.csproj

COPY src/ src/
COPY tests/ tests/
RUN dotnet build tests/RepMatch.Tests/RepMatch.Tests.csproj -c Release --no-restore

# Las pruebas no tocan la red ni la base: usan el proveedor InMemory y hospedan
# Catalogo.Api en memoria con WebApplicationFactory.
ENTRYPOINT ["dotnet", "test", "tests/RepMatch.Tests/RepMatch.Tests.csproj", \
            "-c", "Release", "--no-build", "--nologo"]
