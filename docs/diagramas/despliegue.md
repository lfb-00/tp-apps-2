# Diagrama de despliegue — RepMatch

Dos topologías: la **implementada** (TP Inicial, Primera Parte y, de la Segunda Parte, el componente
de IA, el servicio SOAP, los precios VTEX y su caché Redis), que se levanta con `docker compose up`, y la **prevista** para las entregas siguientes, que
se documenta acá para mostrar que la arquitectura actual la admite sin rediseño.

## Implementada — TP Inicial, Primera Parte, IA, SOAP y tiendas VTEX

```mermaid
flowchart TB
    subgraph nav["💻 Navegador"]
        BR["Cliente web<br/><i>HTTP + WebSocket (Blazor Server)</i>"]
    end

    subgraph docker["🐳 Docker host — red bridge 'repmatch'"]
        subgraph c1["Contenedor: repmatch-web"]
            W["RepMatch.Web<br/>ASP.NET Core 10 · Blazor Server<br/><b>:8080</b><br/>Catalogo__Modo=Soap · IA__Modo=Remoto<br/>volumen claves-web"]
        end

        subgraph c2["Contenedor: repmatch-catalogo-api"]
            A["RepMatch.Catalogo.Api<br/>ASP.NET Core 10 · REST + OpenAPI<br/><b>:8080</b> → host :8081"]
        end

        subgraph c5["Contenedor: repmatch-catalogo-soap"]
            S["RepMatch.Catalogo.Soap<br/>ASP.NET Core 10 · CoreWCF · SOAP + WSDL<br/><b>:8080</b> → host :8082"]
        end

        subgraph c4["Contenedor: repmatch-ia-api"]
            IA["RepMatch.IA.Api<br/>ASP.NET Core 10 · REST + OpenAPI<br/><b>:8080</b> → host :8083<br/>sin base de datos"]
        end

        subgraph c3["Contenedor: repmatch-postgres"]
            P[("PostgreSQL 17<br/><b>:5432</b><br/>volumen datos-postgres")]
        end

        subgraph c6["Contenedor: repmatch-redis"]
            R[("Redis 7<br/><b>:6379</b><br/>caché distribuida de ofertas")]
        end
    end

    BR -- "http://localhost:8080" --> W
    W == "HTTP/JSON<br/>http://catalogo-api:8080<br/>opciones de vehículo" ==> A
    W == "SOAP/XML<br/>http://catalogo-soap:8080/ConsultaCompatibilidad.svc<br/>repuestos compatibles" ==> S
    W == "HTTP/JSON<br/>http://ia-api:8080<br/>POST /api/diagnostico" ==> IA
    W -- "Npgsql" --> P
    A -- "Npgsql" --> P
    S -- "Npgsql" --> P
    W -- "IDistributedCache" --> R

    LLM(["🌐 Proveedor LLM<br/>Groq o Gemini<br/><i>solo con IA_LLM_APIKEY</i>"])
    VT(["🌐 Tiendas VTEX<br/>www.easy.com.ar · www.fravega.com<br/><i>API pública de catálogo, sin credenciales</i>"])
    W == "HTTPS/JSON<br/>/api/catalog_system/pub/products/search<br/>precios de las piezas" ==> VT
    IA -. "HTTPS, opcional" .-> LLM
    A -. "HTTPS, opcional<br/>(diagnóstico en proceso)" .-> LLM

    style W fill:#eaf2fa,stroke:#1f5f9e
    style A fill:#fbf0e2,stroke:#9a5b13
    style S fill:#fbf0e2,stroke:#9a5b13
    style IA fill:#f3ecfa,stroke:#6b3fa0
    style P fill:#e6f4ec,stroke:#1f7a4d
    style R fill:#fce7f3,stroke:#be185d
```

### Nodos

| Nodo | Imagen | Puerto host | Rol |
|---|---|---|---|
| `repmatch-web` | build desde `src/RepMatch.Web/Dockerfile` | 8080 | Capa de presentación + factory del catálogo. Volumen `claves-web` con las claves que cifran la sesión del navegador, para que sobreviva a la recreación del contenedor |
| `repmatch-catalogo-api` | build desde `src/RepMatch.Catalogo.Api/Dockerfile` | 8081 | Host remoto del componente de catálogo. Las búsquedas creadas por su API se diagnostican en proceso |
| `repmatch-catalogo-soap` | build desde `src/RepMatch.Catalogo.Soap/Dockerfile` | 8082 | Host SOAP del componente de catálogo (CoreWCF). Publica el WSDL en `/ConsultaCompatibilidad.svc?wsdl` y resuelve con el mismo `CatalogoLocal` que `catalogo-api` |
| `repmatch-ia-api` | build desde `src/RepMatch.IA.Api/Dockerfile` | 8083 | Host REST del componente de IA. Sin base: recibe los candidatos en la petición. Sin clave de LLM responde el clasificador local |
| `repmatch-postgres` | `postgres:17-alpine` | 5432 | Persistencia, con volumen nombrado |
| `repmatch-redis` | `redis:7-alpine` | — | Caché distribuida de las respuestas VTEX, compartida por las instancias de Web |
| `repmatch-tienda-simulada` | build desde `tests/RepMatch.TiendaSimulada/Dockerfile`, perfil `simulacion` | — | Solo para las pruebas de punta a punta: responde como VTEX con búsquedas reales grabadas. `scripts/e2e.sh` apunta la Web acá con `OFERTAS_URL_EASY` y `OFERTAS_URL_FRAVEGA` |

La Web sale a internet para consultar los precios en las tiendas. Sin salida, o con una tienda caída,
la búsqueda funciona igual: muestra el diagnóstico y las piezas con un aviso de que no se pudieron
consultar los precios.

Las imágenes .NET son multi-etapa: se compila con `mcr.microsoft.com/dotnet/sdk:10.0` y se publica
sobre `mcr.microsoft.com/dotnet/aspnet:10.0`, que no lleva compilador. Los servicios .NET corren como
usuario `app`, sin privilegios de root.

`postgres` declara un `healthcheck` con `pg_isready` y los servicios .NET que usan la base dependen de
`condition: service_healthy`; `redis` verifica `redis-cli ping` y la Web espera también a que esté
saludable. La Web espera además a que `catalogo-api`, `catalogo-soap` e `ia-api` respondan `/salud`:
sin eso, la API arranca antes de que la base acepte conexiones. `catalogo-soap` y `web-local`
esperan a `catalogo-api`, que siembra la base primero: con la base vacía, dos hosts sembrando a la
vez chocan con los índices únicos. Como segunda red de seguridad,
`ExtensionesPersistencia.InicializarBaseAsync` reintenta hasta diez veces con espera creciente, y
antes de cada reintento descarta lo que quedó pendiente del intento fallido, así el host que perdió
la carrera encuentra la semilla ya cargada y sigue.

### Los modos de ejecución

| Modo | Cómo se levanta | Qué demuestra |
|---|---|---|
| **Local** | `Catalogo__Modo=Local` | Invocación directa en proceso: la Web resuelve el catálogo por referencia de proyecto y no genera tráfico hacia `catalogo-api` |
| **Remoto** | `Catalogo__Modo=Remoto` | Invocación remota: cada consulta sale por HTTP al otro contenedor, y el mismo `X-Correlation-Id` aparece en los logs de ambos |
| **Soap** | `Catalogo__Modo=Soap` *(por defecto en compose)* | Los repuestos salen por SOAP hacia `catalogo-soap` y las opciones de vehículo por REST hacia `catalogo-api`: cada búsqueda usa los dos protocolos, con el mismo `X-Correlation-Id` en los tres logs |

La IA tiene los mismos dos modos con `IA__Modo`: `Remoto` en el servicio `web` de compose y `Local`
en `web-local`.

Fuera de contenedores, para desarrollo:

```bash
dotnet run --project src/RepMatch.Catalogo.Api --urls http://localhost:5081
```

```bash
dotnet run --project src/RepMatch.Catalogo.Soap --urls http://localhost:5082
```

```bash
dotnet run --project src/RepMatch.IA.Api --urls http://localhost:5083
```

```bash
dotnet run --project src/RepMatch.Web --urls http://localhost:5080
```

## Topología prevista — Segunda Parte e Integrador

Ninguno de los nodos nuevos obliga a cambiar los componentes ya construidos: `search-api` consume el
mismo `ICatalogoRepuestos`, igual que ya lo hace el cliente de `catalogo-soap`, y las mismas
`IFuenteOfertas` que hoy usa la Web en proceso para consultar VTEX.

```mermaid
flowchart TB
    subgraph implementado["Ya implementado"]
        W["web :8080<br/>Blazor + BFF"]
        A["catalogo-api :8081<br/>REST"]
        IA["ia-api :8083<br/>diagnóstico por texto libre"]
        SOAP["catalogo-soap :8082<br/>CoreWCF · WSDL"]
        P[("postgres :5432")]
        R[("redis :6379<br/>caché distribuida")]
    end

    subgraph previsto["Previsto"]
        S["search-api :8084<br/>agregador de ofertas"]
        WK["notificaciones-worker<br/>consumidores"]
        MQ{{"rabbitmq :5672<br/>UI :15672"}}
    end

    subgraph externo["🌐 Fuentes externas reales"]
        EB["eBay Browse API<br/><i>OAuth2 client_credentials</i>"]
        VT["VTEX Catalog API<br/><i>easy.com.ar · fravega.com</i>"]
        VP["NHTSA vPIC<br/><i>decodificación de VIN</i>"]
    end

    W --> A
    W --> S
    W --> IA
    W --> SOAP
    W --> P
    A --> P
    S --> R
    S --> EB
    S --> VT
    SOAP --> P
    SOAP -. "previsto: decodificar VIN" .-> VP
    IA --> MQ
    W -- "BusquedaCreada" --> MQ
    MQ --> WK
    MQ --> S

    style W fill:#eaf2fa,stroke:#1f5f9e
    style A fill:#fbf0e2,stroke:#9a5b13
    style IA fill:#f3ecfa,stroke:#6b3fa0
    style P fill:#e6f4ec,stroke:#1f7a4d
```

### Requisitos externos de la topología prevista

| Servicio | Credenciales | Estado verificado |
|---|---|---|
| eBay Browse API | App ID + Cert ID (registro gratuito) | 5000 llamadas/día |
| VTEX `easy.com.ar` | ninguna | ✅ Implementado: HTTP 206; del catálogo semilla publica baterías |
| VTEX `fravega.com` | ninguna | ✅ Implementado: HTTP 206; marketplace, sobre todo accesorios |
| NHTSA vPIC | ninguna | ✅ HTTP 200 |
| LLM (Groq o Gemini, ya usado por `ia-api`) | clave del proveedor en `IA_LLM_APIKEY`, opcional | sin probar con clave real; sin clave responde el clasificador local |
| MercadoLibre | — | ❌ descartado: `403 forbidden`, cerraron la búsqueda pública |
