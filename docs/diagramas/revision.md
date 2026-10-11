# Revisión de diagramas para Claude / Claude Design

Revisión estática del working tree del 11 de octubre de 2026. Incluye los cambios de documentación
que ya estaban staged antes de esta revisión. No implica una nueva ejecución de la aplicación.

Los ocho diagramas están alineados con la implementación dentro del alcance que representan.
No fue necesario corregir sus participantes, relaciones ni recorridos. La topología futura
continúa identificada como prevista; SOAP, RabbitMQ, Redis, IA y APIs externas no se presentan
como servicios implementados.

| Documento | Evidencia contrastada |
|---|---|
| `clases.md` | Entidades de `RepMatch.Domain`, value objects, enumeraciones, `EntidadBase`, `BusquedaCreada` y mapeo de cliente/vehículo predeterminado en `ClienteConfiguration`. |
| `componentes.md` | Referencias de los ocho `.csproj`, `FabricaCatalogo`, contratos, adaptadores REST, controladores y registros de la API. |
| `capas.md` | `FachadaAplicacion`, `SelectorVehiculo`, servicios, despacho de eventos, observador, estrategias locales/remotas y repositorios. |
| `despliegue.md` | `docker-compose.yml`, Dockerfiles, registros de los hosts y configuración del catálogo. Los puertos dibujados son los valores por defecto. |
| `secuencia.md` | `Home.razor`, `Login.razor`, `Clientes.razor`, `MainLayout`, `SesionActual`, fachada, servicios, observador y despacho. Se verificó que se guarda antes de despachar y que el observador confirma sus cambios. |

## Indicaciones de diseño

Conservar Mermaid como fuente editable y mantener el significado de las flechas, las cardinalidades,
el orden de llamadas y la separación entre implementación actual y futura. Mejorar presentación
con tipografía legible, espacio entre grupos y una paleta consistente de azul, verde y ámbar.

Las imágenes SVG se generan desde Mermaid con el mismo estilo. No redibujar las relaciones
a mano ni usar generación de imágenes para texto técnico. Para cambiar contenido, editar el archivo
`.mmd` de `fuentes/` y regenerar. Para cambiar la presentación de las imágenes, editar
`scripts/diagramas/mermaid.json` y regenerar.

Este informe quedó preparado para compartir; no se envió a Claude ni a Claude Design desde esta sesión.
