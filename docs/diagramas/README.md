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

## Regenerar imágenes

Desde la raíz del repositorio, con Node.js 22.13 o posterior y npm instalados:

```powershell
npm ci --prefix scripts/diagramas
npm run generar --prefix scripts/diagramas
```

La instalación descarga el navegador que utiliza Mermaid CLI. El estilo de las imágenes se
configura en `scripts/diagramas/mermaid.json`. Mermaid CLI queda fijado en `package-lock.json`.
El generador detecta todos los `.mmd` de `fuentes/`. Para agregar un diagrama, crear su fuente y enlazar sus imágenes desde el documento correspondiente.

## Preparar la entrega

Después de regenerar las imágenes:

```powershell
npm run entrega --prefix scripts/diagramas
```

Se crea una carpeta nueva en `artifacts/entrega-repmatch-<fecha>/` con `diagramas/`, `codigo/`
y un `LEEME.md`. Se copian los archivos actuales del working tree, incluidos cambios sin commit;
se excluyen `.git`, dependencias instaladas, resultados de compilación y `.env`.

La [revisión contra el código](revision.md) se puede compartir con Claude o Claude Design junto
con esta carpeta. Los diagramas densos de clases y secuencia conviene abrirlos en SVG y ampliar.
