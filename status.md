# Status

Read this first. Snapshot 2026-10-05. English. Facts only. Intent and the next slice are in `plan.md`.

## Now

Branch work is uncommitted on `main`. App is a Spanish Blazor Server UI over `FachadaAplicacion`. Catalog access is local or remote via `Catalogo:Modo`. Docker Compose defaults to remote.

Run: `docker compose up --build --wait` → web `http://localhost:8080`, catalog `http://localhost:8081/salud`, Postgres `localhost:5432`.

Tests: `docker compose --profile test build tests && docker compose --profile test run --rm tests` is the canonical verification. Without the build step, `run` reuses an old `repmatch/tests` image and reports a stale count.

Adding a vehicle to an existing client, or an offer to an existing search, works since 2026-10-01. Before, EF Core issued UPDATE instead of INSERT for those children (`DbUpdateConcurrencyException`). Domain ids are now `ValueGeneratedNever` in `RepMatchDbContext`.

Public search and password login implemented 2026-10-03. `/` is now the search for everyone. Login moved to `/login` with email + password. `Cliente` stores `HashContrasena` (BCrypt). Seed accounts use password `repMatch123!`.

Profile, default vehicle and light/dark theme implemented 2026-10-05 on branch `feature/perfil-usuario` (uncommitted).

**Existing databases must be recreated after this change.** The schema comes from `EnsureCreatedAsync`, which does not add the new `configuraciones_vehiculo` table to an existing volume. There are no EF migrations (do not add them without asking). Run `docker compose down -v` before `docker compose up --build --wait`; seed data is loaded again.

Data Protection keys live in the `claves-web` volume (`ProteccionDatos:DirectorioClaves`), so a tab keeps its session when the Web container is recreated. If a stored session cannot be decrypted anyway, `SesionActual` drops it and starts signed out.

## User-visible

- `/` is a public vehicle search for everyone, signed in or not. Marca, modelo, año y motor se seleccionan de dropdowns dependientes alimentados por el catálogo vehicular. No login required to search.
- `/login` is the new login and registration page. Email + password to sign in. "Crear cuenta" muestra nombre o apodo y la repetición obligatoria de contraseña.
- Signed in, `/` shows the garage picker instead of the vehicle form. Results are part cards. Recent searches are only theirs (`ListarBusquedasDelClienteAsync`). A person with no cars gets a link to their garage.
- Header shows "Ingresar" button when signed out. Signed in, it shows avatar (photo or initials) + name; the dropdown has "Mi perfil", "Mi garage", "Cerrar sesión". The mobile menu shows avatar, name and the same links.
- A sun/moon button in the header (and in the mobile menu) toggles light/dark on every page. Signed out it is stored in `localStorage` (`repmatch-tema`). Signed in it is also stored in `Cliente.TemaPreferido` (`claro`/`oscuro`); on login or session restore the saved value wins and is copied to `localStorage`. An inline script in `App.razor` sets `data-theme` on `<html>` before first paint (falls back to `prefers-color-scheme`). `wwwroot/tema.js` is called through `TemaActual`.
- `/perfil` (redirects to `/login` without a session): edit name, phone (+ WhatsApp flag), province (24 jurisdictions, stored as string), locality; email is read-only and has no change path anywhere. Profile photo (JPG/PNG/WEBP, max 5 MB, resized in the browser to 256x256 JPEG, stored as `bytea` in `clientes`). Default vehicle. Theme. Password change (checks the current one with BCrypt). Delete account: custom Blazor modal with password; deletes the client, garage and searches in one transaction, signs out and shows "Tu cuenta fue eliminada." on `/`.
- `/` preselects the default vehicle, else the first one. `/clientes` tags the default vehicle "Predeterminado".
- `/clientes` is "Mi garage" for the signed-in person: their cars and a form to add one. It is not an admin desk.
- `/catalogo` is still a direct lookup, not tied to a person. Results use the same cards.
- `/busquedas` redirects to `/`.
- `/acceso` returns 404. Do not restore it.
- Free text is stored and ignored when choosing parts.
- No shop offers. `Busqueda.RegistrarOfertas` has no caller.

Seed data: Bruno Lo Faro `blofaro@uade.edu.ar` (Gol 2015 "El Gol" as default, Peugeot 208 2019 "El 208"; phone `+54 9 11 1234-5678` with WhatsApp, Ciudad Autónoma de Buenos Aires, Monserrat), Taller San Martin `contacto@tallersanmartin.com.ar` (Toyota Corolla 2018). All seed accounts use password `repMatch123!`.

## Assignment

TP Inicial and Primera Parte are implemented. Do not redo them.

| Item | State |
|---|---|
| .NET 10, Docker, NuGet, 6 library projects, layered app | done |
| Local vs remote catalog (`FabricaCatalogo`, `ICatalogoRepuestos`) | done. Evidence in `docs/evidencias/` and `GET /evidencia/catalogo`, regenerated 2026-10-05 (steady state, median of calls 3-12: Local 2.1 ms, Remote 7.4 ms). The report's old Blazor-circuit table was removed: it came from `/acceso` and cannot be reproduced |
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

## Gotchas

- `clientes.VehiculoPredeterminadoId → vehiculos` (SET NULL) plus `vehiculos.ClienteId → clientes` (CASCADE) is an FK cycle. EF cannot order a single INSERT or DELETE of both rows (`circular dependency` on Postgres; InMemory does not notice). Seed saves the default in a second `SaveChanges`; `EliminarCuentaAsync` clears the default first inside `IUnitOfWork.EjecutarEnTransaccionAsync`.
- `busquedas.ClienteId` has no FK; account deletion removes them explicitly via `IBusquedaRepository.EliminarPorClienteAsync`.
- `SesionActual.RefrescarAsync()` reloads the client and fires `Cambio`; Home only resets its form when the client id changes, so a theme toggle does not clear results.
- Do not change the `@key` of the `InputFile` while a file is being read: the input element is replaced and reading fails.

## Last verification

2026-10-05, headless Chrome (Playwright) against the web container on Postgres, fresh database: theme toggle signed out (persisted, no flash on reload), login applies saved theme, avatar and name refresh without reload, profile save, phone error, >5 MB and GIF rejected, PNG upload + preview + save + remove, default vehicle reflected on `/` and `/clientes`, wrong current password, `/perfil` redirect, new account deleted (modal focus, Escape, outside click, Cancelar, wrong password stays open; afterwards 0 orphan vehicles/searches), 375px layout with no horizontal scroll, no console errors. Logs contain no phone, password or image bytes.

Earlier verification:



2026-09-28, browser on `http://localhost:8080` after the per-person session:

- Signed out, `/` is an email form. No client list. An unknown email asks for a name and does not create the account until that second step.
- Bruno sees only the Gol and the 208, and only his searches. A Gol search returns 7 part cards.
- Ana Sin Auto sees "Agregar un vehículo" and no submit button.
- Taller San Martin sees only the Corolla. Salir returns to the email form.
- A full load of `/busquedas` redirects to `/` and restores the same person from protected session storage.
- `/catalogo` still returns 7 cards for the Gol. `/clientes` lists only the signed-in person's cars.

Earlier the same day: 55 tests passed, before this session change. Docker was not serving `blazor.web.js` until `RequiresAspNetWebAssets` and `MapStaticAssets`.

Not checked: duplicate email. WEBP upload and a real camera JPEG were not exercised (the PNG path goes through the same `RequestImageFileAsync`).
