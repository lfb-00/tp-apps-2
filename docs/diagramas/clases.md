# Diagrama de clases — RepMatch

Modelo del componente de dominio (`RepMatch.Domain`). No tiene dependencias externas: ni EF Core,
ni ASP.NET, ni atributos de persistencia. El mapeo relacional vive aparte, en
`RepMatch.Persistence/Configuraciones/`.

## Mapeo con la consigna

La consigna del TP Inicial pide un componente de dominio con `Pedido`, `Cliente` y `Producto`.
Como la aplicación es un comparador y no un e-shop, esos tres conceptos se materializan así:

| Consigna | RepMatch | Por qué |
|---|---|---|
| Cliente | `Cliente` | Igual: quien usa el comparador, con su garage de vehículos. |
| Producto | `Repuesto` + `Oferta` | `Repuesto` es la pieza como concepto (código canónico, equivalencias, compatibilidad); `Oferta` es esa pieza publicada por una tienda concreta a un precio concreto. Separarlos es lo que permite comparar la misma pieza entre sitios. |
| Pedido | `Busqueda` | El agregado que atraviesa todo el sistema. No se compra nada: lo que se "despacha" es la consulta a las tiendas. |

## Diagrama

```mermaid
classDiagram
    direction LR

    class EntidadBase {
        <<abstract>>
        +Guid Id
        +IReadOnlyCollection~IEventoDominio~ EventosDominio
        #RegistrarEvento(evento)
        +LimpiarEventos()
    }

    class DatosVehiculo {
        <<value object>>
        +string Marca
        +string Modelo
        +int Anio
        +string? Motor
    }

    class Dinero {
        <<value object>>
        +decimal Monto
        +string Moneda
        +Pesos(monto)$ Dinero
        +Dolares(monto)$ Dinero
        +operator+(a, b) Dinero
        +EsMasBaratoQue(otro) bool
    }

    class Cliente {
        +string Nombre
        +string Email
        +string? HashContrasena
        +DateTimeOffset FechaAlta
        +string? Telefono
        +bool TieneWhatsApp
        +string? Provincia
        +string? Localidad
        +byte[]? FotoPerfil
        +string? FotoTipoContenido
        +Guid? VehiculoPredeterminadoId
        +string? TemaPreferido
        +IReadOnlyCollection~Vehiculo~ Vehiculos
        +EstablecerContrasena(hash)
        +CambiarNombre(nombre)
        +ActualizarContacto(telefono, tieneWhatsApp, provincia, localidad)
        +CambiarFotoPerfil(datos, tipoContenido)
        +QuitarFotoPerfil()
        +EstablecerVehiculoPredeterminado(vehiculoId)
        +CambiarTema(tema)
        +AgregarVehiculo(datos, vin, alias) Vehiculo
        +QuitarVehiculo(vehiculoId)
    }

    class Vehiculo {
        +Guid ClienteId
        +DatosVehiculo Datos
        +string? Vin
        +string Alias
        +CambiarAlias(alias)
    }

    class Repuesto {
        +string CodigoCanonico
        +string Nombre
        +string? Descripcion
        +SistemaVehiculo Sistema
        +IReadOnlyCollection~string~ CodigosEquivalentes
        +IReadOnlyCollection~AplicacionVehiculo~ Aplicaciones
        +EsCompatibleCon(vehiculo) bool
        +TerminosDeBusqueda() IReadOnlyList~string~
    }

    class AplicacionVehiculo {
        +Guid RepuestoId
        +string Marca
        +string Modelo
        +int AnioDesde
        +int AnioHasta
        +string? Motor
        +EsCompatibleCon(vehiculo) bool
    }

    class Busqueda {
        +Guid ClienteId
        +DatosVehiculo Vehiculo
        +string TextoLibre
        +EstadoBusqueda Estado
        +DateTimeOffset FechaCreacion
        +string? MotivoFalla
        +IReadOnlyCollection~string~ CodigosObjetivo
        +IReadOnlyCollection~Oferta~ Ofertas
        +AsignarCodigosObjetivo(codigos)
        +RegistrarOfertas(ofertas)
        +Fallar(motivo)
        +OfertasOrdenadasPorPrecio(moneda) IReadOnlyList~Oferta~
    }

    class Oferta {
        +Guid BusquedaId
        +string CodigoRepuesto
        +string NombreTienda
        +string Titulo
        +Dinero Precio
        +Dinero? CostoEnvio
        +string UrlOriginal
        +bool Disponible
        +DateTimeOffset CapturadaEn
        +Dinero PrecioTotal
        +EstaVencida(antiguedadMaxima) bool
    }

    class SistemaVehiculo {
        <<enumeration>>
        Frenos
        Suspension
        Motor
        Transmision
        Electrico
        Refrigeracion
        Escape
        Direccion
        Carroceria
        Neumaticos
    }

    class EstadoBusqueda {
        <<enumeration>>
        Borrador
        Pendiente
        Diagnosticada
        Completada
        Fallida
    }

    EntidadBase <|-- Cliente
    EntidadBase <|-- Vehiculo
    EntidadBase <|-- Repuesto
    EntidadBase <|-- AplicacionVehiculo
    EntidadBase <|-- Busqueda
    EntidadBase <|-- Oferta

    Cliente "1" *-- "0..*" Vehiculo : garage
    Cliente "0..*" --> "0..1" Vehiculo : predeterminado
    Vehiculo *-- "1" DatosVehiculo : datos
    Repuesto "1" *-- "1..*" AplicacionVehiculo : compatibilidad
    Repuesto --> SistemaVehiculo
    Busqueda *-- "1" DatosVehiculo : snapshot
    Busqueda "1" *-- "0..*" Oferta : resultados
    Busqueda --> EstadoBusqueda
    Oferta *-- "1..2" Dinero : precio / envio
```

## Decisiones que el diagrama refleja

**`DatosVehiculo` es value object, `Vehiculo` es entidad.** El vehículo del garage tiene identidad
propia (el cliente le pone un alias). La búsqueda, en cambio, guarda un *snapshot* de los datos: si
mañana el cliente borra el auto, la búsqueda histórica sigue siendo interpretable.

**`Dinero` existe para que no se puedan sumar monedas distintas.** El comparador mezcla ofertas en
ARS (tiendas VTEX argentinas) y en USD (eBay). Sumarlas sin cotización daría un ranking mentiroso,
así que el tipo lo prohíbe y lanza `ExcepcionDominio`.

**`Oferta.PrecioTotal` incluye el envío.** Ordenar por precio de lista haría ganar a una oferta
barata con envío carísimo. El ranking usa siempre el total.

**El perfil del cliente protege sus invariantes.** La foto no puede estar vacía, tiene que ser JPG,
PNG o WEBP y no puede superar 5 MB. El teléfono admite dígitos, espacios, guiones, paréntesis y un
`+` inicial, entre 8 y 20 caracteres. El tema solo puede ser `"claro"` u `"oscuro"`. La provincia se
guarda como texto y no como enumeración: es un dato de contacto, no algo sobre lo que el dominio
decida. No hay ningún método para cambiar el email.

**El vehículo predeterminado tiene que ser del propio garage.** `EstablecerVehiculoPredeterminado`
rechaza un vehículo ajeno, y `QuitarVehiculo` limpia el predeterminado si se quita ese auto. Se
guarda solo el `Id` (sin navegación); en la base es una FK opcional con `ON DELETE SET NULL`.

**Las colecciones se exponen como `IReadOnlyCollection`.** Modificarlas obliga a pasar por un método
de la entidad, que es donde viven las invariantes. EF Core escribe directamente los campos privados
de respaldo (`PropertyAccessMode.Field`).
