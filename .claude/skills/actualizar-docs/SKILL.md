---
name: actualizar-docs
description: Revisar y actualizar documentación de un repositorio contra su código, con cambios mínimos y especial atención a diagramas, contratos y ejemplos. Usar para auditorías de consistencia o revisión del diff de documentación, preservando la intención del producto y la redacción existente.
---

# Actualizar documentación con cambios mínimos

El código es la fuente de verdad para describir la implementación. El objetivo es corregir inconsistencias comprobables, preservando el estilo, la estructura y la intención del producto. Esta skill recoge la preferencia del usuario por correcciones puntuales; sus instrucciones del momento prevalecen.

## Criterio para decidir cambios

- Cambiar afirmaciones falsas o desactualizadas: clases y contratos inexistentes o faltantes en una descripción exhaustiva, dependencias incorrectas, flujos distintos al código, parámetros erróneos, ejemplos que fallan y comandos o endpoints obsoletos.
- No reformular una frase correcta por sonar más técnica o precisa. No agregar aclaraciones sobre casos límite, proveedores, excepciones o cobertura de pruebas si no resuelven una contradicción relevante en lo documentado.
- Distinguir intención del producto, diseño futuro e implementación actual. No reemplazar la visión general por una lista de limitaciones actuales ni reinterpretar el roadmap como una promesa de funcionalidad ya implementada.
- Respetar el nivel de abstracción del documento. Un diagrama simplificado no necesita representar todos los detalles del código; sí debe mostrar correctamente las relaciones y recorridos que afirma representar.
- No modificar código para hacerlo coincidir con los docs. Si se encuentra un defecto de código, informarlo sin repararlo salvo autorización del usuario.

Ejemplo de cambio innecesario: sustituir «cada grupo de datos semilla se carga solo si todavía falta» por «solo cuando su tabla raíz está vacía». Conservar la primera frase cuando expresa adecuadamente el comportamiento. Ejemplo de corrección necesaria: si un diagrama afirma que toda la presentación pasa por una fachada, pero un selector consulta directamente otro contrato, corregir esa relación y el texto que la describe.

## Revisión

1. Inspeccionar las instrucciones del repositorio y el estado de Git, incluidos los cambios staged y unstaged. Identificar el trabajo existente antes de editar; no sobrescribir cambios ajenos por asumir que todos son propios.
2. En cada actualización, verificar toda la documentación del repositorio contra el código, aunque el cambio solicitado sea puntual: README, docs, diagramas, informes, documentación de patrones, archivos de seguimiento e instrucciones de agentes, ejemplos HTTP y contratos publicados. Comprobar también las fuentes `.mmd`, los SVG existentes y sus explicaciones. No limitar la revisión al archivo editado, salvo que el usuario restrinja expresamente el alcance.
3. Contrastar cada afirmación técnica pertinente con entidades, servicios, interfaces, implementaciones, registros de dependencias, controladores, persistencia, configuración, UI y pruebas. Seguir los recorridos reales antes de corregir un diagrama. No basarse solamente en nombres de clases o en otros docs.
4. Aplicar el cambio más pequeño que elimine la inconsistencia. Mantener el resto del párrafo. Actualizar las menciones relacionadas cuando la misma afirmación incorrecta aparezca en varios documentos.
5. Revisar el diff completo, incluido el que ya existía si el usuario pidió depurarlo. Para cada cambio, poder señalar qué afirmación era incorrecta y qué código lo demuestra. Retirar reformulaciones, precisiones y ampliaciones innecesarias dentro del alcance autorizado.

La verificación completa es obligatoria en cada actualización; las modificaciones siguen siendo mínimas. Conservar los archivos que ya sean correctos y aplicar el criterio de regeneración de la sección siguiente. Si alguna parte no se puede verificar, indicarla expresamente al entregar, sin presentar la revisión como completa.

## Diagramas y evidencias

Comprobar participantes, dirección y significado de flechas, capas, contratos, implementaciones local/remota, cardinalidades, orden de validación, persistencia y eventos, argumentos de las llamadas y separación entre topología implementada y prevista. Mantener consistentes el diagrama y su explicación con pocas palabras.

En una revisión, verificar primero los SVG existentes contra las fuentes `.mmd` y el código. No regenerarlos solo para comprobar que renderizan. Regenerar únicamente cuando cambien las fuentes o el estilo, falte una imagen, se detecte que está desactualizada o el usuario lo solicite. Limitar la regeneración a los diagramas afectados cuando sea posible; si hace falta generar una imagen para verificarla, usar una carpeta temporal sin sobrescribir los SVG del repositorio. Evitar cambios en archivos generados cuando su contenido ya es correcto.

Actualizar ejemplos ejecutables y contratos generados cuando estén desactualizados. Preferir regenerar OpenAPI desde la aplicación. No inventar resultados de ejecución ni reemplazar logs históricos por datos nuevos manteniendo la fecha vieja. Actualizar cantidades de pruebas solo con evidencia verificable y conservar el contexto temporal de las evidencias existentes.

## Verificación y entrega

- Ejecutar `git diff --check`, verificar enlaces y validar la sintaxis de diagramas o formatos generados afectados cuando haya herramientas disponibles. No agregar tests de código para cambios exclusivamente de redacción.
- Usar pruebas o ejecuciones cuando sean necesarias para comprobar una afirmación; distinguir lo ejecutado de la revisión estática y de resultados históricos.
- No hacer commits ni stage por defecto. Si el usuario pide dejar todo unstaged, quitar todo del index conservando los archivos del working tree y comprobar que `git diff --cached` esté vacío. No descartar trabajo para lograrlo.
- Resumir brevemente qué inconsistencias se corrigieron, qué se verificó y si quedó algo pendiente. Confirmar el estado unstaged y la ausencia de commits cuando corresponda.
