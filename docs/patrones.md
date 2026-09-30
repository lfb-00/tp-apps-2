# Patrones aplicados — Primera Parte

RepMatch es un comparador de repuestos, del estilo de un metabuscador: el usuario indica el
vehículo y el problema, y la aplicación guarda la consulta con las piezas compatibles. No vende
ni administra stock. Los nombres Pedido, Inventario y Clientes de la consigna son ejemplos de
servicios. Acá los servicios del dominio son `ServicioClientes` y `ServicioBusquedas`.

| Patrón que pide la consigna | Dónde está |
|---|---|
| Factory | `src/RepMatch.Web/Servicios/FabricaCatalogo.cs` |
| Repository | `src/RepMatch.Domain/Repositorios/` y `src/RepMatch.Persistence/Repositorios/` |
| Strategy | `ICatalogoRepuestos`, `CatalogoLocal`, `CatalogoRemoto` |
| Observer | `DespachadorEventos`, `ObservadorCompatibilidad`, `BusquedaCreada` |
| Facade | `src/RepMatch.Aplicacion/Fachada/FachadaAplicacion.cs` |

También están en el código, aunque la consigna no los nombre: Unit of Work (`IUnitOfWork`) y
Adapter (`CatalogoRemoto`, que traduce HTTP al contrato del catálogo).

## Factory

`FabricaCatalogo.AgregarCatalogo` lee `Catalogo:Modo` al arrancar y enlaza `ICatalogoRepuestos`
con `CatalogoLocal` o con `CatalogoRemoto`. Los dos caminos arman dependencias distintas (EF Core
o un `HttpClient`), así que la decisión no puede quedar repartida en cada pantalla.

Cambiar `Catalogo__Modo` reconfigura la aplicación sin recompilar. La presentación no se entera:
habla con `FachadaAplicacion`.

## Repository

Las interfaces `IClienteRepository`, `IRepuestoRepository` e `IBusquedaRepository` están declaradas
en `RepMatch.Domain`, que no referencia EF Core. Las implementaciones viven en
`RepMatch.Persistence`. `RepuestoRepository.BuscarCompatiblesAsync` filtra en la base por marca,
modelo y rango de años, y deja la regla de motorización en `Repuesto.EsCompatibleCon`.

## Strategy

`CatalogoLocal` y `CatalogoRemoto` cumplen el mismo contrato y devuelven los mismos DTO. Una
consulta de compatibilidad no cambia según venga de EF Core o de HTTP. Sumar otro mecanismo es
otra clase más una rama en la fábrica.

La propiedad `Modo` existe para el log y para la evidencia de acceso local contra remoto. No la
muestra la interfaz.

## Observer

`Busqueda` registra `BusquedaCreada` al construirse. `ServicioBusquedas` confirma la unidad de
trabajo y le pasa el evento a `DespachadorEventos`. El despachador recorre los
`IObservadorEventoDominio` registrados y llama a los que `PuedeObservar`.

`ObservadorCompatibilidad` es el que consulta `ICatalogoRepuestos` y llama a
`AsignarCodigosObjetivo` o a `Fallar`. `ServicioBusquedas` no referencia el catálogo. Ese corte es
la integración entre módulos que pide la consigna: un módulo publica un hecho y otro reacciona.

## Facade

`FachadaAplicacion` es la única dependencia de las páginas Razor. Concentra clientes, búsquedas y
las dos consultas del catálogo. Una página no arma el caso de uso llamando al servicio, al
catálogo y al despachador por separado, y tampoco traduce errores de HTTP.

## Cómo se ve en una búsqueda

1. La página llama a `FachadaAplicacion.RegistrarBusquedaAsync`.
2. `ServicioBusquedas` valida, crea la `Busqueda` y la guarda en estado `Pendiente`.
3. El despachador entrega `BusquedaCreada`.
4. El observador completa los códigos y vuelve a confirmar.
5. La fachada devuelve la búsqueda ya actualizada.

El recorrido está dibujado en [`diagramas/secuencia.md`](diagramas/secuencia.md) y las capas en
[`diagramas/capas.md`](diagramas/capas.md).
