# RepMatch

Meta-buscador de repuestos automotrices con diagnóstico asistido por IA.
Trabajo práctico de **Desarrollo de Aplicaciones II** — UADE.

El usuario identifica su vehículo, describe el problema en lenguaje natural, y la aplicación compara
las ofertas de varios sitios reales. **No vende ni almacena stock**: normaliza, compara y deriva a la
tienda de origen.

> **Entrega actual: TP Inicial** — arquitectura de componentes, aplicación multicapa y acceso local
> vs. remoto. El diagnóstico por IA, el servicio SOAP y la mensajería llegan en las entregas
> siguientes.

## Arranque rápido — solo hace falta Docker

No necesitás .NET ni PostgreSQL instalados: el SDK vive dentro de la imagen de build.

```bash
docker compose up --build --wait
```

- Web → **http://localhost:8080**
- Catálogo (REST + OpenAPI) → **http://localhost:8081/salud**
- PostgreSQL → `localhost:5432`

Abrí **http://localhost:8080/acceso**: esa página ejecuta la misma consulta por los dos caminos
—invocación directa en proceso e invocación remota HTTP— y muestra ambas latencias juntas.

Las 54 pruebas, también sin instalar nada:

```bash
docker compose --profile test run --rm tests
```

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

En dos terminales:

```bash
dotnet run --project src/RepMatch.Catalogo.Api --urls http://localhost:5081
```

```bash
dotnet run --project src/RepMatch.Web --urls http://localhost:5080
```

Así arranca con el proveedor `InMemory`, sin necesidad de PostgreSQL.

## Estructura

```
src/
  RepMatch.Domain/          componente de dominio      · sin dependencias
  RepMatch.Common/          componente de utilidad     · validación, logging, configuración
  RepMatch.Contracts/       DTOs e ICatalogoRepuestos
  RepMatch.Persistence/     componente de acceso a datos · EF Core
  RepMatch.Aplicacion/      lógica de negocio          · incluye CatalogoLocal
  RepMatch.Clientes.Rest/   adaptador remoto           · incluye CatalogoRemoto
  RepMatch.Catalogo.Api/    host REST del catálogo
  RepMatch.Web/             presentación (Blazor Server)
tests/RepMatch.Tests/       54 pruebas
docs/                        diagramas, informe y evidencias
scripts/evidencias.sh        genera las evidencias del entregable
```

## Acceso local vs. remoto

Una interfaz, `ICatalogoRepuestos`, con dos implementaciones intercambiables **por configuración,
sin recompilar**:

```bash
Catalogo__Modo=Local   dotnet run --project src/RepMatch.Web
```

```bash
Catalogo__Modo=Remoto  dotnet run --project src/RepMatch.Web
```

| | Local | Remoto |
|---|---|---|
| Mecanismo | referencia de proyecto, en proceso | HTTP + JSON contra `Catalogo.Api` |
| Latencia (en caliente) | ~1,9 ms | ~12,6 ms |

## Base de datos

Sí, usa base de datos, con **dos proveedores intercambiables por configuración**:

| Proveedor | Cuándo se usa | Para qué sirve |
|---|---|---|
| **PostgreSQL 17** | En `docker compose` | Persistencia real, compartida entre los dos servicios, con volumen |
| **InMemory** | Por defecto fuera de Docker, y en las pruebas | Correr y demostrar la app sin instalar nada |

El acceso va por EF Core 10 con Npgsql, detrás de los repositorios declarados en el dominio. El
esquema y los datos semilla se crean solos al arrancar (`EnsureCreatedAsync` + `DatosSemilla`), así
que **no hay migraciones que correr a mano**. Es idempotente: si el catálogo ya existe, no lo toca.

Las entidades persistidas son `Cliente`, `Vehiculo`, `Repuesto`, `AplicacionVehiculo`, `Busqueda` y
`Oferta`. Ojo con el alcance: la base guarda el **catálogo propio y las búsquedas de los usuarios**,
no un inventario. RepMatch no almacena stock — las ofertas de las tiendas externas se capturan como
resultado de una búsqueda, con su fecha de captura, porque los precios envejecen rápido.

Si la base todavía no acepta conexiones cuando arranca un servicio, `InicializarBaseAsync` reintenta
hasta diez veces con espera creciente. En compose eso además está cubierto por el `healthcheck` de
`postgres` con `pg_isready`.

## Configuración

Todo se puede sobreescribir por variable de entorno (`Seccion__Clave`).

| Clave | Valores | Por defecto |
|---|---|---|
| `Catalogo:Modo` | `Local`, `Remoto` | `Local` (en compose: `Remoto`) |
| `Catalogo:UrlBaseRemota` | URL de `Catalogo.Api` | `http://localhost:5081` |
| `Persistencia:Proveedor` | `InMemory`, `Postgres` | `InMemory` (en compose: `Postgres`) |
| `ConnectionStrings:RepMatch` | cadena Npgsql | vacía |

Y los puertos del host, vía `.env` (ver `.env.example`): `PUERTO_WEB`, `PUERTO_CATALOGO`,
`PUERTO_POSTGRES`, `PUERTO_WEB_LOCAL`.

## Documentación

- [Informe de arquitectura](docs/informe-arquitectura.md)
- [Diagrama de clases](docs/diagramas/clases.md)
- [Diagrama de componentes](docs/diagramas/componentes.md)
- [Diagrama de despliegue](docs/diagramas/despliegue.md)

## Stack

.NET 10 · ASP.NET Core · Blazor Server · EF Core 10 · PostgreSQL 17 · Serilog · FluentValidation · xUnit
