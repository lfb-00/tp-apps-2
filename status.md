# Status

Read this first. Snapshot 2026-09-28. English. Facts only. Intent and the next slice are in `plan.md`.

## Now

Branch work is uncommitted on `main` (last commit `85ff78d`). App is a Spanish Blazor Server UI over `FachadaAplicacion`. Catalog access is local or remote via `Catalogo:Modo`. Docker Compose defaults to remote.

Run: `docker compose up --build --wait` → web `http://localhost:8080`, catalog `http://localhost:8081/salud`, Postgres `localhost:5432`.

Tests: `docker compose --profile test build tests && docker compose --profile test run --rm tests` → 57 passed, 0 failed (2026-10-01). Without the build step, `run` reuses an old `repmatch/tests` image and reports a stale count.

Adding a vehicle to an existing client, or an offer to an existing search, works since 2026-10-01. Before, EF Core issued UPDATE instead of INSERT for those children (`DbUpdateConcurrencyException`). Domain ids are now `ValueGeneratedNever` in `RepMatchDbContext`.

Correction not built. `plan.md` says search is public and login needs a password. The app still does the opposite: `/` is an email gate, and `Cliente` stores `Email` only.

## User-visible

- `/` with no session is an email entry. A known email opens that person's account with no password. An unknown email asks for a name and creates the account. There is no list of all clients. This gate is what the next slice replaces.
- Signed in, `/` searches that person's vehicles only. Results are part cards. Recent searches are only theirs (`ListarBusquedasDelClienteAsync`). A person with no cars gets a link to their garage, not a client picker.
- `/clientes` is "Mi garage" for the signed-in person: their cars and a form to add one. It is not an admin desk.
- The header shows the person's name and Salir. The session id is in protected session storage (`SesionActual`).
- `/catalogo` is still a direct lookup, not tied to a person. Results use the same cards.
- `/busquedas` redirects to `/`.
- `/acceso` returns 404. Do not restore it.
- Free text is stored and ignored when choosing parts.
- No shop offers. `Busqueda.RegistrarOfertas` has no caller.

Seed data: Bruno Lo Faro `blofaro@uade.edu.ar` (Gol 2015 "El Gol", Peugeot 208 2019 "El 208"), Taller San Martin `contacto@tallersanmartin.com.ar` (Toyota Corolla 2018). Ana Sin Auto `ana.sinauto@ejemplo.com` was added later through the catalog API and has an empty garage.

## Assignment

TP Inicial and Primera Parte are implemented. Do not redo them.

| Item | State |
|---|---|
| .NET 10, Docker, NuGet, 6 library projects, layered app | done |
| Local vs remote catalog (`FabricaCatalogo`, `ICatalogoRepuestos`) | done. Evidence in `docs/evidencias/` and `GET /evidencia/catalogo`. Blazor-circuit latency numbers in the report are from an older run; the script regenerates the endpoint numbers |
| Class, component, deployment diagrams | `docs/diagramas/clases.md`, `componentes.md`, `despliegue.md` |
| Architecture report | `docs/informe-arquitectura.md` |
| Factory, Repository, Strategy | done |
| Observer | `DespachadorEventos`, `ObservadorCompatibilidad`, event `BusquedaCreada` |
| Facade | `FachadaAplicacion`. Razor pages must keep calling only this |
| Clients and searches services | done. No inventory service: the PDF name is an example |
| Layer diagram, sequence diagram, pattern write-up | `docs/diagramas/capas.md`, `docs/diagramas/secuencia.md`, `docs/patrones.md` |

`docs/` is Spanish and is the hand-in. Do not translate it.

## Do not

- Add SOAP, queues, external shop clients, or an LLM over the free text.
- Render prices or store names that were not captured.
- Explain layers, contracts, or local/remote in the UI.
- Add a stock or inventory service.

## Last verification

2026-09-28, browser on `http://localhost:8080` after the per-person session:

- Signed out, `/` is an email form. No client list. An unknown email asks for a name and does not create the account until that second step.
- Bruno sees only the Gol and the 208, and only his searches. A Gol search returns 7 part cards.
- Ana Sin Auto sees "Agregar un vehículo" and no submit button.
- Taller San Martin sees only the Corolla. Salir returns to the email form.
- A full load of `/busquedas` redirects to `/` and restores the same person from protected session storage.
- `/catalogo` still returns 7 cards for the Gol. `/clientes` lists only the signed-in person's cars.

Earlier the same day: 55 tests passed, before this session change. Docker was not serving `blazor.web.js` until `RequiresAspNetWebAssets` and `MapStaticAssets`.

Not checked: creating a brand-new account through the second step, duplicate email, mobile layout.
