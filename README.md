# RepMatch

Meta-buscador de repuestos automotrices con diagnóstico asistido por IA.
Trabajo práctico de **Desarrollo de Aplicaciones II** — UADE.

El usuario identifica su vehículo, describe el problema en lenguaje natural, y la aplicación compara
las ofertas de varios sitios reales. **No vende ni almacena stock**: normaliza, compara y deriva a la
tienda de origen.

> **Entrega actual: TP Inicial** — arquitectura de componentes, aplicación multicapa y acceso local
> vs. remoto. El diagnóstico por IA, el servicio SOAP y la mensajería llegan en las entregas
> siguientes.

## Arranque rápido

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

Abrir **http://localhost:5080/acceso**: esa página ejecuta la misma consulta por los dos caminos
—invocación directa en proceso e invocación remota HTTP— y muestra ambas latencias juntas.

Con contenedores:

```bash
docker compose up --build
```

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

## Configuración

Todo se puede sobreescribir por variable de entorno (`Seccion__Clave`).

| Clave | Valores | Por defecto |
|---|---|---|
| `Catalogo:Modo` | `Local`, `Remoto` | `Local` |
| `Catalogo:UrlBaseRemota` | URL de `Catalogo.Api` | `http://localhost:5081` |
| `Persistencia:Proveedor` | `InMemory`, `Postgres` | `InMemory` |
| `ConnectionStrings:RepMatch` | cadena Npgsql | vacía |

`InMemory` permite correr todo sin Docker ni base instalada.

## Documentación

- [Informe de arquitectura](docs/informe-arquitectura.md)
- [Diagrama de clases](docs/diagramas/clases.md)
- [Diagrama de componentes](docs/diagramas/componentes.md)
- [Diagrama de despliegue](docs/diagramas/despliegue.md)

## Stack

.NET 10 · ASP.NET Core · Blazor Server · EF Core 10 · PostgreSQL 17 · Serilog · FluentValidation · xUnit
