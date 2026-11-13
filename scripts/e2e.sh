#!/usr/bin/env bash
# Pruebas de punta a punta (Playwright) contra el entorno completo de docker compose.
#
#   bash scripts/e2e.sh              # todo: base limpia, entorno, 4 fases de pruebas, apagado
#   bash scripts/e2e.sh --dejar      # igual, pero deja el entorno levantado al terminar
#   bash scripts/e2e.sh --docker     # fuerza correr las pruebas dentro de un contenedor
#   bash scripts/e2e.sh --local      # fuerza correr las pruebas con el SDK de .NET de esta maquina
#   bash scripts/e2e.sh --sin-reset  # no borra la base (mas rapido, pero arrastra datos viejos)
#   bash scripts/e2e.sh --evidencia  # ademas copia el resumen a docs/evidencias/09-pruebas-e2e.txt
#
# Por defecto elige solo: si hay SDK de .NET 10 corre las pruebas en esta maquina, si no, dentro
# de un contenedor. En los dos casos la app corre en docker compose.
#
# Fases:
#   1. Normal       todo arriba: navegacion, formularios, permisos, roles, IA.
#   2. SinIA        con ia-api frenado: la busqueda tiene que seguir funcionando.
#   3. SinCatalogo  con catalogo-api frenado (REST: vehiculos): la app no se tiene que caer.
#   4. SinSoap      con catalogo-soap frenado (SOAP: repuestos): la busqueda avisa y no se cae.
#
# Corre SIN clave de LLM aunque el .env tenga una: responde el clasificador local, que es
# determinista, y las pruebas pueden afirmar sistema y piezas.
#
# ATENCION: borra la base de datos local de compose (docker compose down -v), salvo --sin-reset.
#
# Termina con codigo 0 si las cuatro fases pasan. Capturas, trazas, reportes .trx, la salida de
# cada fase y resumen.txt quedan en tests/RepMatch.E2E/resultados (fuera de git). Con
# --evidencia, el resumen se copia a docs/evidencias para la entrega: el total de pruebas sale de
# esa corrida y nadie lo escribe a mano en la documentacion.
set -euo pipefail

cd "$(dirname "$0")/.."
RAIZ="$(pwd)"
PROYECTO="tests/RepMatch.E2E/RepMatch.E2E.csproj"
RESULTADOS="$RAIZ/tests/RepMatch.E2E/resultados"

DEJAR=0
RESET=1
EVIDENCIA=0
MODO="auto"
for arg in "$@"; do
  case "$arg" in
    --dejar) DEJAR=1 ;;
    --sin-reset) RESET=0 ;;
    --evidencia) EVIDENCIA=1 ;;
    --docker) MODO="docker" ;;
    --local) MODO="local" ;;
    -h|--help) sed -n '2,31p' "$0"; exit 0 ;;
    *) echo "Opcion desconocida: $arg (ver --help)"; exit 2 ;;
  esac
done

paso() { printf '\n\033[1;34m== %s\033[0m\n' "$*"; }
aviso() { printf '\033[1;33m%s\033[0m\n' "$*"; }

command -v docker >/dev/null || { echo "Falta docker en el PATH."; exit 1; }
docker compose version >/dev/null || { echo "Falta docker compose (v2)."; exit 1; }

# Puertos del .env, si existe. La clave del LLM se pisa despues a proposito.
if [ -f .env ]; then set -a; . ./.env; set +a; fi
export IA_LLM_APIKEY=""
PUERTO_WEB="${PUERTO_WEB:-8080}"
PUERTO_WEB_LOCAL="${PUERTO_WEB_LOCAL:-8090}"
PUERTO_CATALOGO_SOAP="${PUERTO_CATALOGO_SOAP:-8082}"

if [ "$MODO" = "auto" ]; then
  if command -v dotnet >/dev/null && dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
    MODO="local"
  else
    MODO="docker"
  fi
fi
echo "Las pruebas corren en modo: $MODO"

compose() { docker compose --profile local "$@"; }
compose_e2e() { docker compose --profile e2e "$@"; }

apagar() {
  if [ "$DEJAR" -eq 0 ]; then
    paso "Apagando el entorno"
    compose down >/dev/null 2>&1 || true
  else
    aviso "El entorno queda levantado: Web http://localhost:$PUERTO_WEB · Local http://localhost:$PUERTO_WEB_LOCAL"
  fi
}
trap apagar EXIT

if [ "$RESET" -eq 1 ]; then
  paso "1/4 Base limpia (docker compose down -v)"
  compose down -v --remove-orphans
fi

paso "2/4 Levantando el entorno (web, web-local, catalogo-api, catalogo-soap, ia-api, postgres)"
compose up --build --wait

paso "3/4 Preparando las pruebas"
mkdir -p "$RESULTADOS"
if [ "$MODO" = "local" ]; then
  rm -rf "${RESULTADOS:?}"/*
  dotnet build "$PROYECTO" -c Release --nologo -v q
else
  compose_e2e build e2e
  # Lo que dejo una corrida anterior en el contenedor puede ser de root: se borra desde adentro.
  compose_e2e run --rm --no-deps --entrypoint sh e2e -c 'rm -rf /resultados/*'
fi

# Corre una fase sin cortar el script: devuelve el codigo de dotnet test.
correr() {
  local escenario="$1"; shift
  if [ "$MODO" = "local" ]; then
    E2E_ESCENARIO="$escenario" \
    E2E_URL_WEB="http://localhost:$PUERTO_WEB" \
    E2E_URL_WEB_LOCAL="http://localhost:$PUERTO_WEB_LOCAL"     E2E_URL_CATALOGO_SOAP="http://localhost:$PUERTO_CATALOGO_SOAP" \
    E2E_RESULTADOS="$RESULTADOS" \
      dotnet test "$PROYECTO" -c Release --no-build --nologo \
        --results-directory "$RESULTADOS" \
        --logger "trx;LogFileName=$escenario.trx" \
        --logger "console;verbosity=normal" "$@"
  else
    E2E_ESCENARIO="$escenario" compose_e2e run --rm --no-deps e2e \
      --logger "trx;LogFileName=$escenario.trx" \
      --logger "console;verbosity=normal" "$@"
  fi
}

ESTADO_Normal="-"; ESTADO_SinIA="-"; ESTADO_SinCatalogo="-"; ESTADO_SinSoap="-"
fallos=0
fase() {
  local escenario="$1"; shift
  paso "Fase $escenario"
  # Con pipefail, el codigo de la tuberia es el de dotnet test aunque tee termine bien.
  if correr "$escenario" "$@" 2>&1 | tee "$RESULTADOS/$escenario.log"; then
    eval "ESTADO_$escenario=OK"
  else
    eval "ESTADO_$escenario='CON ERRORES'"
    fallos=$((fallos + 1))
  fi
}

paso "4/4 Pruebas"
# En la fase Normal las de resiliencia se omiten solas (miran E2E_ESCENARIO).
fase Normal

compose stop ia-api
fase SinIA --filter "Escenario=SinIA"
compose up -d --wait ia-api

compose stop catalogo-api
fase SinCatalogo --filter "Escenario=SinCatalogo"
compose up -d --wait catalogo-api

compose stop catalogo-soap
fase SinSoap --filter "Escenario=SinSoap"
compose up -d --wait catalogo-soap

# En modo docker los archivos los escribio root dentro del contenedor: se devuelven al usuario.
if [ "$MODO" = "docker" ] && command -v id >/dev/null; then
  compose_e2e run --rm --no-deps --entrypoint chown e2e -R "$(id -u):$(id -g)" /resultados >/dev/null 2>&1 || true
fi

# La linea de resumen de dotnet test de cada fase, en espaniol o en ingles segun el SDK.
linea_resumen() {
  local log="$RESULTADOS/$1.log"
  local resumen
  resumen="$(grep -hE '(Correctas!|Con error!|Passed!|Failed!) +- ' "$log" 2>/dev/null \
    | sed -e 's/^ *//' -e 's/ - [^ ]*\.dll.*$//' | tail -1 || true)"
  if [ -n "$resumen" ]; then
    printf '%s' "$resumen"
  else
    grep -E '^(Pruebas totales:|Total tests:|[[:space:]]+(Correcto|Incorrecto|Omitido|Error|Passed|Failed|Skipped):)' "$log" \
      | sed 's/^[[:space:]]*//' | paste -sd ';' -
  fi
}

{
  echo "# Pruebas de punta a punta (Playwright) de RepMatch"
  echo "# Generado por scripts/e2e.sh. No editar a mano."
  echo
  echo "Fecha:   $(date '+%Y-%m-%d %H:%M %z')"
  echo "Commit:  $(git rev-parse --short HEAD 2>/dev/null || echo 'sin git')"
  echo "Modo:    pruebas en $MODO, app en docker compose (web con SOAP y REST, y web-local), sin clave de LLM"
  echo
  echo "| Fase | Estado | Resumen de dotnet test |"
  echo "|---|---|---|"
  echo "| Normal | $ESTADO_Normal | $(linea_resumen Normal) |"
  echo "| SinIA (ia-api frenado) | $ESTADO_SinIA | $(linea_resumen SinIA) |"
  echo "| SinCatalogo (catalogo-api frenado) | $ESTADO_SinCatalogo | $(linea_resumen SinCatalogo) |"
  echo "| SinSoap (catalogo-soap frenado) | $ESTADO_SinSoap | $(linea_resumen SinSoap) |"
  echo
  echo "Las omitidas de la fase Normal son las de resiliencia (corren en su fase) y las que"
  echo "documentan errores conocidos (ver tests/RepMatch.E2E/README.md)."
} > "$RESULTADOS/resumen.txt"

if [ "$EVIDENCIA" -eq 1 ]; then
  cp "$RESULTADOS/resumen.txt" "$RAIZ/docs/evidencias/09-pruebas-e2e.txt"
fi

paso "Resumen"
cat "$RESULTADOS/resumen.txt"
echo
[ "$EVIDENCIA" -eq 1 ] && echo "  Evidencia: docs/evidencias/09-pruebas-e2e.txt (para commitear con la entrega)"
echo "  Capturas:  tests/RepMatch.E2E/resultados/capturas/"
echo "  Trazas:    tests/RepMatch.E2E/resultados/trazas/   (se abren arrastrando el .zip a"
echo "             https://trace.playwright.dev, que lo procesa en el navegador sin subirlo)"
echo "  Reportes:  tests/RepMatch.E2E/resultados/*.trx"

exit "$fallos"
