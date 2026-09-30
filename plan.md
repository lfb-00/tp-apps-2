# Plan

For the next coding agent. Not a course deliverable. English. Update `status.md` when a fact changes. Do not narrate finished work here.

## Product

RepMatch compares auto parts. Anyone can search. They name the car, describe the problem, and see the parts that fit. Logging in is optional. It does not sell and it does not hold stock.

A signed-in person is the client. Login is email plus password. Email alone must not open an account. After login, the garage, the saved searches, and the name in the header are only that person's. Do not put a dropdown of all clients on the search. Do not list other people's cars or searches.

`/clientes` is that person's garage, not an admin registration desk. Signed out, that page is the way in to the account, not a list of clients.

The free-text problem is stored. It does not choose parts. Choice is by vehicle fitment against the own catalog.

## Quality bar

A session is done when the home page is the search and the result is part cards: name, system, description, which cars it fits, equivalent codes. Not a comma-separated list of canonical codes, and not the enum `Diagnosticada`.

UI copy is Spanish. Course files in `docs/` stay Spanish. This file and `status.md` stay English.

Do not fake a shop comparison. There are no prices and no stores until an offer is actually captured. `Busqueda.RegistrarOfertas` is unused. Do not render empty price columns.

## Constraints

Grading scope already covered: TP Inicial and Primera Parte (`TP (1).pdf`). Do not re-implement them.

Do not build Segunda Parte inside a UI pass: no SOAP, no queues, no external shop adapters, no model that reads the free text.

`Pedido`, `Inventario`, and `Clientes` in the PDF are examples. Do not add an inventory or stock service.

Do not put architecture, layers, or "local vs remote" in the UI. `/acceso` stays deleted. Evidence stays at `docs/evidencias/` and `GET /evidencia/catalogo`.

## Locked decisions

- Product UI: Spanish. Tracker files: English. `docs/`: Spanish.
- Home search works with no session. Login requires a password. The signed-in person is the client. Part cards, not an admin table of codes. No client picker.
- Services are `ServicioClientes` and `ServicioBusquedas`.
- `FachadaAplicacion` is the only type Razor pages call.
- `BusquedaCreada` is dispatched in-process to `ObservadorCompatibilidad`, which fills compatible codes.
- Sequence diagram: `docs/diagramas/secuencia.md`.

## Repo

```
src/ tests/     application
docs/           course deliverables (Spanish)
plan.md         intent (this file)
status.md       snapshot of what is true now
```

## Next

Modify the current email gate. It is the wrong product.

- `/` searches with no session. The signed-out form names the vehicle (marca, modelo, año, motor) and the problem, then shows the same part cards. Do not send a signed-out person to an email form first.
- Signed in, the vehicle can still come from that person's garage. Recent searches stay `ListarBusquedasDelClienteAsync`.
- Login and account creation ask for a password. Persist a hash on `Cliente`. `Cliente` has no password today, so seed accounts need one before email-only entry can be removed.
- Razor still calls only `FachadaAplicacion`. Do not add a second role for a garage operator.

## Later, not now

Store offers, message queues, SOAP, and interpreting the free text. Those are Segunda Parte. The search result stays "parts that fit this vehicle" until an offer exists.
