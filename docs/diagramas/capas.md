# Diagrama de arquitectura en capas — Primera Parte

Incluye la capa de servicios que pide la Primera Parte, la fachada por la que entra la presentación
y el despacho de eventos entre módulos.

```mermaid
flowchart TB
    subgraph presentacion["Presentación · RepMatch.Web"]
        WEB["Páginas Razor<br/>Blazor Server"]
        SEL["SelectorVehiculo"]
        FAB["FabricaCatalogo"]
    end

    subgraph servicios["Capa de servicios · RepMatch.Aplicacion"]
        FAC["FachadaAplicacion"]
        SC["ServicioClientes"]
        SB["ServicioBusquedas"]
        SM["ServicioMecanicos · ServicioRevisiones<br/>ServicioDerivaciones"]
        DES["DespachadorEventos"]
        OBS["ObservadorCompatibilidad"]
        LOC["CatalogoLocal · CatalogoVehiculosLocal"]
    end

    subgraph remoto["Adaptador remoto · RepMatch.Clientes.Rest"]
        REM["CatalogoRemoto · CatalogoVehiculosRemoto"]
    end

    subgraph contratos["Contratos · RepMatch.Contracts"]
        IC["ICatalogoRepuestos"]
        ICV["ICatalogoVehiculos"]
    end

    subgraph utilidad["Utilidad · RepMatch.Common"]
        COM["ResultadoOperacion · validación · logging"]
    end

    subgraph dominio["Dominio · RepMatch.Domain"]
        ENT["Cliente · Busqueda · Repuesto · Derivacion"]
        EV["BusquedaCreada · BusquedaCompartida<br/>DiagnosticoValidado · DerivacionATienda"]
        REPO["IClienteRepository · IBusquedaRepository · IRepuestoRepository<br/>IConfiguracionVehiculoRepository · IDerivacionRepository"]
    end

    subgraph datos["Acceso a datos · RepMatch.Persistence"]
        PER["Repositorios · UnitOfWork · EF Core"]
        BD[("PostgreSQL o InMemory")]
    end

    WEB --> FAC
    SEL --> ICV
    FAB -. "Catalogo:Modo elige una implementación" .-> LOC
    FAB -. "Catalogo:Modo elige una implementación" .-> REM
    FAC --> SC
    FAC --> SB
    FAC --> SM
    FAC --> IC
    FAC --> ICV
    FAC --> COM
    SB --> DES
    SM --> DES
    DES --> OBS
    OBS --> IC
    SC --> REPO
    SB --> REPO
    OBS --> REPO
    SM --> REPO
    SB --> EV
    SM --> EV
    LOC -. implementa .-> IC
    LOC -. implementa .-> ICV
    REM -. implementa .-> IC
    REM -. implementa .-> ICV
    LOC --> REPO
    REM --> COM
    REPO --> PER
    PER --> ENT
    PER --> BD
```

`ICatalogoRepuestos` e `ICatalogoVehiculos` viven en `Contracts` y no dependen de Persistence. Las
implementaciones locales están en `Aplicacion` y llegan a la base por las interfaces de repositorio.
Las remotas están en `Clientes.Rest` y salen por HTTP; no tocan Persistence. `FabricaCatalogo`
enlaza cada contrato con una sola de las dos al arrancar.

La presentación no referencia repositorios ni el despachador. Las páginas entran por la fachada.
`SelectorVehiculo` consulta `ICatalogoVehiculos` para cargar las opciones, y la fachada usa el mismo
contrato para validar el vehículo antes de consultar piezas. `ServicioBusquedas` persiste la
búsqueda y publica `BusquedaCreada`. El observador, no el servicio, consulta el catálogo.
`ServicioRevisiones` y `ServicioDerivaciones` siguen el mismo orden —confirmar y después despachar—
con `BusquedaCompartida`, `DiagnosticoValidado` y `DerivacionATienda`, que hoy no tienen
observadores. El endpoint `GET /derivar/{ofertaId}` de la Web también entra por la fachada.
`Aplicacion` y `Clientes.Rest` usan `Common`.
