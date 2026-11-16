# Manual de despliegue — RepMatch

Cómo levantar, verificar, operar y bajar RepMatch en un entorno de laboratorio. La solución corre
completa en contenedores con Docker Compose: no hace falta instalar .NET, PostgreSQL ni RabbitMQ.
La arquitectura de despliegue (nodos, dependencias y por qué) está en
[`diagramas/despliegue.md`](diagramas/despliegue.md).

## 1. Qué se despliega

| Servicio | Imagen | Puerto del host | Salud | Espera a |
|---|---|---|---|---|
| `web` | build de `src/RepMatch.Web` | 8080 (`PUERTO_WEB`) | `GET /salud` | postgres, redis, catalogo-api, catalogo-soap, ia-api |
| `catalogo-api` | build de `src/RepMatch.Catalogo.Api` | 8081 (`PUERTO_CATALOGO`) | `GET /salud` | postgres |
| `catalogo-soap` | build de `src/RepMatch.Catalogo.Soap` | 8082 (`PUERTO_CATALOGO_SOAP`) | `GET /salud` | postgres, catalogo-api |
| `ia-api` | build de `src/RepMatch.IA.Api` | 8083 (`PUERTO_IA`) | `GET /salud` | — |
| `worker` | build de `src/RepMatch.Notificaciones.Worker` | 8085 (`PUERTO_WORKER`) | `GET /salud` | postgres, rabbitmq, catalogo-api |
| `postgres` | `postgres:17-alpine` | 5432 (`PUERTO_POSTGRES`) | `pg_isready` | — |
| `rabbitmq` | `rabbitmq:4-management` | 5672 y 15672 (`PUERTO_RABBITMQ`, `PUERTO_RABBITMQ_UI`) | `rabbitmq-diagnostics ping` | — |
| `redis` | `redis:7-alpine` | — (solo red interna) | `redis-cli ping` | — |

Los servicios .NET declaran su `HEALTHCHECK` en el Dockerfile (con `curl` contra `/salud`) y corren
como usuario `app`, sin root. Compose no da por levantado un servicio hasta que su dependencia está
saludable. `catalogo-api` es el que crea el esquema de la base y carga los datos semilla; por eso
`catalogo-soap`, `worker` y `web-local` lo esperan.

Servicios opcionales, que se activan por perfil:

| Perfil | Servicio | Para qué |
|---|---|---|
| `local` | `web-local` (8090, `PUERTO_WEB_LOCAL`) | La Web con catálogo e IA en proceso, para comparar con la principal |
| `test` | `tests` | Las pruebas automatizadas, sin SDK |
| `carga` | `carga` | k6, lo usa `scripts/carga.sh` |
| `e2e` | `e2e` | Playwright, lo usa `scripts/e2e.sh` |
| `simulacion` | `tienda-simulada` | Tiendas VTEX simuladas para las pruebas de punta a punta |

## 2. Requisitos

- **Docker** con **Compose v2** (`docker compose version`). En Windows y macOS, Docker Desktop; en
  Linux, Docker Engine con el plugin de Compose.
- **Internet** para el primer build (imágenes base y paquetes NuGet) y para los precios de las
  tiendas. Sin salida a internet la aplicación funciona igual: muestra el diagnóstico y las piezas,
  y avisa que no pudo consultar los precios.
- **Puertos libres** en el host: 8080 a 8083, 8085, 5432, 5672 y 15672. Si alguno está ocupado, se
  cambia en el `.env` (sección 4).
- **Opcional:** una clave de LLM (Groq o Gemini, con cupo gratuito). Sin clave diagnostica el
  clasificador local.
- **Para los scripts en Windows:** Git for Windows. Los scripts `.sh` se corren con
  `.\scripts\bash.ps1`, que encuentra Git Bash y evita que `bash` resuelva al puente de WSL.

## 3. Levantar

```bash
git clone https://github.com/lfb-00/tp-apps-2.git
cd tp-apps-2
cp .env.example .env      # opcional: solo para cambiar puertos o poner la clave del LLM
docker compose up --build --wait
```

`--wait` devuelve el control cuando todos los servicios están saludables. El primer build tarda
varios minutos; los siguientes reutilizan las capas de Docker.

### Verificar

| Qué | Dónde |
|---|---|
| Aplicación web | http://localhost:8080 |
| Catálogo REST | http://localhost:8081/salud · contrato en http://localhost:8081/openapi/v1.json |
| Catálogo SOAP | http://localhost:8082/ConsultaCompatibilidad.svc?wsdl |
| IA REST | http://localhost:8083/salud · contrato en http://localhost:8083/openapi/v1.json |
| Worker | http://localhost:8085/salud · comisiones en http://localhost:8085/api/comisiones |
| RabbitMQ (administración) | http://localhost:15672, usuario y contraseña `repmatch` |
| Estado de los contenedores | `docker compose ps` (todos `healthy`) |

Los contratos OpenAPI se publican porque compose arranca los servicios en `Development`. Para
entrar a la web están las cuentas de la semilla, todas con la contraseña `repMatch123!`:
`blofaro@uade.edu.ar` (cliente), `contacto@tallersanmartin.com.ar` (mecánico, plan gratuito) y
`carla@motoresbenitez.com.ar` (mecánica, plan Pro).

Para comprobar el circuito completo: hacer una búsqueda con sesión en la web y ver en la UI de
RabbitMQ que la cola `repmatch.actividad` recibió y confirmó el mensaje; en el log del worker
aparece el evento con el mismo id de correlación que la petición de la web.

## 4. Configuración

Hay dos niveles:

- **El `.env` del host**, que lee Docker Compose. Se crea copiando `.env.example` y está en
  `.gitignore`: ahí van los puertos y los secretos.
- **La configuración de cada servicio**, que se sobreescribe por variable de entorno con el
  formato `Seccion__Clave` (por ejemplo `Catalogo__Modo=Remoto`). Compose ya fija los valores del
  entorno de laboratorio. La tabla completa de claves está en el
  [README, sección "Configuración"](../README.md#configuración).

Variables del `.env`:

| Variable | Para qué | Por defecto |
|---|---|---|
| `PUERTO_WEB`, `PUERTO_CATALOGO`, `PUERTO_CATALOGO_SOAP`, `PUERTO_IA`, `PUERTO_WORKER`, `PUERTO_WEB_LOCAL` | Puertos del host de cada servicio | 8080, 8081, 8082, 8083, 8085, 8090 |
| `PUERTO_POSTGRES`, `PUERTO_RABBITMQ`, `PUERTO_RABBITMQ_UI` | Puertos del host de la base y del broker | 5432, 5672, 15672 |
| `IA_LLM_APIKEY` | Clave del LLM. Vacía: diagnostica el clasificador local | vacía |
| `IA_LLM_URLBASE`, `IA_LLM_MODELO` | Proveedor y modelo del LLM (API compatible con OpenAI) | Groq, `openai/gpt-oss-120b` |
| `IA_LLM_ESFUERZO` | `reasoning_effort` del LLM. Vacío no lo envía (modelos que no razonan) | `low` |
| `RABBITMQ_USUARIO`, `RABBITMQ_CONTRASENA` | Credenciales del broker, para todos los servicios | `repmatch` / `repmatch` |
| `OFERTAS_URL_EASY`, `OFERTAS_URL_FRAVEGA` | URL base de cada tienda VTEX | las tiendas reales |
| `OFERTAS_CACHE_MINUTOS` | Caché de respuestas de las tiendas; 0 la desactiva | 10 |
| `EBAY_APP_ID`, `EBAY_CERT_ID` | Credenciales de producción de la Browse API de eBay. Vacías: eBay no se consulta | vacías |
| `NOTIFICACIONES_INTERVALO_SEGUNDOS` | Cada cuánto la campana de la Web busca avisos nuevos | 20 |
| `OFERTAS_INCLUIR_POR_NOMBRE` | Mostrar ofertas que coinciden solo por nombre (sin número de fabricante ni EAN) | false |
| `CATALOGO_MODO` | Cómo consulta la web el catálogo: `Soap`, `Remoto` o `Local` | `Soap` |

Los puertos solo cambian por dónde se ve cada servicio desde el host: entre ellos, los contenedores
se hablan por la red interna `repmatch`. Las credenciales de RabbitMQ son de laboratorio; en
cualquier otro entorno hay que cambiarlas.

Para usar Gemini en lugar de Groq:

```bash
IA_LLM_URLBASE=https://generativelanguage.googleapis.com/v1beta/openai/
IA_LLM_MODELO=gemini-2.5-flash
```

## 5. Modos de ejecución

| Modo | Cómo | Qué demuestra |
|---|---|---|
| Principal | `docker compose up --build --wait` | Web en modo `Soap`: repuestos por SOAP, vehículos e IA por REST, eventos por RabbitMQ |
| Catálogo por REST | `CATALOGO_MODO=Remoto` en el `.env` y volver a levantar | El mismo catálogo por el adaptador REST |
| Web en proceso | `docker compose --profile local up --build web-local` | Catálogo e IA en proceso, en http://localhost:8090 |

## 6. Base de datos

- **Esquema automático.** Al arrancar, `catalogo-api` aplica las migraciones de EF Core pendientes y
  carga los datos semilla que falten. Un bloqueo advisory de PostgreSQL serializa ese trabajo, así
  que varios servicios pueden arrancar a la vez contra la misma base sin duplicar la semilla. Si la
  base todavía no acepta conexiones, se reintenta hasta diez veces con espera creciente.
- **Bases anteriores a las migraciones.** Un volumen creado con una versión vieja se adopta solo si
  tiene completo el esquema inicial. Uno anterior a la entrega de ofertas no se puede adoptar: el
  servicio falla al arrancar con un mensaje que pide recrearlo con `docker compose down -v`.
- **Volúmenes.** `datos-postgres` (la base), `datos-rabbitmq` (colas durables y sus mensajes),
  `claves-web` y `claves-web-local` (las claves que cifran la sesión del navegador; si se borran,
  hay que volver a ingresar).
- **Conectarse a mano:** `docker compose exec postgres psql -U repmatch -d repmatch`.

En el log de `catalogo-api` de una base nueva aparece un `ERR` de `Failed executing DbCommand` sobre
`__EFMigrationsHistory`: EF Core consulta esa tabla antes de crearla. No es una falla; la línea
siguiente dice "Base de datos lista".

## 7. Operación

| Tarea | Comando |
|---|---|
| Ver el estado | `docker compose ps` |
| Seguir los logs de un servicio | `docker compose logs -f web` (o `catalogo-api`, `worker`, …) |
| Seguir una petición entre servicios | Buscar su id de correlación (cabecera `X-Correlation-Id`) en los logs: `docker compose logs \| grep <id>` |
| Reiniciar un servicio | `docker compose restart worker` |
| Aplicar cambios de código | `docker compose up --build --wait` (reconstruye solo lo que cambió) |
| Bajar todo, conservando los datos | `docker compose down` |
| Bajar todo y borrar los datos | `docker compose down -v` |

Cada servicio escribe con Serilog a la consola, en el formato `[hora nivel] [servicio] [correlación]
mensaje`, así que `docker compose logs` alcanza para seguir el sistema completo.

Ante una dependencia caída, la aplicación degrada en vez de caerse: sin IA quedan todas las piezas
compatibles, sin tiendas se ven las piezas con un aviso, y sin broker las operaciones siguen y el
fallo de publicación queda en el log.

## 8. Verificar el despliegue con pruebas

| Qué | Comando | Necesita |
|---|---|---|
| Pruebas automatizadas | `docker compose --profile test run --rm tests` | Docker |
| Pruebas de punta a punta | `bash scripts/e2e.sh` | Docker; Git Bash en Windows |
| Pruebas de carga | `bash scripts/carga.sh` | Docker |
| Evidencias de integración | `bash scripts/evidencias-integracion.sh` | Docker y Python |
| Evidencias local vs. remoto | `bash scripts/evidencias.sh` | SDK de .NET 10 |

Las pruebas de punta a punta y las evidencias de integración usan su propio proyecto de Compose
(`repmatch-e2e`, `repmatch-evidencias`), con otros puertos, nombres de contenedor y volúmenes:
pueden correr con el entorno principal levantado y al terminar borran solo sus volúmenes. En Windows, cada script se corre con
`.\scripts\bash.ps1 scripts\<nombre>.sh`.

## 9. Problemas frecuentes

| Síntoma | Causa y solución |
|---|---|
| `port is already allocated` | Otro programa usa ese puerto. Cambiarlo en el `.env` y volver a levantar. |
| Un servicio no llega a `healthy` | Ver su log con `docker compose logs <servicio>`. Si es un error de esquema, recrear la base con `docker compose down -v`. |
| La IA responde siempre con `"fuente": "fallback-local"` | No hay clave del LLM, o el proveedor falló o tardó más de 4 s. Revisar `IA_LLM_APIKEY` y el log de `ia-api`, que dice por qué conmutó. |
| No aparecen precios | Sin salida a internet o una tienda caída. La búsqueda sigue; el log de `web` registra el fallo de cada tienda. |
| El build no descarga imágenes ni paquetes | Proxy del daemon de Docker mal configurado. Ver "Si Docker no puede descargar las imágenes" en el [README](../README.md#si-docker-no-puede-descargar-las-imágenes). |
| En WSL, `failed to connect to the docker API` | Choque entre Docker Desktop y el daemon de WSL. Ver la sección correspondiente del [README](../README.md#si-usás-docker-dentro-de-wsl-y-además-tenés-docker-desktop). |

## 10. Sin Docker (desarrollo)

Con el SDK de .NET 10 instalado, la web sola alcanza: usa base en memoria, resuelve catálogo e IA en
proceso y no publica en la cola.

```bash
dotnet build && dotnet test
dotnet run --project src/RepMatch.Web --urls http://localhost:5080
```

Para el modo remoto se levantan los otros hosts en otras terminales (`Catalogo.Api` en 5081,
`Catalogo.Soap` en 5082, `IA.Api` en 5083) y se arranca la web con `Catalogo__Modo=Remoto` o
`Catalogo__Modo=Soap`. Detalle en la sección 8 del [informe](informe-arquitectura.md).
