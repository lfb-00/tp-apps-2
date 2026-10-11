# Guion de presentación de RepMatch

Duración orientativa: 10 a 12 minutos. Las diapositivas incluyen estas notas del presentador.

## 1. RepMatch

Presentamos RepMatch, nuestro trabajo de Desarrollo de Aplicaciones II en UADE. La visión del producto es comparar repuestos automotrices y derivar al vendedor. Esta presentación cubre el TP Inicial y la Primera Parte, con una aplicación funcional de catálogo por compatibilidad. La comparación de ofertas y la IA pertenecen a la siguiente etapa.

## 2. Alcance de la aplicación

El problema es identificar una pieza que sea compatible con el vehículo. Hoy resolvemos el catálogo vehicular, las piezas compatibles y el historial por cliente. El texto del problema se guarda para usuarios registrados, pero no selecciona piezas ni produce un diagnóstico. La visión futura agrega IA, precios de tiendas y comparación de ofertas. RepMatch deriva al vendedor y no administra ventas ni stock.

## 3. Funcionalidad disponible

La búsqueda pública funciona sin cuenta. El catálogo ofrece filtros de vehículo y sistema, y también permite listar todas las piezas. Con cuenta, el usuario administra su garage y consulta su historial. El perfil incluye datos de contacto, foto, vehículo predeterminado, contraseña y tema. El vehículo del garage se usa como atajo en el catálogo y en la búsqueda principal.

## 4. Happy path: una búsqueda con cuenta

Para la demo usamos la cuenta semilla de Bruno, cuyo vehículo predeterminado es el Gol 2015 1.6. Ingresamos, verificamos que el Gol aparece seleccionado y escribimos un problema de al menos 15 caracteres, por ejemplo: escucho un ruido al frenar desde ayer. El usuario pulsa buscar. La aplicación valida el vehículo, guarda la búsqueda, asigna códigos compatibles y devuelve las fichas de piezas. El texto se guarda como contexto, pero no restringe el resultado al sistema de frenos. El historial se actualiza para el cliente actual.

## 5. Ejemplo de resultado para el Gol 2015

Estos ejemplos salen de los datos semilla del catálogo. Las pastillas delanteras usan el código canónico PAST-VW-GOL-DEL y equivalencias OEM y de fabricante. El filtro de aceite tiene una aplicación específica al motor 1.6. La bomba de agua también pertenece a ese motor. Las fichas muestran nombre, descripción, sistema, aplicaciones y equivalencias. La búsqueda principal devuelve todas las piezas compatibles, mientras que el catálogo permite restringir por sistema. En esta etapa no hay precios ni ofertas capturadas de tiendas.

## 6. Casos de uso como ejemplos

Un visitante puede consultar un Peugeot 208 2019 1.6 sin registrarse. El formulario pide detalle del problema, pero esta consulta no envía ni persiste ese texto. Un usuario que conoce el sistema entra al catálogo y filtra un Gol por Frenos, lo que muestra las pastillas y los discos compatibles. Un cliente registrado puede agregar un Toyota Corolla 2018 1.8 al garage, asignarle un alias y usarlo después en sus búsquedas. La app verifica que la configuración exista y rechaza vehículos repetidos en el mismo garage.

## 7. Validaciones y caminos alternativos

Las validaciones están repartidas por responsabilidad. La interfaz verifica la selección y la extensión mínima del texto. Los servicios validan DTOs y existencia de clientes o configuraciones. El dominio protege invariantes como vehículo duplicado y años permitidos. Si no hay compatibles, la búsqueda registrada pasa a Fallida. Si el catálogo falla dentro del observador, la app guarda una falla y confirma el cambio. Un fallo en una consulta posterior de la fachada puede mostrar un error aunque la búsqueda ya esté guardada. Evitamos prometer que todos los errores borran o revierten la búsqueda.

## 8. Arquitectura en capas

La Web utiliza la fachada para las operaciones de negocio. La excepción deliberada es SelectorVehiculo, que consulta ICatalogoVehiculos para cargar sus opciones. La aplicación coordina servicios y eventos. Las entidades y las interfaces de repositorio viven en Domain. Persistence implementa esos contratos con EF Core. El dominio no referencia EF Core ni ASP.NET. Contracts contiene los DTOs y los contratos de catálogo. Common concentra configuración, validación y logging.

## 9. Un contrato, dos modos de acceso

ICatalogoRepuestos es el contrato que usan la fachada y el observador. En modo Local se enlaza con CatalogoLocal dentro del proceso. En modo Remoto se enlaza con CatalogoRemoto, un HttpClient tipado que llama a la API. La API resuelve siempre CatalogoLocal. FabricaCatalogo elige el modo en el arranque mediante Catalogo:Modo y registra ambos concretos. ICatalogoVehiculos tiene el mismo par local y remoto. Esto aplica Factory, Strategy y Adapter sin modificar a los consumidores.

## 10. La búsqueda activa un evento de dominio

ServicioBusquedas construye la entidad, copia sus eventos pendientes y guarda la búsqueda primero. Después limpia el buzón y despacha BusquedaCreada. DespachadorEventos selecciona al observador compatible. ObservadorCompatibilidad lee la búsqueda, consulta el catálogo y asigna los códigos objetivo. Si encuentra piezas, el estado pasa de Pendiente a Diagnosticada. Si no encuentra o falla el catálogo, pasa a Fallida. El observador confirma los cambios. Finalmente, la fachada obtiene las piezas completas para mostrarlas. El evento de esta entrega se procesa dentro del proceso, sin cola RabbitMQ.

## 11. Patrones aplicados

Cada patrón resuelve un problema concreto. Factory concentra la elección del catálogo en el arranque. Strategy hace intercambiables los accesos local y remoto. Repository evita que las entidades conozcan el motor de datos, y Unit of Work confirma cambios. Observer desacopla el procesamiento posterior de BusquedaCreada. Facade coordina operaciones de clientes, búsquedas y catálogo para la presentación. CatalogoRemoto además actúa como Adapter del contrato HTTP.

## 12. Despliegue de laboratorio

Docker Compose levanta Web, API de catálogo y PostgreSQL en una red bridge. La Web se publica en el puerto 8080 y la API en el 8081 del host, aunque ambas aplicaciones usan el 8080 dentro de su contenedor. Web y API acceden a PostgreSQL. El modo por defecto de la Web es Remoto, con http://catalogo-api:8080 como URL interna. Los volúmenes guardan la base y las claves de Data Protection. El perfil local permite demostrar el camino directo desde otra instancia de la Web en el puerto 8090.

## 13. Guion de la demo

Con el entorno ya iniciado, comenzamos en localhost:8080 con una búsqueda pública para el Gol 2015 1.6. Luego ingresamos con la cuenta de demo y una contraseña configurada en el entorno de laboratorio. Mostramos el garage y el vehículo predeterminado. Ejecutamos una búsqueda con una descripción suficiente y mostramos las piezas y el historial propio. En el catálogo filtramos por Frenos para mostrar un resultado distinto. Cerramos enseñando /salud y /openapi/v1.json en la API, y la evidencia de acceso local o remoto disponible en /evidencia/catalogo. La última ejecución de pruebas registrada en status.md, del 11 de octubre de 2026, informa 84 pruebas aprobadas. 

## 14. Estado y próximas etapas

El TP Inicial y la Primera Parte están implementados: componentes, arquitectura en capas, acceso local y remoto y patrones. La última verificación documentada informa 84 pruebas aprobadas y un build sin advertencias. Para la Segunda Parte se prevén SOAP, productores y consumidores RabbitMQ, fuentes externas de ofertas y el componente de IA. El diseño permite extender el contrato de catálogo con otra estrategia y reemplazar el despacho actual por una publicación a cola. La visión final es un comparador de ofertas con diagnóstico, preservando la derivación al vendedor.

## Preparación de la demo

1. Levantar el entorno con `docker compose up --build --wait`.
2. Abrir `http://localhost:8080`.
3. Usar la cuenta de demo configurada en el entorno de laboratorio.
4. Buscar Volkswagen Gol, año 2015, motor 1.6. Descripción: “Escucho un ruido al frenar desde ayer”.
5. Mostrar las piezas compatibles y el historial. Aclarar que el texto no selecciona piezas en esta etapa.
6. En `/catalogo`, filtrar el mismo vehículo por `Frenos`: pastillas y discos compatibles.
7. Mostrar `http://localhost:8081/salud` y `http://localhost:8081/openapi/v1.json`.
8. Evidencia del modo activo: `http://localhost:8080/evidencia/catalogo`.

Los resultados de pruebas citados son históricos, documentados el 11/10/2026 en `status.md`.
Los diagramas completos en Mermaid y SVG están en `../diagramas/`.

