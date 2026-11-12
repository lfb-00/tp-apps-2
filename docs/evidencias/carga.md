# Pruebas de carga básica

Corrida reproducible con Docker Compose y k6 0.54.0, el 2026-11-12. El entorno de referencia
fue el Compose local de RepMatch; catálogo y diagnóstico usaron el corpus/semilla local. La clave
del LLM se dejó vacía, por lo que la prueba de IA mide `fallback-local`, no la cuota del proveedor.

## Resultados

Percentiles en milisegundos. La métrica de catálogo mide el tiempo total de la petición HTTP a
`/evidencia/catalogo`; la de diagnóstico mide `POST /api/diagnostico`. Cada escenario HTTP usó 10
VUs, ramp-up de 10 s, 30 s a carga constante y ramp-down de 5 s.

| Escenario | Muestras | p50 | p95 | p99 | Errores |
|---|---:|---:|---:|---:|---:|
| Catálogo Local | 3.606 | 3,43 | 5,57 | 8,18 | 0 |
| Catálogo Remoto | 3.513 | 5,80 | 10,26 | 16,35 | 0 |
| Diagnóstico fallback-local | 3.699 | 1,09 | 2,04 | 3,25 | 0 |
| Publicación a RabbitMQ por Management API | 200 | 9,84 | 15,92 | 20,45 | 0 |

El escenario de cola publicó una ráfaga de 200 mensajes con 10 VUs. RabbitMQ enrutó todos y
`repmatch.actividad` llegó a cero mensajes listos y sin confirmar **6 ms después de la última
publicación**. Ese valor es una única medición de drenado del lote, no un percentil.

## Lectura y límites

- Bajo 10 VUs, el catálogo Remoto tuvo p95 4,69 ms mayor que Local (aproximadamente 84 %). Es el
  costo observable del salto HTTP en esta corrida; no hubo errores ni evidencia de saturación.
- El diagnóstico usa el clasificador local porque no se habilitó el proveedor externo. No permite
  inferir límites ni cuota del free tier; para esa prueba se requiere configurar el LLM y ejecutar
  de forma explícita con `IA_CARGA_LLM=1`.
- Los mensajes se publicaron por la API HTTP de administración de RabbitMQ para generar una ráfaga
  sin credenciales de servicios externos. La latencia de publicación incluye ese endpoint HTTP y no
  representa el tiempo del productor `BusRabbitMq`.
- Los picos máximos no se usan como percentiles; los archivos JSON conservan el resumen completo
  generado por k6.

## Reproducir

Desde la raíz del repo, con Docker disponible:

```bash
bash scripts/carga.sh
```

En Windows PowerShell, usá el lanzador del repo para invocar Git Bash de forma explícita:

```powershell
.\scripts\bash.ps1 scripts\carga.sh
```

Requiere Git for Windows. El lanzador también configura `MSYS_NO_PATHCONV=1`, necesario para que
Git Bash no transforme las rutas que Docker Compose recibe.

El comando levanta o actualiza el Compose (Web principal en Remoto y `web-local` en Local) y deja
resúmenes k6 en `docs/evidencias/carga-*.json`. No elimina volúmenes ni baja los servicios al
terminar. Variables opcionales: `VUS`, `DURACION`, `MENSAJES` y `VUS_COLA`. `IA_CARGA_LLM=1`
habilita el LLM configurado y puede consumir cuota.

Archivos de esta corrida:

- [Catálogo Local](./carga-catalogo-local.json)
- [Catálogo Remoto](./carga-catalogo-remoto.json)
- [Diagnóstico IA](./carga-diagnostico.json)
- [Ráfaga y drenado de RabbitMQ](./carga-cola.json)
