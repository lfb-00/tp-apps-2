#!/usr/bin/env bash
# Genera las evidencias de integracion de la Segunda Parte que no cubre scripts/evidencias.sh:
# contratos OpenAPI de IA.Api y del Worker, una respuesta real del componente de IA, el recorrido
# de un evento por RabbitMQ (productor -> cola -> consumidor) y una consulta a la API publica de
# VTEX en las dos tiendas configuradas.
#
#   bash scripts/evidencias-integracion.sh
#   .\scripts\bash.ps1 scripts\evidencias-integracion.sh      (Windows PowerShell)
#
# A diferencia de evidencias.sh no necesita el SDK de .NET: levanta los servicios con Docker Compose
# en un proyecto propio (repmatch-evidencias), con puertos y volumenes aparte, y lo baja al final
# borrando solo sus volumenes. Si IA_LLM_APIKEY esta definida (entorno o .env) la IA responde con
# el LLM; si no, con el clasificador local, y la respuesta lo dice en "fuente".
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SALIDA="$RAIZ/docs/evidencias"
cd "$RAIZ"
mkdir -p "$SALIDA"

docker compose version >/dev/null || { echo "Falta docker compose (v2)."; exit 1; }
PY="$(command -v python || command -v python3 || true)"
[ -n "$PY" ] || { echo "Falta Python (se usa para dar formato a los JSON)."; exit 1; }

export COMPOSE_PROJECT_NAME="${EVIDENCIAS_COMPOSE_PROJECT_NAME:-repmatch-evidencias}"
export REPMATCH_CONTAINER_PREFIX="${COMPOSE_PROJECT_NAME}-"
export PUERTO_CATALOGO="${EVIDENCIAS_PUERTO_CATALOGO:-28081}"
export PUERTO_CATALOGO_SOAP="${EVIDENCIAS_PUERTO_CATALOGO_SOAP:-28082}"
export PUERTO_IA="${EVIDENCIAS_PUERTO_IA:-28083}"
export PUERTO_POSTGRES="${EVIDENCIAS_PUERTO_POSTGRES:-25432}"
export PUERTO_RABBITMQ="${EVIDENCIAS_PUERTO_RABBITMQ:-35672}"
export PUERTO_RABBITMQ_UI="${EVIDENCIAS_PUERTO_RABBITMQ_UI:-35673}"
export PUERTO_WORKER="${EVIDENCIAS_PUERTO_WORKER:-28085}"

API_URL="http://localhost:$PUERTO_CATALOGO"
IA_URL="http://localhost:$PUERTO_IA"
WORKER_URL="http://localhost:$PUERTO_WORKER"
RABBIT_URL="http://localhost:$PUERTO_RABBITMQ_UI"
RABBIT_CRED="${RABBITMQ_USUARIO:-repmatch}:${RABBITMQ_CONTRASENA:-repmatch}"
SERVICIOS=(catalogo-api ia-api rabbitmq worker)

compose() { docker compose --project-name "$COMPOSE_PROJECT_NAME" "$@"; }
bajar() { compose down -v --remove-orphans >/dev/null 2>&1 || true; }
trap bajar EXIT

# Da formato a un JSON de stdin con el fragmento de Python que recibe (d = el JSON leido).
# Salida UTF-8 con LF, igual en Windows y en Linux.
json() {
  PYTHONUTF8=1 "$PY" -c "
import sys, json
sys.stdout.reconfigure(encoding='utf-8', newline='\n')
d = json.load(sys.stdin)
$1
print(json.dumps(d, ensure_ascii=False, indent=2))"
}

logs() { compose logs --no-color --no-log-prefix "$1"; }

echo "==> Levantando ${SERVICIOS[*]} en el proyecto $COMPOSE_PROJECT_NAME"
bajar
compose up --build --wait "${SERVICIOS[@]}"

echo "==> Evidencia 15-16: contratos OpenAPI de IA.Api y del Worker"
curl -sf -m 10 "$IA_URL/openapi/v1.json" | json "" > "$SALIDA/15-openapi-ia.json"
curl -sf -m 10 "$WORKER_URL/openapi/v1.json" | json "" > "$SALIDA/16-openapi-worker.json"

echo "==> Evidencia 17-18: el componente de IA diagnostica un texto libre (POST /api/diagnostico)"
# Los candidatos salen del catalogo, como hace ServicioDiagnostico: la IA elige entre piezas reales.
VEHICULO='{"marca":"Volkswagen","modelo":"Gol","anio":2015,"motor":"1.6"}'
TEXTO="Cuando freno escucho un chirrido metalico y el pedal vibra un poco"
solicitud="$(curl -sf -m 10 "$API_URL/api/repuestos/compatibles?marca=Volkswagen&modelo=Gol&anio=2015&motor=1.6" \
  | json "d = {'texto': '$TEXTO', 'vehiculo': json.loads('$VEHICULO'),
     'candidatos': [{'codigo': r['codigoCanonico'], 'nombre': r['nombre'], 'sistema': r['sistema']} for r in d]}")"
respuesta="$(curl -sf -m 30 -X POST "$IA_URL/api/diagnostico" \
  -H "Content-Type: application/json" -H "X-Correlation-Id: evidencia-ia" -d "$solicitud")"
printf '{"solicitud": %s, "respuesta": %s}' "$solicitud" "$respuesta" | json "" > "$SALIDA/17-respuesta-ia.json"
logs ia-api > "$SALIDA/18-ia-api.log"

echo "==> Evidencia 19-22: un evento recorre la cola (Catalogo.Api -> RabbitMQ -> Worker)"
cliente="$(curl -sf -m 10 "$API_URL/api/clientes" \
  | "$PY" -c "import sys, json; print(next(c['id'] for c in json.load(sys.stdin) if c['rol'] == 'Cliente'))")"
curl -sf -m 30 -X POST "$API_URL/api/busquedas" \
  -H "Content-Type: application/json" -H "X-Correlation-Id: evidencia-cola" \
  -d "{\"clienteId\":\"$cliente\",\"vehiculo\":$VEHICULO,\"textoLibre\":\"$TEXTO\"}" \
  | json "" > "$SALIDA/19-busqueda-creada.json"

# El worker consume en segundo plano: se espera a que el evento aparezca en su log. En la cola el
# evento viaja con su clave de ruteo (busqueda.creada), que es lo que registra ConsumidorActividad.
for _ in $(seq 30); do
  logs worker | grep -q "busqueda.creada" && break
  sleep 1
done
logs catalogo-api > "$SALIDA/20-productor-catalogo-api.log"
logs worker > "$SALIDA/21-consumidor-worker.log"
grep -q "busqueda.creada" "$SALIDA/21-consumidor-worker.log" \
  || { echo "ERROR: el worker no consumio busqueda.creada en 30 s" >&2; exit 1; }

curl -sf -m 10 -u "$RABBIT_CRED" "$RABBIT_URL/api/queues" | json "
e = lambda q, k: (q.get('message_stats') or {}).get(k, 0)
d = [{'cola': q['name'], 'consumidores': q.get('consumers', 0), 'mensajes_pendientes': q.get('messages', 0),
      'publicados': e(q, 'publish'), 'entregados': e(q, 'deliver_get'), 'confirmados': e(q, 'ack')}
     for q in sorted(d, key=lambda q: q['name'])]" > "$SALIDA/22-colas-rabbitmq.json"

echo "==> Evidencia 23: API publica de VTEX en Easy y Fravega (la misma que consulta FuenteVtex)"
# Llamada directa al endpoint oficial, con el mismo User-Agent que usa la app. Se guardan solo los
# campos que lee LectorVtex: nombre, marca, EAN, precio y enlace.
vtex() {
  local tienda="$1" url="$2/api/catalog_system/pub/products/search?ft=bateria%20auto&_from=0&_to=2"
  curl -sf -m 20 -H "User-Agent: RepMatch/1.0 (comparador de repuestos)" "$url" | json "
d = {'tienda': '$tienda', 'url': '$url', 'resultados': [
  {'nombre': p.get('productName'), 'marca': p.get('brand'),
   'ean': (p.get('items') or [{}])[0].get('ean'),
   'precio': ((p.get('items') or [{}])[0].get('sellers') or [{}])[0].get('commertialOffer', {}).get('Price'),
   'enlace': p.get('link')} for p in d]}"
}
{
  echo "["
  vtex Easy "${OFERTAS_URL_EASY:-https://www.easy.com.ar}"
  echo ","
  vtex Fravega "${OFERTAS_URL_FRAVEGA:-https://www.fravega.com}"
  echo "]"
} | json "" > "$SALIDA/23-vtex-tiendas.json"

echo
echo "Evidencias escritas en docs/evidencias/ (15 a 23)."
echo "IA: fuente $(json "d = d['respuesta']['fuente']" < "$SALIDA/17-respuesta-ia.json")"
