# Pruebas de punta a punta (Playwright) dentro de un contenedor, para correrlas sin el SDK de
# .NET instalado. Las lanza scripts/e2e.sh con:
#
#   docker compose --profile e2e run --rm e2e
#
# Corren contra los contenedores web y web-local por la red de compose. Se queda en la imagen
# del SDK a proposito: necesita el corredor de xUnit y PowerShell (viene en la imagen) para
# instalar Chromium con sus dependencias del sistema.
FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /src

# El proyecto no referencia a ningun otro de la solucion: maneja la app solo por el navegador.
COPY tests/RepMatch.E2E/RepMatch.E2E.csproj tests/RepMatch.E2E/
RUN dotnet restore tests/RepMatch.E2E/RepMatch.E2E.csproj

COPY tests/RepMatch.E2E/ tests/RepMatch.E2E/
RUN dotnet build tests/RepMatch.E2E/RepMatch.E2E.csproj -c Release --no-restore

# Chromium y las bibliotecas del sistema que necesita, en la imagen: la corrida no descarga nada.
RUN pwsh tests/RepMatch.E2E/bin/Release/net10.0/playwright.ps1 install --with-deps chromium

ENV E2E_RESULTADOS=/resultados

ENTRYPOINT ["dotnet", "test", "tests/RepMatch.E2E/RepMatch.E2E.csproj", \
            "-c", "Release", "--no-build", "--nologo", "--results-directory", "/resultados"]
