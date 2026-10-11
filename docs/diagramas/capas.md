# Diagrama de arquitectura en capas — Primera Parte

Incluye la capa de servicios que pide la Primera Parte, la fachada por la que entra la presentación
y el despacho de eventos entre módulos.

```mermaid
flowchart TB
    subgraph presentacion["Presentación"]
        WEB["RepMatch.Web<br/>Blazor Server"]
        SEL["SelectorVehiculo"]
    end

    subgraph servicios["Capa de servicios"]
        FAC["FachadaAplicacion"]
        SC["ServicioClientes"]
        SB["ServicioBusquedas"]
        DES["DespachadorEventos"]
        OBS["ObservadorCompatibilidad"]
    end

    subgraph contratos["Contratos"]
        IC["ICatalogoRepuestos"]
        ICV["ICatalogoVehiculos"]
    end

    subgraph dominio["Dominio"]
        ENT["Cliente · Busqueda · Repuesto"]
        EV["BusquedaCreada"]
        REPO["IClienteRepository · IBusquedaRepository · IRepuestoRepository<br/>IConfiguracionVehiculoRepository"]
    end

    subgraph datos["Acceso a datos"]
        PER["Repositorios · UnitOfWork · EF Core"]
        BD[("PostgreSQL o InMemory")]
    end

    WEB --> FAC
    SEL --> ICV
    FAC --> SC
    FAC --> SB
    FAC --> IC
    FAC --> ICV
    SB --> DES
    DES --> OBS
    OBS --> IC
    SC --> REPO
    SB --> REPO
    OBS --> REPO
    SB --> EV
    IC --> PER
    ICV --> PER
    REPO --> PER
    PER --> ENT
    PER --> BD
```

La presentación no referencia repositorios ni el despachador. Las páginas entran por la fachada;
`SelectorVehiculo` consulta `ICatalogoVehiculos` directamente para cargar las opciones, y la fachada
usa el mismo contrato para validar el vehículo antes de consultar piezas. `ServicioBusquedas` persiste la
búsqueda y publica `BusquedaCreada`. El observador, no el servicio, consulta el catálogo.
