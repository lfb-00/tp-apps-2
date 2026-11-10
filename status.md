# Status

Read this first. Snapshot 2026-11-03. English. Facts only. Intent and the next slice are in `plan.md`.

## Now

App is a Spanish Blazor Server UI over `FachadaAplicacion`. Catalog access is local or remote via `Catalogo:Modo`. Docker Compose defaults to remote.

Run: `docker compose up --build --wait` → web `http://localhost:8080`, catalog `http://localhost:8081/salud`, Postgres `localhost:5432`.

Tests: `docker compose --profile test build tests && docker compose --profile test run --rm tests` is the canonical verification. Without the build step, `run` reuses an old `repmatch/tests` image and reports a stale count.

Adding a vehicle to an existing client, or an offer to an existing search, works since 2026-10-01. Before, EF Core issued UPDATE instead of INSERT for those children (`DbUpdateConcurrencyException`). Domain ids are now `ValueGeneratedNever` in `RepMatchDbContext`.

Public search and password login implemented 2026-10-03. `/` is now the search for everyone. Login moved to `/login` with email + password. `Cliente` stores `HashContrasena` (BCrypt). Seed accounts use password `repMatch123!`.

Profile, default vehicle and light/dark theme implemented 2026-10-05.

Roles implemented 2026-11-03. `Cliente.Rol` is `Cliente` or `Mecanico`, chosen at sign-up and never changed. A mechanic also has `NombreTaller`, `Especialidades` (`SistemaVehiculo` list) and `Plan` (`Gratuito`/`Pro`). Business model: affiliate commission per `Derivacion` + Pro plan for mechanics; changing plan charges nothing. Free plan: at most 5 unreviewed searches in the inbox (`Cliente.MaximoPendientesPlanGratuito`). Role and plan checks live in `ServicioRevisiones`/`ServicioMecanicos`, not in pages. New events `BusquedaCompartida`, `DiagnosticoValidado`, `DerivacionATienda` are dispatched after commit and have no observers yet.

AI diagnosis implemented 2026-11-04 (Segunda Parte slice 1). `IDiagnosticador` in Contracts. New library `RepMatch.IA`: `DiagnosticadorLlm` (OpenAI-compatible chat completions, Groq by default, Gemini by config), `DiagnosticadorLocal` (TF-IDF + 4-NN over the embedded `Datos/sintomas.json`, 41 phrases), `DiagnosticadorConRespaldo` (LLM, else local). New host `RepMatch.IA.Api` (`POST /api/diagnostico`, `/salud`, OpenAPI, no database). Web picks local or remote with `IA:Modo` (`FabricaDiagnostico`); `Catalogo.Api` always diagnoses in process. `ObservadorCompatibilidad` now goes through `ServicioDiagnostico`: fitment first, then the AI picks among those parts; codes outside the candidates are dropped; if the AI picks none, or the remote call fails, all fitting parts stay. `Busqueda` stores `SistemaDiagnosticado`, `Urgencia`, `CausasProbables`, `FuenteDiagnostico`. No LLM key is configured anywhere, so every run so far used `fallback-local`; the LLM path is only tested with recorded responses.

**Existing databases must be recreated after this change.** The schema comes from `EnsureCreatedAsync`, which does not add new tables or columns (`configuraciones_vehiculo`; since 2026-11-03 also `derivaciones` and the role/review columns; since 2026-11-04 the diagnosis columns of `busquedas`) to an existing volume. There are no EF migrations (do not add them without asking). Run `docker compose down -v` before `docker compose up --build --wait`; seed data is loaded again.

Data Protection keys live in the `claves-web` volume (`ProteccionDatos:DirectorioClaves`), so a tab keeps its session when the Web container is recreated. If a stored session cannot be decrypted anyway, `SesionActual` drops it and starts signed out.

## User-visible

- `/` is a public vehicle search for everyone, signed in or not. Marca, modelo, año y motor se seleccionan de dropdowns dependientes alimentados por el catálogo vehicular. No login required to search.
- `/login` is the new login and registration page. Email + password to sign in. "Crear cuenta" muestra nombre o apodo y la repetición obligatoria de contraseña.
- Signed in, `/` shows the garage picker instead of the vehicle form. Results are part cards. Recent searches are only theirs (`ListarBusquedasDelClienteAsync`). A person with no cars gets a link to their garage.
- Header shows "Ingresar" button when signed out. Signed in, it shows avatar (photo or initials) + name; the dropdown has "Mi perfil", "Mi garage", "Cerrar sesión". The mobile menu shows avatar, name and the same links.
- A sun/moon button in the header (and in the mobile menu) toggles light/dark on every page. Signed out it is stored in `localStorage` (`repmatch-tema`). Signed in it is also stored in `Cliente.TemaPreferido` (`claro`/`oscuro`); on login or session restore the saved value wins and is copied to `localStorage`. An inline script in `App.razor` sets `data-theme` on `<html>` before first paint (falls back to `prefers-color-scheme`). `wwwroot/tema.js` is called through `TemaActual`.
- `/perfil` (redirects to `/login` without a session): edit name, phone (+ WhatsApp flag), province (24 jurisdictions, stored as string), locality; email is read-only and has no change path anywhere. Profile photo (JPG/PNG/WEBP, max 5 MB, resized in the browser to 256x256 JPEG, stored as `bytea` in `clientes`). Default vehicle. Theme. Password change (checks the current one with BCrypt). Delete account: custom Blazor modal with password; deletes the client, garage and searches in one transaction, signs out and shows "Tu cuenta fue eliminada." on `/`.
- `/` preselects the requested garage vehicle (`?v=`), else the default vehicle, else the first one. `/clientes` tags the default vehicle "Predeterminado".
- `/clientes` is "Mi garage" for the signed-in person: their cars and a form to add one. It is not an admin desk.
- `/catalogo` is a direct lookup with optional garage shortcuts for a signed-in person. Results use the same cards.
- `/busquedas` redirects to `/`.
- `/acceso` returns 404. Do not restore it.
- Free text chooses parts through the AI. Signed in, it is stored with the diagnosis. Signed out, `FachadaAplicacion.DiagnosticarAsync` diagnoses it and nothing is stored.
- Above the part cards, `TarjetaDiagnostico` shows "Probablemente: <sistema>", an urgency tag and up to 3 causes. Unknown text shows "No reconocimos el problema" and all fitting parts. `/consultas/{id}` shows system, urgency and causes under "Diagnóstico propuesto". The source (`llm`/`fallback-local`) is not shown in the UI.
- No shop offers. `Busqueda.RegistrarOfertas` has no caller.
- Sign-up asks "Para mi auto" (client) or "Soy mecánico". A new mechanic lands on `/taller`; sign-in still goes to `/`.
- Each item in "Tus consultas" on `/` links to `/consultas/{id}`. The owner sees the proposed parts and either a mechanic directory (province filter, Pro first) with "Compartir", the shared mechanic with "Dejar de compartir", or the review. The assigned mechanic sees a form: confirm, or pick other parts from the catalog, plus an optional note. Anyone else gets "No encontramos esa consulta".
- `/taller` (mechanics only; header and menus show "Mi taller"): counters (unreviewed / limit, derivations attributed, plan), inbox, workshop name and specialties, "Pasar a Pro" / "Volver al plan gratuito".
- `GET /derivar/{ofertaId}` records a `Derivacion` and redirects to the shop; with no captured offers it returns 404 `{"error":"La oferta ya no existe."}`. `/consultas/{id}` only renders the offers list when offers exist.

Seed data: Bruno Lo Faro `blofaro@uade.edu.ar` (Gol 2015 "El Gol" as default, Peugeot 208 2019 "El 208"; phone `+54 9 11 1234-5678` with WhatsApp, Ciudad Autónoma de Buenos Aires, Monserrat), Taller San Martin `contacto@tallersanmartin.com.ar` (mechanic, free plan, "Taller San Martín", Frenos + Suspensión, San Nicolás CABA, Toyota Corolla 2018), Carla Benítez `carla@motoresbenitez.com.ar` (mechanic, Pro plan, "Motores Benítez", Motor + Refrigeración, Caballito CABA). All seed accounts use password `repMatch123!`.

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
| Facade | `FachadaAplicacion`. Razor pages use this for business operations; `SelectorVehiculo` loads dropdown options through `ICatalogoVehiculos` directly |
| Clients and searches services | done. No inventory service: the PDF name is an example |
| Layer diagram, sequence diagram, pattern write-up | `docs/diagramas/capas.md`, `docs/diagramas/secuencia.md`, `docs/patrones.md` |
| Segunda Parte: AI component (REST) | done 2026-11-04. Report 6.7 and section 7, sequence 7, patterns "El componente de IA y los mismos patrones" |
| Segunda Parte: SOAP, queue, external shop APIs, Segunda Parte report and evidence | not started (`plan.md` slices 2-5) |

`docs/` is Spanish and is the hand-in. Do not translate it.

## Do not

- Add SOAP, queues, external shop clients, or an LLM over the free text outside the slice order in `plan.md` (Segunda Parte, started 2026-11-03).
- Render prices or store names that were not captured.
- Explain layers, contracts, or local/remote in the UI.
- Add a stock or inventory service.

## Gotchas

- `clientes.VehiculoPredeterminadoId → vehiculos` (SET NULL) plus `vehiculos.ClienteId → clientes` (CASCADE) is an FK cycle. EF cannot order a single INSERT or DELETE of both rows (`circular dependency` on Postgres; InMemory does not notice). Seed saves the default in a second `SaveChanges`; `EliminarCuentaAsync` clears the default first inside `IUnitOfWork.EjecutarEnTransaccionAsync`.
- `busquedas.ClienteId` has no FK; account deletion removes them explicitly via `IBusquedaRepository.EliminarPorClienteAsync`.
- `busquedas.MecanicoId` and all `derivaciones` FKs are absent on purpose. Deleting a mechanic unshares their unreviewed searches; reviewed ones keep the review. Derivations survive any deletion (they are the commission record).
- Behind the BNA corporate proxy, Docker injects the proxy into builds and running containers. Builds fail at `dotnet restore` (`NU1301`, `PartialChain`) because the proxy re-signs TLS with the `BNA-SG` CA that WSL trusts but the SDK image does not. At runtime, the injected `NO_PROXY` lacks `catalogo-api` (and now `ia-api`), so Remote mode sends Web → API calls to the proxy (404/407) and a search crashes the circuit. The 2026-11-03 and 2026-11-05 runs used, outside the repo, Dockerfile copies that add the CA and a compose override that adds `catalogo-api,ia-api,postgres` to the Web's `NO_PROXY`. The repo files are unchanged.
- Driving Docker from Windows with one-shot `wsl -e …` calls: the WSL instance stops once no process is left, despite `instanceIdleTimeout=-1` in `.wslconfig`, and takes the containers with it (they show `Exited (0)`). Keep a `wsl -e sleep infinity` running while testing.
- EF owned types cannot share one `DatosVehiculo` instance between two `Busqueda` rows: tests seeding several searches must use a fresh instance each (`Gol with { }`).
- `SesionActual.RefrescarAsync()` reloads the client and fires `Cambio`; Home only resets its form when the client id changes, so a theme toggle does not clear results.
- Do not change the `@key` of the `InputFile` while a file is being read: the input element is replaced and reading fails.

## Last verification

2026-11-05, Docker 29.7.2 inside WSL, fresh volumes (`docker compose down -v`): test image built with 0 warnings, 137 tests passed. `docker compose up --build --wait` healthy for `postgres`, `catalogo-api`, `ia-api` and `web` (Remote catalog and Remote AI, no LLM key); the three images published with 0 warnings. `EnsureCreatedAsync` created the diagnosis columns of `busquedas` on Postgres (`SistemaDiagnosticado` and `Urgencia` varchar(20), `FuenteDiagnostico` varchar(30), `CausasProbables` text[]). Browser against `http://localhost:8080`: signed-out Gol 2015 "vibra el volante y tiembla el pedal al frenar" → Frenos, urgency media, 2 parts; signed in as the seed client, "pierde agua y se calienta cuando esta parado" → Refrigeración, urgency alta, 1 part, row stored as `Diagnosticada` with system, urgency, two causes, `fallback-local` and `{BOMB-AGUA-VW-16}`. Same correlation id in Web and IA.Api logs; 0 ERR/FTL lines in the three services; `ia-api` serves `/openapi/v1.json`. Same out-of-repo proxy tweaks as 2026-11-03, with `ia-api` added to the Web's `NO_PROXY`. Not checked: a real LLM key, mobile layout of the diagnosis card.

2026-11-04, after the AI slice: local `dotnet build` 0 warnings, 0 errors; `dotnet test` 137 passed, 0 failed, 0 skipped (InMemory, no network: LLM tests use recorded responses, IA.Api is hosted with `WebApplicationFactory` and an empty key). `docker compose config` valid in WSL. In-app browser against `dotnet run` (InMemory, catalog Local, `IA:Modo=Remoto` against `IA.Api` on 5083, no LLM key): signed-out Gol search "vibra el volante y tiembla el pedal" → Frenos, 2 parts; signed-in "pierde agua y se calienta parado" → Refrigeración, urgency alta, 1 part, diagnosis persisted and shown on `/consultas/{id}`; same correlation id in Web and IA.Api logs; with IA.Api stopped, the search shows all 7 fitting parts and logs a warning. All 14 Mermaid blocks in `docs/diagramas/` render with Mermaid 11.4.1. Not checked: Docker images (`ia-api` build and compose up), Postgres schema for the new columns, a real LLM key, mobile layout of the diagnosis card.

2026-11-03, Docker 29.7.2 inside WSL (Ubuntu 25.10), fresh volumes: test image built with 0 warnings, 98 tests passed. `docker compose up --build --wait` healthy on Postgres, Web in Remote mode. `EnsureCreatedAsync` created `derivaciones` and the role/review columns; `Especialidades` is stored as `integer[]` (enum values), unlike `Rol`/`Plan` (text). Browser against `http://localhost:8080`: sign-up as mechanic lands on `/taller` (free, 0/5); workshop name + specialties saved; plan changed to Pro; client search, directory (Pro first, filtered by the client's province), share with the free mechanic (1/5), confirm with a note, review and mechanic contact shown to the client; `/derivar` 404 for an unknown offer. Build needed temporary out-of-repo tweaks for the corporate network (see Gotchas). Not checked: mobile layout.

Earlier on 2026-11-03: local `dotnet build` with 0 warnings and 0 errors; `dotnet test` 98 passed, 0 failed, 0 skipped (InMemory). Docker was not available, so neither the Docker test run nor Postgres was exercised: the new schema (enum primitive collection for `Especialidades`, `derivaciones` table) was not yet verified on Npgsql (done later the same day, above). In-app browser against `dotnet run` (InMemory, Local): client search, share with the Pro mechanic, mechanic inbox, correction with two parts and a note, review rendered on the consultation page (mechanic view), `/derivar` 404 for an unknown offer, no server errors. Not checked: the review as seen by the client in the browser, sign-up as mechanic and plan change through the UI, mobile layout.

2026-10-11: `docker compose --profile test run --build --rm tests` rebuilt the current source with
0 warnings and 0 errors; 84 tests passed, 0 failed, 0 skipped. OpenAPI was regenerated from
the current API; historical latency logs remain dated 2026-10-05.

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
