---
name: actualizar-docs
description: Revisar y actualizar documentación contra el código con cambios mínimos. Usar para auditorías de consistencia, revisión del diff y actualización de diagramas, contratos y ejemplos, preservando la intención del producto y la redacción correcta.
---

# Actualizar documentación contra el código

El código es la fuente de verdad de la implementación. Preservar la intención del producto, el estilo y las frases correctas. Corregir inconsistencias comprobables; ampliar documentación cuando el usuario lo pida. No cambiar código para ajustarlo a los docs: informar los defectos encontrados.

## Revisión y cambios

1. Leer las instrucciones del repo e inspeccionar Git, incluidos staged y unstaged. Identificar y conservar trabajo ajeno.
2. En cada actualización, verificar toda la documentación contra el código, aunque el cambio sea puntual, salvo restricción expresa del usuario. Incluir README, docs, diagramas y explicaciones, patrones, informes, seguimiento, instrucciones de agentes, ejemplos HTTP y contratos publicados.
3. Seguir los recorridos reales: UI, fachada, servicios, contratos, implementaciones, DI, dominio, eventos, persistencia y configuración. Usar pruebas o ejecución cuando la revisión estática no alcance.
4. Corregir solo lo necesario y actualizar las menciones relacionadas. Una revisión completa no obliga a modificar archivos correctos. Respetar documentos que el usuario pidió dejar intactos.
5. Revisar el diff final. Cada corrección debe tener una inconsistencia y evidencia identificables; cada ampliación debe responder a lo solicitado. Si se pidió depurar cambios existentes, aplicar también ese criterio al diff previo.

## Ejemplos para decidir

| Situación | Acción |
|---|---|
| «La semilla se carga solo si todavía falta» describe adecuadamente el código. | Conservarla; no reemplazarla por «solo cuando su tabla raíz está vacía» para hacerla más precisa. |
| La introducción expresa que el producto compara ofertas de tiendas reales; esas integraciones pertenecen al roadmap. | Mantener la visión. Corregir únicamente una afirmación que presente esa integración como ya implementada. |
| El diagrama afirma que toda la UI pasa por la fachada, pero un selector usa directamente otro contrato. | Agregar la relación real y corregir la explicación correspondiente. |
| Un enum se presenta como completo, pero faltan valores del código. | Agregar los valores faltantes sin rehacer el resto del diagrama. |
| Un diagrama simplificado omite detalles internos sin contradecir su recorrido. | Conservar la abstracción; no convertirlo automáticamente en un inventario exhaustivo. |
| El usuario pide agregar secuencias completas para explicar la app y sus patrones. | Incorporar los flujos solicitados con participantes, validaciones, alternativas y efectos reales; seguir las pautas siguientes. |
| Se eliminaron imágenes, fuentes duplicadas o scripts de generación. | Quitar enlaces e instrucciones obsoletos; no recrear esos archivos para satisfacer la documentación vieja. |
| Una evidencia histórica muestra una cantidad de pruebas anterior. | Conservar su fecha y resultado; agregar una verificación nueva solo si se ejecutó. |

## Diagramas

Los bloques Mermaid de los Markdown son la única fuente en este proyecto. No crear copias `.mmd`, SVG ni PNG en el repo. Exportar imágenes solo si se solicitan para una presentación o entrega; si la comprobación exige exportar, hacerlo en una carpeta temporal.

Comprobar relaciones, flechas, cardinalidades, contratos, selección local/remota, orden de llamadas, argumentos, eventos y persistencia. Distinguir topología implementada y prevista. Validar sintaxis y renderizado con herramientas disponibles: que Mermaid renderice no demuestra que el contenido coincida con el código.

### Secuencias completas cuando se soliciten

- Cubrir el recorrido desde la acción del usuario hasta el resultado visible, con los participantes que explican la operación y los patrones aplicados.
- Mostrar validaciones y caminos alternativos relevantes con `alt`, `opt` y `loop`. Distinguir resultados fallidos de excepciones capturadas y propagadas; no inventar manejo de errores.
- Representar consultas, cambios pendientes, guardados, cascadas y límites de transacción según ocurren. Indicar diferencias de proveedor si afectan el comportamiento descrito.
- Usar nombres de operaciones y contratos; evitar copiar cuerpos de métodos o firmas extensas. Enlazar el código de referencia y describir cada flujo en pocas palabras.
- Actualizar índices y menciones relacionados cuando se agreguen secuencias. No agregar una secuencia por cada método solo para aumentar cobertura.

Ejemplos de RepMatch que deben contrastarse nuevamente con el código al actualizar:

| Flujo | Qué debe reflejar |
|---|---|
| Búsqueda sin sesión | Selector vía `ICatalogoVehiculos`, validación del formulario y consulta por fachada. El texto no se envía ni persiste; no se crea `Busqueda` ni se despacha `BusquedaCreada`. |
| Consulta local/remota | Factory elige las implementaciones; el consumidor usa contratos. Local accede al repositorio y dominio; Remoto adapta HTTP/JSON y llega al catálogo local de la API. Separar validación vehicular de consulta de repuestos. |
| Eliminación de cuenta | Validación de contraseña, carga del cliente y BCrypt antes de borrar. Liberar el predeterminado si existe, borrar búsquedas y cliente, confirmar y cerrar sesión tras el éxito. La transacción relacional y el trabajo directo en InMemory no tienen las mismas garantías. |
| Búsqueda con cuenta | Guardar antes de despachar el evento; Observer asigna códigos y confirma después. La fachada obtiene el detalle de las piezas. No dibujar todo como una sola transacción. |

## Contratos, evidencias y entrega

- Actualizar ejemplos ejecutables y contratos desactualizados; preferir generar OpenAPI desde la aplicación. No inventar ejecuciones, conteos de pruebas ni resultados de logs.
- Ejecutar `git diff --check`, comprobar enlaces y validar los formatos afectados. No agregar tests de código para cambios exclusivamente documentales.
- No hacer commits ni stage por defecto. Si se pide todo unstaged, vaciar el index conservando el working tree y comprobar `git diff --cached`.
- Informar qué se cambió y verificó, distinguiendo revisión estática, sintaxis, renderizado y ejecución. Explicitar lo que no pudo verificarse sin presentar la revisión como completa.
