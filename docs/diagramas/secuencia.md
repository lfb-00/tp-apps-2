# Diagrama de secuencia — registrar una búsqueda

Caso de la Primera Parte: el cliente elige un vehículo del garage, describe el problema, y la
búsqueda queda guardada con las piezas compatibles. El texto libre se persiste. La elección de
piezas la hace el observador de `BusquedaCreada`, por compatibilidad de vehículo.

```mermaid
sequenceDiagram
    actor Usuario
    participant Web as RepMatch.Web
    participant Fachada as FachadaAplicacion
    participant Busquedas as ServicioBusquedas
    participant Dominio as Busqueda
    participant UoW as UnitOfWork
    participant Despacho as DespachadorEventos
    participant Obs as ObservadorCompatibilidad
    participant Catalogo as ICatalogoRepuestos

    Usuario->>Web: Cliente, vehículo del garage y problema
    Web->>Fachada: RegistrarBusquedaAsync
    Fachada->>Busquedas: CrearAsync
    Busquedas->>Dominio: new Busqueda
    Dominio-->>Busquedas: encola BusquedaCreada
    Busquedas->>UoW: ConfirmarAsync
    Note over UoW: la búsqueda queda Pendiente
    Busquedas->>Despacho: DespacharAsync
    Despacho->>Obs: ObservarAsync(BusquedaCreada)
    Obs->>Catalogo: BuscarCompatiblesAsync
    Catalogo-->>Obs: piezas compatibles
    Obs->>Dominio: AsignarCodigosObjetivo o Fallar
    Obs->>UoW: ConfirmarAsync
    Busquedas-->>Fachada: BusquedaDto
    Fachada-->>Web: resultado
    Web-->>Usuario: búsqueda con estado y piezas
```
