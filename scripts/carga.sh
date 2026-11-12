#!/usr/bin/env bash
# En Windows PowerShell: .\scripts\bash.ps1 scripts\carga.sh
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$RAIZ"
export MSYS_NO_PATHCONV=1

VUS="${VUS:-10}"
DURACION="${DURACION:-30s}"
MENSAJES="${MENSAJES:-200}"
VUS_COLA="${VUS_COLA:-10}"

if [[ "${IA_CARGA_LLM:-0}" != "1" ]]; then
  export IA_LLM_APIKEY=
  echo "IA_CARGA_LLM no esta habilitada: el escenario de IA usa el clasificador local."
fi

export CATALOGO_MODO=Remoto
docker compose --profile local up --build --wait -d

for MODO in local remoto; do
  echo "==> Catalogo $MODO: $VUS VUs durante $DURACION"
  docker compose --profile carga run --rm \
    -e ESCENARIO=catalogo -e MODO="$MODO" -e VUS="$VUS" -e DURACION="$DURACION" \
    carga run --summary-export="/evidencias/carga-catalogo-$MODO.json" /scripts/carga.js
done

echo "==> Diagnostico IA: $VUS VUs durante $DURACION"
docker compose --profile carga run --rm \
  -e ESCENARIO=diagnostico -e VUS="$VUS" -e DURACION="$DURACION" \
  carga run --summary-export=/evidencias/carga-diagnostico.json /scripts/carga.js

echo "==> RabbitMQ: $MENSAJES mensajes con $VUS_COLA VUs"
docker compose --profile carga run --rm \
  -e ESCENARIO=cola -e MENSAJES="$MENSAJES" -e VUS_COLA="$VUS_COLA" \
  carga run --summary-export=/evidencias/carga-cola.json /scripts/carga.js

echo "Mediciones k6 guardadas en docs/evidencias/carga-*.json."
