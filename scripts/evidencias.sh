#!/usr/bin/env bash
# Genera las evidencias de ejecucion local y remota que pide el entregable del TP Inicial.
#
#   bash scripts/evidencias.sh
#
# Levanta Catalogo.Api, corre la Web primero en modo Local y despues en modo Remoto ejecutando
# la misma consulta, y deja los logs de los tres procesos en docs/evidencias/.
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SALIDA="$RAIZ/docs/evidencias"
API_URL="http://localhost:5081"
WEB_URL="http://localhost:5080"

mkdir -p "$SALIDA"
cd "$RAIZ"

limpiar() {
  taskkill //F //IM RepMatch.Web.exe          >/dev/null 2>&1 || true
  taskkill //F //IM RepMatch.Catalogo.Api.exe >/dev/null 2>&1 || true
  pkill -f RepMatch.Web          >/dev/null 2>&1 || true
  pkill -f RepMatch.Catalogo.Api >/dev/null 2>&1 || true
}
trap limpiar EXIT

esperar() {
  local url="$1" intentos=40
  for _ in $(seq $intentos); do
    if curl -sf -m 3 "$url/salud" >/dev/null 2>&1; then return 0; fi
    sleep 1
  done
  echo "ERROR: $url no respondio a tiempo" >&2
  return 1
}

consulta="api/repuestos/compatibles?marca=Volkswagen&modelo=Gol&anio=2015&motor=1.6"

echo "==> Compilando"
dotnet build -v q --nologo >/dev/null

limpiar
sleep 2

echo "==> Levantando Catalogo.Api en $API_URL"
ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --project src/RepMatch.Catalogo.Api --no-build --urls "$API_URL" \
  > "$SALIDA/03-catalogo-api.log" 2>&1 &
esperar "$API_URL"

echo "==> Evidencia 1: Web en modo LOCAL (invocacion directa en proceso)"
ASPNETCORE_ENVIRONMENT=Development Catalogo__Modo=Local \
  dotnet run --project src/RepMatch.Web --no-build --urls "$WEB_URL" \
  > "$SALIDA/01-web-modo-local.log" 2>&1 &
esperar "$WEB_URL"
# Se ejercita dos veces: la primera mide el arranque en frio, la segunda el regimen normal.
curl -sf -m 20 "$WEB_URL/evidencia/catalogo" >/dev/null
curl -sf -m 20 "$WEB_URL/evidencia/catalogo" | tee "$SALIDA/06-medicion-local.json"
echo
sleep 1
taskkill //F //IM RepMatch.Web.exe >/dev/null 2>&1 || pkill -f RepMatch.Web || true
sleep 3

echo "==> Evidencia 2: Web en modo REMOTO (HTTP contra Catalogo.Api)"
ASPNETCORE_ENVIRONMENT=Development Catalogo__Modo=Remoto Catalogo__UrlBaseRemota="$API_URL" \
  dotnet run --project src/RepMatch.Web --no-build --urls "$WEB_URL" \
  > "$SALIDA/02-web-modo-remoto.log" 2>&1 &
esperar "$WEB_URL"
curl -sf -m 20 "$WEB_URL/evidencia/catalogo" >/dev/null
curl -sf -m 20 "$WEB_URL/evidencia/catalogo" | tee "$SALIDA/07-medicion-remota.json"
echo
sleep 1

echo "==> Evidencia 3: llamada directa al contrato REST del componente remoto"
curl -sf -m 10 "$API_URL/$consulta" \
  | python -m json.tool > "$SALIDA/04-respuesta-rest.json" 2>/dev/null \
  || curl -sf -m 10 "$API_URL/$consulta" > "$SALIDA/04-respuesta-rest.json"

echo "==> Evidencia 4: contrato OpenAPI del componente remoto"
curl -sf -m 10 "$API_URL/openapi/v1.json" > "$SALIDA/05-openapi.json" || \
  echo "(OpenAPI solo se publica en entorno Development)" > "$SALIDA/05-openapi.json"

echo
echo "Evidencias escritas en docs/evidencias/:"
ls -1 "$SALIDA"
echo
echo "Para la evidencia visual, abri $WEB_URL/acceso y sacale una captura:"
echo "esa pagina ejecuta los dos caminos y muestra ambas latencias juntas."
