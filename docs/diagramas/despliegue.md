# Diagrama de despliegue — RepMatch

Dos topologías: la del **TP Inicial**, que es la que está implementada y se levanta con
`docker compose up`, y la **prevista** para las entregas siguientes, que se documenta acá para
mostrar que la arquitectura actual la admite sin rediseño.

## TP Inicial — implementado

```mermaid
flowchart TB
    subgraph nav["💻 Navegador"]
        BR["Cliente web<br/><i>HTTP + WebSocket (Blazor Server)</i>"]
    end

    subgraph docker["🐳 Docker host — red bridge 'repmatch'"]
        subgraph c1["Contenedor: repmatch-web"]
            W["RepMatch.Web<br/>ASP.NET Core 10 · Blazor Server<br/><b>:8080</b><br/>Catalogo__Modo=Remoto"]
        end

        subgraph c2["Contenedor: repmatch-catalogo-api"]
            A["RepMatch.Catalogo.Api<br/>ASP.NET Core 10 · REST + OpenAPI<br/><b>:8080</b> → host :8081"]
        end

        subgraph c3["Contenedor: repmatch-postgres"]
            P[("PostgreSQL 17<br/><b>:5432</b><br/>volumen datos-postgres")]
        end
    end

    BR -- "http://localhost:8080" --> W
    W == "HTTP/JSON<br/>http://catalogo-api:8080<br/><b>acceso REMOTO</b>" ==> A
    W -- "Npgsql" --> P
    A -- "Npgsql" --> P

    style W fill:#eaf2fa,stroke:#1f5f9e
    style A fill:#fbf0e2,stroke:#9a5b13
    style P fill:#e6f4ec,stroke:#1f7a4d
```

### Nodos

| Nodo | Imagen | Puerto host | Rol |
|---|---|---|---|
| `repmatch-web` | build desde `src/RepMatch.Web/Dockerfile` | 8080 | Capa de presentación + factory del catálogo |
| `repmatch-catalogo-api` | build desde `src/RepMatch.Catalogo.Api/Dockerfile` | 8081 | Host remoto del componente de catálogo |
| `repmatch-postgres` | `postgres:17-alpine` | 5432 | Persistencia, con volumen nombrado |

Las imágenes .NET son multi-etapa: se compila con `mcr.microsoft.com/dotnet/sdk:10.0` y se publica
sobre `mcr.microsoft.com/dotnet/aspnet:10.0`, que no lleva compilador. Ambos servicios corren como
usuario `app`, sin privilegios de root.

`postgres` declara un `healthcheck` con `pg_isready` y los servicios .NET dependen de
`condition: service_healthy`: sin eso, la API arranca antes de que la base acepte conexiones. Como
segunda red de seguridad, `ExtensionesPersistencia.InicializarBaseAsync` reintenta hasta diez veces
con espera creciente.

### Los dos modos de ejecución

| Modo | Cómo se levanta | Qué demuestra |
|---|---|---|
| **Local** | `Catalogo__Modo=Local` | Invocación directa en proceso: la Web resuelve el catálogo por referencia de proyecto y no genera tráfico hacia `catalogo-api` |
| **Remoto** | `Catalogo__Modo=Remoto` *(por defecto en compose)* | Invocación remota: cada consulta sale por HTTP al otro contenedor, y el mismo `X-Correlation-Id` aparece en los logs de ambos |

Fuera de contenedores, para desarrollo:

```bash
dotnet run --project src/RepMatch.Catalogo.Api --urls http://localhost:5081
```

```bash
dotnet run --project src/RepMatch.Web --urls http://localhost:5080
```

## Topología prevista — Primera, Segunda Parte e Integrador

Ninguno de los nodos nuevos obliga a cambiar los componentes ya construidos: `search-api` consume el
mismo `ICatalogoRepuestos`, y `catalogo-soap` será una tercera implementación de esa interfaz.

```mermaid
flowchart TB
    subgraph implementado["Ya implementado (TP Inicial)"]
        W["web :8080<br/>Blazor + BFF"]
        A["catalogo-api :8081<br/>REST"]
        P[("postgres :5432")]
    end

    subgraph previsto["Previsto"]
        S["search-api :8083<br/>agregador de ofertas"]
        SOAP["catalogo-soap :8082<br/>CoreWCF · WSDL"]
        IA["ai-api :8084<br/>diagnóstico por texto libre"]
        WK["notificaciones-worker<br/>consumidores"]
        MQ{{"rabbitmq :5672<br/>UI :15672"}}
        R[("redis :6379<br/>caché de ofertas")]
    end

    subgraph externo["🌐 Fuentes externas reales"]
        EB["eBay Browse API<br/><i>OAuth2 client_credentials</i>"]
        VT["VTEX Catalog API<br/><i>easy.com.ar · fravega.com</i>"]
        VP["NHTSA vPIC<br/><i>decodificación de VIN</i>"]
    end

    W --> A
    W --> S
    W --> IA
    A --> P
    S --> R
    S --> EB
    S --> VT
    SOAP --> P
    SOAP --> VP
    IA --> MQ
    W -- "BusquedaCreada" --> MQ
    MQ --> WK
    MQ --> S

    style W fill:#eaf2fa,stroke:#1f5f9e
    style A fill:#fbf0e2,stroke:#9a5b13
    style P fill:#e6f4ec,stroke:#1f7a4d
```

### Requisitos externos de la topología prevista

| Servicio | Credenciales | Estado verificado |
|---|---|---|
| eBay Browse API | App ID + Cert ID (registro gratuito) | 5000 llamadas/día |
| VTEX `easy.com.ar` | ninguna | ✅ HTTP 206 con autopartes reales |
| VTEX `fravega.com` | ninguna | ✅ HTTP 206 |
| NHTSA vPIC | ninguna | ✅ HTTP 200 |
| MercadoLibre | — | ❌ descartado: `403 forbidden`, cerraron la búsqueda pública |
