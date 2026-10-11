# Diagramas de secuencia

---

## 1. Registrar una búsqueda

Caso de la Primera Parte: el cliente elige un vehículo del garage, describe el problema, y la
búsqueda queda guardada con los códigos de las piezas compatibles. El texto libre se persiste. La
elección de piezas la hace el observador de `BusquedaCreada`, por compatibilidad de vehículo; la
fachada después arma las piezas completas para mostrarlas.

![Diagrama secuencia-busqueda](imagenes/secuencia-busqueda.svg)

[SVG](imagenes/secuencia-busqueda.svg) · [Fuente Mermaid](fuentes/secuencia-busqueda.mmd)

---

## 2. Autenticación (login)

El usuario ingresa con email y contraseña. La contraseña nunca se almacena: solo su hash BCrypt.
`SesionActual` guarda el id del cliente en el almacenamiento de sesión del browser, cifrado por
ASP.NET Core Data Protection. El registro de una cuenta nueva usa otro camino desde la misma
pantalla: `RegistrarClienteAsync` (validación FluentValidation, rechazo de email duplicado y
`BCrypt.HashPassword`) y después `EntrarAsync`.

![Diagrama secuencia-login](imagenes/secuencia-login.svg)

[SVG](imagenes/secuencia-login.svg) · [Fuente Mermaid](fuentes/secuencia-login.mmd)

---

## 3. Alta de un vehículo

El cliente agrega un auto a su garage. Las reglas de negocio viven en el dominio: marca y modelo
obligatorios y año entre 1950 y el año actual + 1 (`DatosVehiculo`), vehículo no repetido para el
mismo cliente (`Cliente.AgregarVehiculo`) y VIN válido si se informa (`Vehiculo`; la Web no manda
VIN). `AgregarVehiculoAsync` valida el DTO con FluentValidation, verifica que exista la
configuración en el catálogo y controla que el alias no supere 120 caracteres. Luego busca el cliente,
delega en el dominio y persiste.

![Diagrama secuencia-alta-vehiculo](imagenes/secuencia-alta-vehiculo.svg)

[Fuente Mermaid](fuentes/secuencia-alta-vehiculo.mmd)
