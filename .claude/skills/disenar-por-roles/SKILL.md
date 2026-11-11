---
name: disenar-por-roles
description: Planificar una funcionalidad de RepMatch pensando en cada rol (anónimo, cliente, mecánico) y en el uso real, antes de escribir código. Usar cuando pidan "armá un plan", "cómo implementamos el issue #N", "pensalo para los dos roles", o antes de encarar un issue que toque UI, dominio o integraciones.
---

# Diseñar una funcionalidad pensando en los roles

RepMatch tiene tres tipos de usuario y cada cambio los afecta distinto. Este procedimiento evita el
error más común: implementar el camino del cliente con sesión y descubrir después que el mecánico o
el anónimo quedaron rotos, o que el modelo de negocio (comisión por derivación) dejó de cerrar.

El resultado es un **plan**, no código. Se le presenta a la usuaria y se implementa recién cuando lo aprueba.

## 1. Entender el pedido

1. Leer el issue completo **y sus comentarios** (API de GitHub; no hay `gh`). Anotar los criterios de
   aceptación literales y los issues relacionados (`Refs`, "depende de").
2. Ubicar el pedido en la consigna (CLAUDE.md, "La consigna, por etapa"): qué requisito cubre y qué
   evidencia va a pedir el docente.
3. Trabajar solo sobre issues existentes. Si falta uno, mencionarlo; no redactarlo.

## 2. Recorrer el código real

Seguir el camino completo, no solo el archivo obvio: página Blazor → `FachadaAplicacion` → servicio →
dominio → eventos/observadores → repositorio → configuración EF → DI (`Agregar*`) de **cada host**.

- Catalogo.Api también crea búsquedas y despacha observadores: lo que se registre en la Web suele
  tener que registrarse ahí.
- Buscar con Grep quién crea o consume la entidad en `src/` (no en `tests/`): puede pasar que algo
  exista en el dominio y la UI pero nadie lo genere en producción.
- Si hay una API externa, **probarla de verdad** (WebFetch) con términos del catálogo semilla y anotar
  qué devuelve: campos, basura, cobertura, errores, paginación. El plan se basa en eso, no en la doc.

## 3. Matriz de roles

Para cada rol, responder estas preguntas en una tabla dentro del plan:

| Pregunta | Anónimo | Cliente | Mecánico |
|---|---|---|---|
| ¿Qué ve y en qué página? | | | |
| ¿Qué puede hacer? ¿Qué no debe poder hacer? | | | |
| ¿Qué se persiste por su acción? | | | |
| ¿Cómo afecta la comisión (`Derivacion`) y su atribución? | | | |
| ¿Qué pasa si el servicio externo falla o tarda? | | | |

Reglas del negocio que no hay que romper:

- **Anónimo**: no tiene `Busqueda` guardada, el texto no se persiste. Sin búsqueda no hay `Oferta`
  guardada ni `Derivacion`. Mostrarle valor sin regalar lo que da ingreso (invitar a ingresar).
- **Cliente** (`Cliente.Rol` = cliente): dueño de la búsqueda; la comparte con un mecánico.
- **Mecánico** (misma entidad, otro rol): ve solo lo compartido con él. Confirma o corrige el
  diagnóstico (`CodigosRecomendados` va aparte de `CodigosObjetivo`: es el dato etiquetado de la IA).
  Plan Pro vs. gratuito limita la bandeja. Una derivación se le atribuye si la oferta es de una pieza
  que él recomendó: cuidar que no pueda atribuírsela a sí mismo.
- Si el mecánico corrige con piezas nuevas, todo lo que dependa de las piezas (ofertas, precios)
  tiene que existir también para las recomendadas.
- El ranking de ofertas depende **solo del precio**, nunca de quién paga ni del plan del mecánico.
- RepMatch no vende, no cobra, no guarda stock.

## 4. Uso real

Revisar, para el plan:

- **Latencia**: qué espera el usuario y cuánto. Timeout global, resultados parciales válidos.
- **Fallas**: un servicio externo caído degrada la pantalla, nunca la rompe (`ResultadoOperacion<T>`,
  aviso visible). Una fuente que falla no tumba a las otras.
- **Datos honestos**: monedas sin mezclar, lo que falta (ej. envío) se dice en pantalla, datos
  vencidos se marcan, coincidencias dudosas se advierten.
- **Estado vacío**: qué ve el usuario cuando no hay resultados, con un mensaje que explique por qué.
- **Doble acción y datos viejos**: botones deshabilitados mientras se trabaja; que el otro rol vea
  los cambios sin F5 (ver los errores conocidos que marcó Playwright).
- **Privacidad**: qué sale a terceros (nada personal: `FiltroDatosPersonales`, User-Agent genérico).
- **Uso responsable de APIs**: solo APIs oficiales, límite de pedidos, nunca scraping.

## 5. Contenido del plan

1. **Hallazgos** (código y API real) que cambian el diseño.
2. **Cambios por capa** respetando las dependencias: Domain/Common/Contracts sin referencias internas;
   mapeo en `Persistence/Configuraciones/`; registro con `Agregar*`; configuración por
   `Seccion__Clave`. Proyecto nuevo → `RepMatch.slnx` + `COPY` en cada Dockerfile.
3. **Integración**: si hay eventos de dominio, qué observador, en qué orden, y cómo se enchufa
   después en RabbitMQ sin tocar `ServicioBusquedas`.
4. **Qué ve cada rol** (la matriz del paso 3, resumida).
5. **Pruebas**: unitarias con fixtures reales; integración con `WebApplicationFactory`; E2E con
   Playwright **por rol** (anónimo, cliente, mecánico, y el ida y vuelta cliente ↔ mecánico) más el
   caso con el servicio caído. Los E2E no dependen de datos externos que cambian: usar un stub con
   fixtures y dejar la prueba contra el servicio real aparte.
6. **Docs a actualizar** (CLAUDE.md, "Al cambiar cosas, actualizar también"). El total de pruebas no
   se escribe a mano.
7. **Rama y PR**: de qué rama sale, qué issues cierra (`Closes`) y cuáles refiere (`Refs`), qué
   queda fuera de alcance.
8. **Decisiones abiertas**: pocas, cada una con opciones concretas y una recomendación.
