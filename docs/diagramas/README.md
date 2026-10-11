# Diagramas de RepMatch

Los archivos `.mmd` en `fuentes/` son la única fuente editable de los diagramas. Los documentos
muestran las imágenes generadas en `imagenes/`: SVG para ampliar sin perder calidad e
insertar en documentos o presentaciones. No editar las imágenes a mano.

| Diagrama | Documento | SVG |
|---|---|---|
| Arquitectura en capas | [capas.md](capas.md) | [SVG](imagenes/capas.svg) |
| Clases del dominio | [clases.md](clases.md) | [SVG](imagenes/clases.svg) |
| Componentes | [componentes.md](componentes.md) | [SVG](imagenes/componentes.svg) |
| Despliegue actual | [despliegue.md](despliegue.md) | [SVG](imagenes/despliegue-actual.svg) |
| Despliegue previsto | [despliegue.md](despliegue.md) | [SVG](imagenes/despliegue-previsto.svg) |
| Registrar una búsqueda | [secuencia.md](secuencia.md) | [SVG](imagenes/secuencia-busqueda.svg) |
| Login | [secuencia.md](secuencia.md) | [SVG](imagenes/secuencia-login.svg) |
| Alta de vehículo | [secuencia.md](secuencia.md) | [SVG](imagenes/secuencia-alta-vehiculo.svg) |

## Actualizar diagramas

Editar la fuente `.mmd` correspondiente y exportarla a SVG con una herramienta compatible con
Mermaid. Reemplazar únicamente la imagen del diagrama modificado. Para agregar un diagrama,
crear su fuente y enlazar el SVG desde el documento correspondiente.

La [revisión contra el código](revision.md) acompaña los diagramas. Los diagramas densos de clases
y secuencia conviene abrirlos en SVG y ampliar.
