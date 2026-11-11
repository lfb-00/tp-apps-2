# RepMatch

Meta-buscador de repuestos automotrices con diagnóstico asistido por IA.
Trabajo práctico de **Desarrollo de Aplicaciones II** — UADE.

El usuario identifica su vehículo, describe el problema en lenguaje natural, y la aplicación compara
las ofertas de varios sitios reales. **No vende ni almacena stock**: normaliza, compara y deriva a la
tienda de origen.

> **Alcance de este repo: TP Inicial, Primera Parte y, de la Segunda Parte, el componente de IA, el
> servicio SOAP y los precios de tiendas reales (Easy y Frávega, por la API pública de VTEX).** La
> aplicación es el comparador. La mensajería y eBay todavía no están.
> Diagramas, informe, evidencias y patrones están en `docs/`. El seguimiento del trabajo está
> en `plan.md` y `status.md`.

## Diagnóstico por IA

El texto libre del problema ("vibra el volante cuando freno") pasa por el componente de IA, que
devuelve sistema, urgencia, causas probables y las piezas que conviene revisar. La compatibilidad
con el vehículo sigue siendo el filtro: la IA elige entre esas piezas y nunca agrega otras. Si la
IA no responde, se muestran todas las compatibles.

Hay dos fuentes, y la respuesta dice cuál contestó:

- **LLM** (`llm`): Groq o Gemini por su API compatible con OpenAI. Solo si hay clave en
  `IA__Llm__ApiKey`. El texto sale sin mails ni teléfonos.
- **Clasificador local** (`fallback-local`): TF-IDF y vecinos más cercanos sobre un corpus de
  síntomas en `src/RepMatch.IA/Datos/sintomas.json`. Responde sin red cuando no hay clave o el LLM
  falla o tarda más de `IA:Llm:TimeoutSegundos`.

Se expone por REST en `RepMatch.IA.Api` (`POST /api/diagnostico`) y la Web lo usa en proceso o
remoto según `IA:Modo`, como el catálogo. Funciona también sin sesión: el texto se diagnostica y
no se guarda.

## Precios de las tiendas

Después de mostrar el diagnóstico y las piezas, la Web busca los precios en **Easy** y **Frávega**
con la API pública de catálogo de VTEX (sin credenciales, nunca scraping). Mientras tanto cada pieza
muestra "Buscando precios…": una tienda lenta no demora el diagnóstico.

- **Cliente**: ve las ofertas de cada pieza, de la más barata a la más cara, con la tienda, quién
  vende y el precio de lista si hay descuento. "Ir a la tienda" pasa por `/derivar` (comisión).
- **Mecánico**: ve las mismas ofertas con enlace directo a la tienda, para que su clic no cuente
  como una derivación atribuida a él. Si corrige el diagnóstico, también se buscan los precios de lo
  que recomendó.
- **Sin sesión**: "desde $X en N tiendas" e "Ingresá para ver dónde".

Lo que se encuentra por nombre se advierte ("verificá que sea la medida de tu auto"). Si una tienda
no responde, la pantalla lo avisa y muestra lo de las demás. La búsqueda no informa el envío: los
precios son de referencia y el envío se calcula en la tienda. Del catálogo semilla, las tiendas
publican sobre todo baterías; para las demás piezas la pantalla dice que ninguna tienda conectada
la publica. Detalle en la sección 6.9 del [informe](docs/informe-arquitectura.md).

## Roles y modelo de negocio

Al crear la cuenta se elige un rol:

- **Cliente**: busca piezas para su auto y puede compartir una consulta (`/consultas/{id}`) con un
  mecánico del directorio para que revise el diagnóstico.
- **Mecánico**: además tiene `/taller`, con la bandeja de consultas que le compartieron, los datos
  del taller y su plan. Confirma o corrige el diagnóstico y deja una nota.

El cliente nunca paga. RepMatch gana por **comisión de afiliado** en cada salida a una tienda
(`GET /derivar/{ofertaId}` registra la derivación y redirige) y por el **plan Pro** del mecánico
(bandeja sin límite y perfil destacado; el gratuito admite 5 consultas sin revisar). El cambio de
plan no cobra: el pago queda fuera de alcance. El orden de las ofertas depende solo del precio.
Detalle en la sección 1 del [informe](docs/informe-arquitectura.md).

Cuentas de la semilla (contraseña `repMatch123!`): `blofaro@uade.edu.ar` (cliente),
`contacto@tallersanmartin.com.ar` (mecánico, plan gratuito) y `carla@motoresbenitez.com.ar`
(mecánica, plan Pro).

## Arranque rápido — solo hace falta Docker

No necesitás .NET ni PostgreSQL instalados: el SDK vive dentro de la imagen de build.

```bash
docker compose up --build --wait
```

- Web → **http://localhost:8080**
- API REST → **http://localhost:8081/salud**; contrato OpenAPI → **http://localhost:8081/openapi/v1.json** (Development)
- Catálogo SOAP → **http://localhost:8082/ConsultaCompatibilidad.svc?wsdl** (contrato WSDL)
- IA REST → **http://localhost:8083/salud**; contrato OpenAPI → **http://localhost:8083/openapi/v1.json** (Development)
- PostgreSQL → `localhost:5432`

La misma consulta por los dos caminos queda en `docs/evidencias/` (la genera
`scripts/evidencias.sh` pegándole a `GET /evidencia/catalogo`).

Las pruebas, también sin instalar nada:

```bash
docker compose --profile test run --rm tests
```

Las pruebas de punta a punta recorren la web con un navegador real (Playwright): formularios,
permisos, roles, el componente de IA y qué pasa cuando se cae la IA o el catálogo. Un solo
comando levanta el entorno, corre todo y lo apaga; **borra la base local**:

```bash
bash scripts/e2e.sh
```

Detalle, opciones y cómo leer los resultados en
[tests/RepMatch.E2E/README.md](tests/RepMatch.E2E/README.md).

Para bajar todo (`-v` borra además el volumen de la base):

```bash
docker compose down -v
```

### Si algún puerto está ocupado

Copiá `.env.example` como `.env` y cambiá solo el que te moleste. Los contenedores se hablan
entre sí por la red interna, así que esto no afecta el funcionamiento.

```bash
cp .env.example .env
```

En ese mismo `.env` va la clave del LLM (`IA_LLM_APIKEY`) si querés que diagnostique el LLM en
vez del clasificador local. El `.env` está en `.gitignore`.

### Si Docker no puede descargar las imágenes

En una red corporativa con proxy, el daemon puede quedar con una configuración que no resuelve.
Verificá si el proxy que tiene configurado responde:

```bash
systemctl show docker -p Environment
```

Si apunta a un proxy inalcanzable y tenés salida directa a internet, desactivá el drop-in:

```bash
sudo mv /etc/systemd/system/docker.service.d/proxy.conf /etc/systemd/system/docker.service.d/proxy.conf.disabled && sudo systemctl daemon-reload && sudo systemctl restart docker
```

Para volver atrás, renombralo de vuelta a `proxy.conf` y reiniciá el servicio.

El mismo proxy se inyecta en los contenedores de build desde `~/.docker/config.json`, así que si
`apt-get` o `dotnet restore` fallan dentro del build, revisá también ese archivo.

### Si usás Docker dentro de WSL y además tenés Docker Desktop

No conviven bien: la integración WSL de Docker Desktop se apropia de `/run/docker.sock` y, al
cerrarse, lo deja desenlazado — el daemon nativo sigue vivo pero el CLI deja de encontrarlo
(*"failed to connect to the docker API at unix:///var/run/docker.sock"*). Se recupera con:

```bash
sudo systemctl restart docker.socket docker.service
```

Lo más sano es desactivar la integración WSL en Docker Desktop, o no abrirlo.

## Correr sin Docker

Con el SDK de .NET 10 instalado:

```bash
dotnet build && dotnet test
```

Con la Web sola alcanza: por defecto resuelve catálogo e IA en proceso.

```bash
dotnet run --project src/RepMatch.Web --urls http://localhost:5080
```

Para el modo remoto, en otras terminales:

```bash
dotnet run --project src/RepMatch.Catalogo.Api --urls http://localhost:5081
```

```bash
dotnet run --project src/RepMatch.Catalogo.Soap --urls http://localhost:5082
```

```bash
dotnet run --project src/RepMatch.IA.Api --urls http://localhost:5083
```

Así arranca con el proveedor `InMemory`, sin necesidad de PostgreSQL.

## Estructura

```
src/
  RepMatch.Domain/          componente de dominio      · sin dependencias
  RepMatch.Common/          componente de utilidad     · validación, logging, configuración
  RepMatch.Contracts/       DTOs, ICatalogoRepuestos, ICatalogoVehiculos e IDiagnosticador
  RepMatch.Persistence/     componente de acceso a datos · EF Core
  RepMatch.Aplicacion/      lógica de negocio          · incluye CatalogoLocal y CatalogoVehiculosLocal
  RepMatch.Clientes.Rest/   adaptador remoto           · CatalogoRemoto, CatalogoVehiculosRemoto y DiagnosticadorRemoto
  RepMatch.Clientes.Soap/   adaptador SOAP             · CatalogoSoap y el proxy generado desde el WSDL
  RepMatch.IA/              componente de IA           · LLM, clasificador local y respaldo entre ambos
  RepMatch.Fuentes.Vtex/    adaptador de API externa   · FuenteVtex: precios de Easy y Frávega (VTEX)
  RepMatch.Catalogo.Api/    host REST del catálogo
  RepMatch.Catalogo.Soap/   host SOAP del catálogo (CoreWCF) · WSDL en /ConsultaCompatibilidad.svc?wsdl
  RepMatch.IA.Api/          host REST del componente de IA · sin base de datos
  RepMatch.Web/             presentación (Blazor Server)
tests/RepMatch.Tests/       pruebas
tests/RepMatch.E2E/         pruebas de punta a punta con Playwright (ver su README)
tests/RepMatch.TiendaSimulada/ simulador de tiendas VTEX para las pruebas de punta a punta
tests/fixtures/vtex/        respuestas reales grabadas de Easy y Frávega
docs/                        diagramas, informe y evidencias
scripts/evidencias.sh        genera las evidencias del entregable
```

## Acceso local vs. remoto

Dos interfaces, `ICatalogoRepuestos` e `ICatalogoVehiculos`, cada una con implementaciones local y remota intercambiables **por configuración,
sin recompilar**. Los repuestos tienen además una tercera, por SOAP. `IDiagnosticador` sigue el mismo esquema con `IA__Modo`:

```bash
Catalogo__Modo=Local   dotnet run --project src/RepMatch.Web
```

```bash
Catalogo__Modo=Remoto  dotnet run --project src/RepMatch.Web
```

```bash
Catalogo__Modo=Soap    dotnet run --project src/RepMatch.Web
```

| | Local | Remoto | Soap |
|---|---|---|---|
| Mecanismo | referencia de proyecto, en proceso | HTTP + JSON contra `Catalogo.Api` | repuestos por SOAP contra `Catalogo.Soap`, vehículos por REST |
| Latencia (en régimen, mediana) | ~3 ms | ~12 ms | ~25 ms |

En docker compose la Web arranca en modo **Soap**: cada búsqueda usa SOAP y REST.

## Base de datos

Sí, usa base de datos, con **dos proveedores intercambiables por configuración**:

| Proveedor | Cuándo se usa | Para qué sirve |
|---|---|---|
| **PostgreSQL 17** | En `docker compose` | Persistencia real, compartida entre los dos servicios, con volumen |
| **InMemory** | Por defecto fuera de Docker, y en las pruebas | Correr y demostrar la app sin instalar nada |

El acceso va por EF Core 10 con Npgsql, detrás de los repositorios declarados en el dominio. El
esquema y los datos semilla se crean solos al arrancar (`EnsureCreatedAsync` + `DatosSemilla`), así
que **no hay migraciones que correr a mano**. Es idempotente: cada grupo de datos semilla se carga
solo si todavía falta. Al incorporar una tabla o columna nueva (por ejemplo, el catálogo de
vehículos, los roles y la tabla `derivaciones`, las columnas del diagnóstico en `busquedas` o los
datos de la tienda en `ofertas`), una base existente debe recrearse con
`docker compose down -v` antes de levantar Compose otra vez.

Las entidades persistidas son `Cliente`, `Vehiculo`, `ConfiguracionVehiculo`, `Repuesto`,
`AplicacionVehiculo`, `Busqueda`, `Oferta` y `Derivacion`. El catálogo vehicular contiene las combinaciones
permitidas de marca, modelo, año y motor que consumen los selectores de la Web. Ojo con el alcance:
la base guarda el **catálogo propio y las búsquedas de los usuarios**,
no un inventario. RepMatch no almacena stock: las ofertas de las tiendas se guardan como resultado
de una búsqueda, con su fecha de captura, porque los precios envejecen rápido. Se vuelven a
consultar cuando vencen (`Ofertas:VigenciaMinutos`).

Si la base todavía no acepta conexiones cuando arranca un servicio, `InicializarBaseAsync` reintenta
hasta diez veces con espera creciente. En compose eso además está cubierto por el `healthcheck` de
`postgres` con `pg_isready`.

## Configuración

Todo se puede sobreescribir por variable de entorno (`Seccion__Clave`).

| Clave | Valores | Por defecto |
|---|---|---|
| `Catalogo:Modo` | `Local`, `Remoto` | `Local` (en compose: `Remoto`) |
| `Catalogo:UrlBaseRemota` | URL de `Catalogo.Api` | `http://localhost:5081` |
| `Catalogo:TimeoutSegundos` | timeout de las llamadas HTTP al catálogo remoto (1 a 120) | `30` |
| `Persistencia:Proveedor` | `InMemory`, `Postgres` | `InMemory` (en compose: `Postgres`) |
| `ConnectionStrings:RepMatch` | cadena Npgsql | vacía |
| `IA:Modo` | `Local`, `Remoto` (solo Web) | `Local` (en compose: `Remoto`) |
| `IA:UrlBaseRemota` | URL de `IA.Api` | `http://localhost:5083` |
| `IA:TimeoutSegundos` | timeout de la Web hacia `IA.Api` (1 a 120) | `10` |
| `IA:Llm:ApiKey` | clave del proveedor del LLM; vacía desactiva el LLM | vacía (en compose: `IA_LLM_APIKEY` del `.env`) |
| `IA:Llm:UrlBase` | API compatible con OpenAI | `https://api.groq.com/openai/v1/` |
| `IA:Llm:Modelo` | modelo del proveedor | `llama-3.3-70b-versatile` |
| `IA:Llm:TimeoutSegundos` | espera máxima al LLM antes de responder con el local (1 a 60) | `4` |
| `Ofertas:TimeoutGlobalSegundos` | tope para todas las tiendas juntas (1 a 60) | `8` |
| `Ofertas:VigenciaMinutos` | después de esto los precios se vuelven a consultar | `60` |
| `Ofertas:Tiendas:N:Nombre`, `:UrlBase`, `:Moneda`, `:PrefijosCategoria` | una entrada por tienda VTEX; sumar una tienda es sumar una entrada | Easy (`https://www.easy.com.ar`, `/Automotor/`) y Frávega (`https://www.fravega.com`, `/Autos, Motos y Otros/`) |
| `Ofertas:Tiendas:N:TimeoutSegundos`, `:PedidosSimultaneos`, `:ResultadosPorTermino`, `:CacheMinutos` | límites por tienda | `5`, `2`, `10`, `10` |
| `ProteccionDatos:DirectorioClaves` | directorio de las claves que cifran la sesión (solo Web) | vacío: las maneja ASP.NET Core (en compose: `/claves`, en un volumen) |

Y los puertos del host, vía `.env` (ver `.env.example`): `PUERTO_WEB`, `PUERTO_CATALOGO`,
`PUERTO_IA`, `PUERTO_POSTGRES`, `PUERTO_WEB_LOCAL`. En compose, `OFERTAS_URL_EASY`,
`OFERTAS_URL_FRAVEGA` y `OFERTAS_CACHE_MINUTOS` cambian las tiendas: `scripts/e2e.sh` las apunta a
`tienda-simulada`.

## Documentación

- [Informe de arquitectura](docs/informe-arquitectura.md)
- [Patrones de diseño aplicados](docs/patrones.md)
- [Diagrama de clases](docs/diagramas/clases.md)
- [Diagrama de componentes](docs/diagramas/componentes.md)
- [Diagrama de despliegue](docs/diagramas/despliegue.md)
- [Arquitectura en capas](docs/diagramas/capas.md)
- [Diagramas de secuencia (búsquedas, login, alta de vehículo, catálogo, eliminación de cuenta, diagnóstico por IA, SOAP y precios de las tiendas)](docs/diagramas/secuencia.md)

## Stack

.NET 10 · ASP.NET Core · Blazor Server · EF Core 10 · PostgreSQL 17 · Serilog · FluentValidation · xUnit
