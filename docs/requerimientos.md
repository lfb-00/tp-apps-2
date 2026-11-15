# Requerimientos — RepMatch

Análisis de requerimientos funcionales y no funcionales que pide el Integrador, incluidos los del
componente de IA (latencia, exactitud mínima y privacidad). Cada requerimiento dice cómo se
verifica: una prueba automatizada, una evidencia de `docs/evidencias/` o, cuando la garantía sale
del diseño, el contrato o la clase que la impone.

Las pruebas de punta a punta (`tests/RepMatch.E2E/Pruebas/`) se citan como `Clase.Metodo`; las
demás pruebas viven en `tests/RepMatch.Tests/`. Cómo correr cada grupo: secciones 5.4 a 5.7 del
[informe](informe-arquitectura.md).

## Alcance y restricciones

RepMatch **compara** ofertas de tiendas reales y deriva al vendedor: no vende, no cobra y no guarda
stock. No hay carrito, pagos propios ni inventario. El ingreso es la comisión de afiliado por cada
derivación y el plan Pro del mecánico, sin cobro real.

| Actor | Qué puede hacer |
|---|---|
| Anónimo | Buscar por síntoma y vehículo, ver el diagnóstico, las piezas compatibles y "desde $X en N tiendas", recorrer el catálogo |
| Cliente | Lo anterior, más guardar consultas, administrar su garage y perfil, ver precios por tienda, derivar a la tienda y compartir una consulta con un mecánico |
| Mecánico | Atender las consultas que le comparten (confirmar o corregir el diagnóstico), administrar su taller y su plan |
| Sistemas externos | Tiendas VTEX (precios), proveedor de LLM (diagnóstico), clientes del catálogo por REST o SOAP |

## Requerimientos funcionales

| Id | Requerimiento | Verificación |
|---|---|---|
| RF-01 | Registrarse como cliente o mecánico, ingresar y cerrar sesión. Un email repetido, una contraseña corta o dos contraseñas distintas se rechazan con un mensaje. | `AutenticacionTests` (11 casos) |
| RF-02 | Las pantallas privadas exigen sesión y cada persona ve solo lo suyo: un cliente no ve consultas ajenas ni usa el taller; el mecánico ve una consulta solo si se la compartieron, sin el email ni el garage del cliente. | `PermisosTests` |
| RF-03 | Elegir el vehículo por marca, modelo, año y motor entre las combinaciones del catálogo, y guardarlo en el garage del cliente. | `CatalogoYGarageTests`, `PerfilTests.El_vehiculo_predeterminado_es_el_que_aparece_elegido_al_buscar` |
| RF-04 | Buscar sin sesión describiendo el problema en texto libre: se muestran el diagnóstico, las piezas compatibles y el precio mínimo con la cantidad de tiendas, sin guardar el texto. | `BusquedaTests`, `IATests.Sin_sesion_un_sintoma_de_frenos_muestra_el_diagnostico_y_solo_piezas_de_frenos`, `OfertasTests.Sin_sesion_muestra_desde_cuanto_pero_no_donde` |
| RF-05 | Con sesión, la búsqueda queda guardada en "tus consultas" con su diagnóstico, sus piezas y sus precios. | `BusquedaTests.Con_sesion_la_busqueda_queda_en_tus_consultas_y_se_puede_abrir`, `IATests.El_diagnostico_queda_guardado_en_la_consulta` |
| RF-06 | Diagnosticar el texto libre: sistema afectado, urgencia, causas probables y piezas recomendadas, elegidas solo entre las compatibles con el vehículo. Un texto que no describe un problema no recibe diagnóstico. | `IATests`, `DiagnosticoLocalTests`, `DiagnosticoLlmTests`, `ValidacionIATests` |
| RF-07 | Comparar los precios de las piezas en las tiendas, separando coincidencias exactas (mismo EAN) de probables, de la más barata a la más cara. Una pieza que ninguna tienda publica se informa como tal. | `OfertasTests`, `ServicioOfertasTests`, `ValidadorCandidatoOfertaTests` |
| RF-08 | Derivar al cliente a la publicación de la tienda registrando la derivación, que llega por la cola al registro de comisiones. El clic del mecánico no se atribuye. | `OfertasTests.Ir_a_la_tienda_registra_la_derivacion_y_redirige_a_la_publicacion`, `ColaTests.Ir_a_la_tienda_llega_por_la_cola_al_registro_de_comisiones`, `OfertasTests.El_mecanico_ve_los_precios_con_enlace_directo_y_sin_derivacion_a_su_nombre` |
| RF-09 | Compartir una consulta con un mecánico, que la confirma o la corrige eligiendo otras piezas. La revisión queda guardada aparte del diagnóstico de la IA y no se puede revisar dos veces. | `MecanicoTests` |
| RF-10 | Plan del mecánico: el gratuito atiende hasta cinco consultas; el Pro, sin tope y destacado en el directorio. Sin cobro real. | `MecanicoTests.El_plan_gratuito_rechaza_la_sexta_consulta_y_el_Pro_la_acepta`, `MecanicoTests.El_mecanico_Pro_aparece_primero_y_destacado_en_el_directorio` |
| RF-11 | Administrar el perfil (datos de contacto, foto, tema, contraseña) y eliminar la cuenta con la contraseña. | `PerfilTests` |
| RF-12 | Consultar el catálogo de repuestos y su compatibilidad por REST y por SOAP, con contrato publicado (OpenAPI y WSDL). | `AccesoLocalVsRemotoTests`, `AccesoSoapTests`, `ServicioSoapTests`; evidencias `05`, `10` y `11` |
| RF-13 | Publicar los eventos de negocio (búsqueda creada, compartida, diagnóstico validado, derivación) para que otros servicios los consuman sin acoplarse a la Web. | `PublicadorEventosTests`, `FlujoDistribuidoTests`, `ColaTests`; evidencias `20` a `22` |

## Requerimientos no funcionales

| Id | Categoría | Requerimiento | Verificación |
|---|---|---|---|
| RNF-01 | Disponibilidad | Si se cae la IA, el catálogo, el servicio SOAP, las tiendas o el broker, la aplicación no se cae: la búsqueda sigue con lo que haya y avisa qué falta. | `ResilienciaTests`, `OfertasTests.Con_las_tiendas_caidas_se_ven_las_piezas_y_un_aviso`, `ColaTests.Con_la_cola_caida_buscar_compartir_y_salir_a_la_tienda_siguen_andando` |
| RNF-02 | Rendimiento | Consulta de compatibilidad con p95 menor a 100 ms con 10 usuarios concurrentes, local y remota. | `scripts/carga.sh`; medido el 2026-11-12: p95 de 5,57 ms (local) y 10,26 ms (remoto), sin errores ([`carga.md`](evidencias/carga.md)) |
| RNF-03 | Rendimiento | Las piezas se muestran sin esperar a las tiendas; los precios llegan después, con un tope global de 8 s (`Ofertas:TimeoutGlobalSegundos`) y 5 s por tienda (`Ofertas:Tiendas:*:TimeoutSegundos`). | `OfertasTests.El_cliente_ve_las_ofertas_de_la_mas_barata_a_la_mas_cara_despues_de_las_piezas`, `ServicioOfertasTests` |
| RNF-04 | Confiabilidad | Mensajería al menos una vez: confirmación después de procesar, un reintento y luego a la cola de mensajes fallidos (DLQ); el consumidor de comisiones es idempotente. | `ContratoMensajesTests.Reintenta_una_vez_y_despues_manda_a_la_DLQ`, `ConsumidorComisionesTests`, `ColaTests.La_topologia_de_la_cola_existe_con_colas_durables_y_sus_DLQ` |
| RNF-05 | Trazabilidad | Cada petición lleva un id de correlación que viaja de la Web al catálogo REST, al SOAP, a la IA y por la cola hasta el worker. | `InterfazTests.La_Web_asigna_un_correlation_id_y_respeta_el_que_recibe`; evidencias `02`/`03`, `12`/`13` y `20`/`21` |
| RNF-06 | Configuración | Toda la configuración se puede sobreescribir por variables de entorno (`Seccion__Clave`) sin recompilar: modos de catálogo e IA, proveedor de base, tiendas, timeouts y broker. | README, sección "Configuración"; `docker-compose.yml` |
| RNF-07 | Seguridad | Contraseñas guardadas con BCrypt; la clave del LLM solo por entorno o `.env`, fuera del repositorio; las ofertas solo aceptan URLs http o https, porque terminan como enlaces. | `ServicioClientes` (BCrypt), `.gitignore`, `DominioTests` |
| RNF-08 | Integración | Las tiendas se consultan solo por sus APIs públicas oficiales, nunca por scraping. | Sección 6.1 del informe; `FuenteVtex` usa `api/catalog_system/pub/products/search` (evidencia `23`) |
| RNF-09 | Portabilidad | Se levanta completa en un laboratorio con solo Docker (`docker compose up --build --wait`); también corre sin Docker con base en memoria. | [Manual de despliegue](manual-despliegue.md); pruebas en Docker con `docker compose --profile test run --rm tests` |
| RNF-10 | Mantenibilidad | La solución compila sin advertencias; dominio, utilidades y contratos no dependen de otros proyectos de la solución. | `dotnet build`; sección 2 del informe |

## Requerimientos del componente de IA

Los mínimos de exactitud se miden con `ValidacionIATests` sobre un conjunto de 60 casos etiquetados
a mano ([`tests/fixtures/ia/validacion-diagnostico.json`](../tests/fixtures/ia/validacion-diagnostico.json)):
54 síntomas de los nueve sistemas y 6 textos que no son síntomas, que tienen que quedar sin
diagnóstico. Las frases se escribieron aparte del corpus del clasificador, con otras palabras para
los mismos problemas, y una prueba verifica que no repitan ninguna. La validación del LLM real no
corre por defecto porque gasta cuota: se habilita con `REPMATCH_VALIDAR_LLM=1` y la clave en
`IA__Llm__ApiKey`.

| Id | Requerimiento | Verificación | Medición |
|---|---|---|---|
| RNF-IA-01 | **Exactitud mínima:** el sistema correcto en al menos el 80 % de los casos del conjunto de validación. Incluye no diagnosticar los textos que no son síntomas. | `ValidacionIATests` | Clasificador local, 2026-11-14, con el corpus ampliado: 81,7 % — cumple (antes, 76,7 %; ver abajo). LLM: sin medir. |
| RNF-IA-02 | **Urgencia:** en al menos el 80 % de los síntomas, la urgencia a un nivel o menos de la del mecánico (Baja, Media, Alta, Crítica), y **ningún** caso Alta o Crítica clasificado como Baja. | `ValidacionIATests` | Clasificador local, 2026-11-14, con el corpus ampliado: 98,1 %, pero **1 subestimación — no cumple** (ver abajo). |
| RNF-IA-03 | **Latencia:** el clasificador local responde con p95 menor a 50 ms. El LLM se abandona a los 4 s (`IA:Llm:TimeoutSegundos`) y responde el clasificador local; la Web espera a `IA.Api` hasta 10 s (`IA:TimeoutSegundos`). | `ValidacionIATests`, `DiagnosticoLocalTests.Responde_en_menos_de_50_ms`, `scripts/carga.sh` | Local: p95 de 0,1 ms en la validación y 2,04 ms con 10 usuarios concurrentes ([`carga.md`](evidencias/carga.md)). |
| RNF-IA-04 | **Siempre hay respuesta:** sin clave, con error de red, timeout o una respuesta fuera del esquema, responde el clasificador local y la respuesta lo indica en `fuente`. Si la IA no responde, quedan todas las piezas compatibles. | `DiagnosticoLlmTests`, `BusquedaConIATests.Si_la_IA_esta_caida_la_busqueda_sale_por_compatibilidad`, `ResilienciaTests.Con_la_IA_caida_la_busqueda_sin_sesion_muestra_todas_las_compatibles` | — |
| RNF-IA-05 | **Solo piezas reales:** la IA elige entre las piezas compatibles que le pasa el catálogo; un código que no está en esa lista se descarta. | `DiagnosticoLlmTests.Interpreta_una_respuesta_valida_y_descarta_codigos_inventados`, `DiagnosticoLocalTests.Solo_devuelve_codigos_de_los_candidatos` | — |
| RNF-IA-06 | **Privacidad:** el texto sale hacia el proveedor sin mails ni teléfonos; los logs registran su largo y no su contenido; los mensajes de la cola llevan ids y datos del vehículo, nunca el texto; sin sesión, el texto no se guarda. | `DiagnosticoLocalTests.Saca_mails_y_telefonos_pero_no_anios_ni_kilometros`, `DiagnosticoLlmTests.Llama_al_proveedor_con_la_clave_y_sin_datos_de_contacto`. Por diseño: `DiagnosticadorConRespaldo` (log con `texto=N caracteres`, evidencia `20`) y los contratos de `EventosIntegracion`, sin campo de texto (evidencia `21`). | — |

### Estado de RNF-IA-01 y RNF-IA-02

La primera medición del clasificador local dio 76,7 % de exactitud de sistema: 46 de 60. Los
errores se concentraban en los sistemas con una o dos frases en el corpus
(`src/RepMatch.IA/Datos/sintomas.json`): Dirección (1), Escape (1), Transmisión (2) y Neumáticos
(2). Con k = 4 vecinos, esos sistemas perdían la votación contra Frenos y Motor, que tienen 9 y 11
frases.

Se sumaron 20 frases, 5 por cada uno de esos sistemas, escritas desde los síntomas típicos y sin
copiar la validación. La exactitud de sistema subió a 81,7 % (49 de 60) y cumple RNF-IA-01, pero
apareció una subestimación grave: "suena un golpeteo fuerte adentro del motor" (Alta) ahora sale
como Escape con urgencia Baja, porque comparte "suena" y "fuerte" con la frase nueva "suena mas
fuerte el escape cuando acelero". RNF-IA-02 no se cumple hasta corregirlo.

La prueba del clasificador local sigue con `Skip` y la causa en un comentario, como el resto de
las pruebas que encontraron un error real. Ojo al corregir: cada ajuste del corpus hecho mirando
los errores de la validación la vuelve menos independiente. Para que la medición siga valiendo,
conviene sumar casos nuevos al conjunto de validación, escritos por alguien que no haya visto el
corpus, y volver a medir. La prueba `El_conjunto_de_validacion_no_repite_frases_del_corpus`
impide que se repitan frases textualmente.

El mínimo aplica también al LLM, que es el que responde en producción cuando hay clave; el
clasificador local es su respaldo y su línea base.
