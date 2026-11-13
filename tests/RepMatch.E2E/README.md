# Pruebas de punta a punta (Playwright)

Recorren la web con un navegador real, como lo haría una persona: completan formularios, se
equivocan a propósito, entran a páginas que no les corresponden y verifican que la app reaccione
bien. Reemplazan la prueba manual de toda la web antes de aprobar un PR.

## Cómo correrlas

Solo hace falta **Docker** (con `docker compose` v2) y **bash** (Linux, macOS, WSL o Git Bash).

```bash
bash scripts/e2e.sh
```

El script hace todo solo:

1. Borra la base local (`docker compose down -v`) para arrancar siempre de la semilla.
2. Levanta el entorno completo: `web` (8080, repuestos por SOAP y vehículos e IA por REST),
   `web-local` (modo Local, 8090), `catalogo-api`, `catalogo-soap`, `ia-api`, `tienda-simulada` y `postgres`.
3. Corre las pruebas en cinco fases:
   - **Normal**: todo arriba. Como la Web busca los repuestos por SOAP, cada búsqueda prueba SOAP.
   - **SinIA**: frena `ia-api` y comprueba que la búsqueda sigue funcionando.
   - **SinCatalogo**: frena `catalogo-api` (REST) y comprueba que la app no se cae.
   - **SinSoap**: frena `catalogo-soap` y comprueba que la búsqueda avisa y la app no se cae.
   - **SinTiendas**: frena `tienda-simulada` y comprueba que se ven las piezas con un aviso, sin precios.
4. Apaga el entorno y muestra un resumen. Termina con código 0 si las cinco fases pasan.

Los precios no salen de las tiendas reales: la Web consulta `tienda-simulada`
(`tests/RepMatch.TiendaSimulada`), que responde como VTEX con búsquedas reales grabadas de Easy y
Frávega (`tests/fixtures/vtex/`). Así las pruebas pueden afirmar precios exactos aunque las tiendas
cambien lo que publican. Para la prueba sin Docker se levanta igual:

```bash
dotnet run --project tests/RepMatch.TiendaSimulada --urls http://localhost:5084
Ofertas__Tiendas__0__UrlBase=http://localhost:5084/easy \
Ofertas__Tiendas__1__UrlBase=http://localhost:5084/fravega \
Ofertas__Tiendas__0__CacheMinutos=0 Ofertas__Tiendas__1__CacheMinutos=0 \
  dotnet run --project src/RepMatch.Web --urls http://localhost:5080
```

Si la máquina tiene el SDK de .NET 10, las pruebas corren directamente en ella (más rápido). Si
no, corren dentro de un contenedor (`docker compose --profile e2e`), con Chromium ya instalado en
la imagen. La primera corrida tarda más: arma las imágenes y descarga Chromium.

Siempre corren **sin clave de LLM**, aunque el `.env` tenga una. Así responde el clasificador
local, que da siempre la misma respuesta, y las pruebas pueden afirmar qué sistema y qué piezas
aparecen.

| Opción | Qué hace |
|---|---|
| `--dejar` | No apaga el entorno al terminar, para mirar algo a mano. |
| `--sin-reset` | No borra la base. Más rápido, pero arrastra datos de corridas anteriores. |
| `--evidencia` | Copia el resumen a `docs/evidencias/09-pruebas-e2e.txt` para la entrega. |
| `--docker` | Fuerza correr las pruebas dentro de un contenedor. |
| `--local` | Fuerza correr las pruebas con el SDK de la máquina. |

Si un puerto está ocupado, se cambia en el `.env` (`PUERTO_WEB`, `PUERTO_WEB_LOCAL`) y el script
lo respeta.

### Sin el script

Con la app levantada de cualquier forma, desde la raíz del repo:

```bash
dotnet test tests/RepMatch.E2E
```

Por defecto apunta a `http://localhost:5080` (Web) y `http://localhost:5090` (Web en modo
Local), que son los puertos de `dotnet run`. Variables para cambiarlo:

| Variable | Por defecto | Para qué |
|---|---|---|
| `E2E_URL_WEB` | `http://localhost:5080` | Web principal |
| `E2E_URL_WEB_LOCAL` | `http://localhost:5090` | Web en modo Local; si no responde, esa prueba se omite |
| `E2E_URL_CATALOGO_SOAP` | `http://localhost:5082` | Servicio SOAP, para pedirle el WSDL |
| `E2E_ESCENARIO` | `Normal` | `SinIA`, `SinCatalogo`, `SinSoap` o `SinTiendas` para las pruebas de resiliencia |
| `E2E_VENTANA` | `0` | `1` abre el navegador visible y en cámara lenta |
| `E2E_RESULTADOS` | `bin/.../resultados` | Carpeta de capturas y trazas |

Una sola prueba, con el navegador visible:

```bash
E2E_VENTANA=1 dotnet test tests/RepMatch.E2E --filter "FullyQualifiedName~El_plan_gratuito_rechaza"
```

## Cómo leer los resultados

Todo queda en `tests/RepMatch.E2E/resultados/` (está en `.gitignore`):

- `capturas/`: cómo quedó la pantalla al final de cada prueba. Si la prueba usa dos navegadores
  (cliente y mecánico), hay una captura por cada uno (`-1`, `-2`).
- `trazas/`: la grabación paso a paso de cada prueba, con capturas, DOM y red de cada acción. Se
  abre arrastrando el `.zip` a <https://trace.playwright.dev> (lo procesa en el navegador, no lo
  sube a ningún servidor).
- `Normal.trx`, `SinIA.trx`, `SinCatalogo.trx`, `SinSoap.trx`, `SinTiendas.trx`: los reportes de cada fase, y los
  `.log` con el mismo nombre, con la salida de consola.
- `resumen.txt`: el resultado de las cinco fases. Con `bash scripts/e2e.sh --evidencia` se copia a
  `docs/evidencias/09-pruebas-e2e.txt` para la entrega (el informe lo enlaza en la sección 5.5).

Además de lo que afirma cada prueba, **todas fallan si la web muestra el cartel "Ocurrió un error
inesperado"**: una excepción no atrapada en el servidor es un error aunque la prueba haya llegado
al final.

## Qué cubren

| Archivo | Qué prueba |
|---|---|
| `AutenticacionTests` | Ingreso, registro como cliente y mecánico, errores de cada campo, email repetido, cerrar sesión, recargar |
| `BusquedaTests` | Búsqueda con y sin sesión, validaciones, chips, consulta guardada, HTML en el texto, doble clic |
| `IATests` | Tarjeta de diagnóstico, piezas elegidas, urgencia, diagnóstico guardado, texto no reconocido, problema sin piezas, Web Local vs. Web principal (SOAP) |
| `CatalogoYGarageTests` | Catálogo completo y filtrado, vehículo sin piezas, alta en el garage, vehículo repetido |
| `PerfilTests` | Datos, foto (válida, no imagen, más de 5 MB), vehículo predeterminado, tema, contraseña, baja de la cuenta |
| `PermisosTests` | Páginas sin sesión, cliente en `/taller`, consulta ajena, mecánico no asignado, datos privados, botón atrás tras salir, `/derivar` inexistente |
| `MecanicoTests` | Compartir, confirmar, corregir, dejar de compartir, límite del plan gratuito, directorio Pro primero, baja del mecánico, datos del taller |
| `InterfazTests` | Tema, menú y búsqueda en celular, rutas viejas, `/salud`, correlation id |
| `OfertasTests` | Precios con los tres roles: el cliente ve las ofertas ordenadas y deriva por `/derivar`, el mecánico ve enlaces directos, sin sesión se ve "desde $X en N tiendas"; las piezas que recomienda el mecánico al corregir tienen precio; pieza sin ofertas; tiendas caídas (fase SinTiendas) |
| `ServicioSoapTests` | El WSDL se descarga con sus cuatro operaciones; una llamada SOAP cruda (un sobre XML por POST) responde |
| `ResilienciaTests` | IA caída, catálogo REST caído y servicio SOAP caído (solo en sus fases). Las tiendas caídas están en `OfertasTests` |

No miden la **exactitud** de la IA: eso es una prueba aparte sobre frases etiquetadas.

## Errores conocidos

Las pruebas que encontraron un error real están marcadas con `Skip` y un comentario que explica
la causa y un arreglo posible. Así la corrida queda en verde, pero el problema sigue a la vista
como "omitida". Al arreglarlo, se saca el `Skip`.

| Prueba | Error |
|---|---|
| `BusquedaTests.Doble_clic_en_buscar_crea_una_sola_consulta` | Dos clics rápidos en "Diagnosticar con IA" crean dos consultas iguales. |
| `MecanicoTests.Sin_recargar_el_cliente_puede_compartir_apenas_el_mecanico_pasa_a_Pro` | El cliente ve datos viejos del mecánico hasta recargar la página. |
| `MecanicoTests.Sin_recargar_el_cliente_ve_la_revision_que_acaba_de_enviar_el_mecanico` | Mismo origen: sin recargar, el cliente no ve la revisión recién enviada. |
| `ResilienciaTests.Con_el_catalogo_caido_ver_el_catalogo_completo_muestra_un_mensaje` | Con el catálogo caído, "Ver catálogo completo" tira la app. |
| `ResilienciaTests.Con_el_catalogo_caido_la_portada_avisa_que_no_esta_disponible` | Con el catálogo caído, el selector de vehículo queda vacío sin avisar. |

Los dos de datos viejos vienen de que, en Blazor Server, el `DbContext` vive lo que dura la
conexión de la pestaña, y EF devuelve las entidades que ya tiene cargadas sin releerlas.

## Relación con los issues

- **#24 (pruebas de integración del flujo completo):** esta suite cubre la parte que ya existe:
  el recorrido desde la web, los caminos de falla de la IA y del catálogo, y que la Web asigna y
  respeta el `X-Correlation-Id`. Falta lo que todavía no está construido: la cola (broker caído,
  consumo por el worker) y seguir el correlation id a través de la cola. Las fuentes externas de
  ofertas ya están cubiertas contra el simulador, con la fase SinTiendas como camino de falla. Esa parte va con Testcontainers en `tests/RepMatch.Tests`, cuando existan esas piezas.
- **#36 (no escribir el total de pruebas a mano):** esta suite no lo escribe en ningún lado; su
  resultado sale de la corrida (`resumen.txt` y `--evidencia`).

## Por qué no está en `RepMatch.slnx`

Necesita la app levantada. Si estuviera en la solución, `dotnet test` en la raíz fallaría sin el
entorno, y CLAUDE.md pide que ese comando corra solo, sin Docker. Por eso se corre aparte, por
ruta. Por el mismo motivo no referencia ningún proyecto de la solución: maneja la app solo
desde el navegador.
