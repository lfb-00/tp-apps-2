
# Plan

For the next coding agent. Not a course deliverable. English. Update `status.md` when a fact changes. Do not narrate finished work here.

## Product

RepMatch compares auto parts. Anyone can search. They name the car, describe the problem, and see the parts that fit. Logging in is optional. It does not sell and it does not hold stock.

A signed-in person is the client. Login is email plus password. Email alone must not open an account. After login, the garage, the saved searches, and the name in the header are only that person's. Do not put a dropdown of all clients on the search. Do not list other people's cars or searches.

`/clientes` is that person's garage, not an admin registration desk. Signed out, that page is the way in to the account, not a list of clients.

The free-text problem narrows the parts through the AI. Fitment against the own catalog stays the hard filter; the AI only picks among fitting parts and never adds one. Signed in, the text and the diagnosis are stored. Signed out, nothing is stored.

## Quality bar

A session is done when the home page is the search and the result is part cards: name, system, description, which cars it fits, equivalent codes. Not a comma-separated list of canonical codes, and not the enum `Diagnosticada`.

UI copy is Spanish. Course files in `docs/` stay Spanish. This file and `status.md` stay English.

Do not fake a shop comparison. Prices and stores come only from captured offers (`ServicioOfertas`, VTEX). Do not render empty price columns: a part without offers says so.

## Constraints

Grading scope already covered: TP Inicial and Primera Parte (`TP (1).pdf`). Do not re-implement them.

Segunda Parte is now in scope, one slice at a time, in the order of Next. Do not mix slices in one session, and do not build a slice inside a UI pass.

`Pedido`, `Inventario`, and `Clientes` in the PDF are examples. Do not add an inventory or stock service.

Do not put architecture, layers, or "local vs remote" in the UI. `/acceso` stays deleted. Evidence stays at `docs/evidencias/` and `GET /evidencia/catalogo`.

## Locked decisions

- Product UI: Spanish. Tracker files: English. `docs/`: Spanish.
- Home search works with no session. Login requires a password. The signed-in person is the client. Part cards, not an admin table of codes. No client picker.
- Services are `ServicioClientes` and `ServicioBusquedas`.
- `FachadaAplicacion` is the only type Razor pages call.
- `BusquedaCreada` is dispatched in-process to `ObservadorCompatibilidad`, which fills codes through `ServicioDiagnostico` (fitment, then AI). Keep that class name: the docs explain the AI as a complement to the Primera Parte observer.
- AI: `IDiagnosticador` has a local path (`RepMatch.IA`, LLM with local fallback) and a remote one (`IA.Api`), chosen by `IA:Modo`. `IA.Api` has no database; candidates travel in the request. The AI source is not shown in the UI.
- Sequence diagram: `docs/diagramas/secuencia.md`.
- Segunda Parte assignment minimums: 1 SOAP (WSDL), 2+ REST (OpenAPI), a queue with producers and consumers, 1+ real external API, an AI component reachable by REST or by the queue.
- Queue: `RabbitMQ.Client` direct behind an own `IEventBus`, no MassTransit (`docs/informe-arquitectura.md` 6.5).
- Shop sources: official public APIs only (VTEX public catalog, eBay Browse). Never scraping. Never render an offer that was not captured.
- The UI result of a search stays synchronous. Moving dispatch to the queue must not make `/` wait for, or poll, a worker to fill `CodigosObjetivo` (see slice 2).
- No API key or token in the repo. Every external dependency (LLM, eBay) has a no-credentials path so `docker compose up` and `dotnet test` work offline.

## Repo

```
src/ tests/     application
docs/           course deliverables (Spanish)
plan.md         intent (this file)
status.md       snapshot of what is true now
```

## Next: Segunda Parte

Slice 1 (AI, #15 and #16) is done; facts in `status.md`. The Docker run was checked on 2026-11-05. Close #15 and #16 once the work is committed.

Before slice 2: commit the roles and AI work that is still uncommitted in the tree, and close the `tp-primera` issues that are done (#1-#9, #35 if its evidence exists). Issues #10-#21 hold the details per slice; where an issue contradicts this file, this file wins and the issue gets a comment.

### Slice 2. Messaging (#12, #13, #14).

- `rabbitmq:4-management` in compose, `IEventBus` in Contracts, `RepMatch.Mensajeria` on `RabbitMQ.Client`. Topic exchange `repmatch.eventos`, durable queue per consumer, DLQ, correlation id in headers, versioned JSON contract.
- Dispatch becomes composite: in-process observers first (the UI needs the codes now), then publish to the bus after commit. Broker down → search still works, failure logged. `ServicioBusquedas` does not change.
- Worker `src/RepMatch.Notificaciones.Worker` consumes `busqueda.compartida` (notify the mechanic: a stored notification shown on `/taller`), `derivacion.tienda` (commission ledger per shop) and `busqueda.creada` (log/metrics). Idempotent by message id, ack after success.
- Drop `AlertaPrecio` from #14: it does not exist in the domain and needs offers first.
- AI over the queue is optional: only if time remains, a consumer that re-diagnoses with the LLM when the synchronous call fell back to local.
- Done 2026-11-11 in `feature/mensajeria-rabbitmq`: broker, `IEventBus`, `PublicadorEventos` (all four events), worker with `derivacion.tienda` (commission ledger) and `busqueda.creada` and the rest (activity log). The `busqueda.compartida` notification for the mechanic is not built yet (new consumer on the same base). Verified only without a broker; needs a run with Docker.

### Slice 3. SOAP (#10, #11).

- `src/RepMatch.Catalogo.Soap` with CoreWCF: `IConsultaCompatibilidad` (`ConsultarPorVehiculo`, `VerificarCodigo`), WSDL, compose service on 8082, healthy.
- `CatalogoSoap` in `RepMatch.Clientes.Soap` as third `ICatalogoRepuestos`; `Catalogo:Modo=Soap`. The presentation does not change.
- #11 still mentions `/acceso`; it is deleted. Compare the three modes in `scripts/evidencias.sh` and in the interchangeability test instead.

### Slice 4. Real offers (#4, #18, #17, #19, #20).

- `ServicioOfertas` (#4): register, dedupe, rank by total price, never mix currencies, drop expired.
- `IFuenteOfertas` + `AdapterVtex` first (no credentials, ARS, easy + fravega by config). Then `AdapterEbay` (USD, OAuth client credentials, self-disables without keys). Fixtures in `tests/fixtures/`, no network in tests.
- Aggregation in parallel with partial results. Prefer endpoints on an existing host over a new `Search.Api` unless a third REST service is needed for the grade.
- Polly per source (timeout, retry, circuit breaker). Use `IDistributedCache` with Redis in Compose and an in-memory provider otherwise; cache failures must not stop offer lookup.
- Offers render on the result only when captured; `/derivar/{ofertaId}` then works for real. Ranking by price only.
- #29/#30 (EAN match and validating name matches) and #20 (per-source resilience) are implemented in the current offers worktree: shared EANs classify as exact matches, contradictory name matches are discarded, and Polly failures are isolated at the source boundary. eBay (#17) remains pending; #19 only if a third REST service is needed.
- `ServicioOfertas` (#4) and the VTEX adapter (#18) are implemented in `feature/ofertas-vtex`: prices load after the parts, source responses are cached through `IDistributedCache`, and only captured offers are shown.

### Slice 5. Docs and evidence (#21).

- `scripts/evidencias.sh`: WSDL, OpenAPI of each REST host, AI response from LLM and from fallback, one message produced and consumed with the same correlation id, an aggregated offer search.
- `docs/informe-segunda-parte.md` (contracts, queue topology, AI decisions), deployment diagram from *prevista* to real, new message-flow diagram, sequence for the AI search, `docs/patrones.md` for new patterns (Strategy for `IDiagnosticador`, Adapter for SOAP/VTEX/eBay, circuit breaker), section 7 table of the report, README, test count in report 5.4 and `status.md`.
- Each slice updates its own docs when it lands; slice 5 only closes gaps.

## Later, not now

Integrador (#22-#28): requirements, AI accuracy on the mechanic-validated dataset (`DiagnosticoValidado`), load tests, deployment manual, demo script.
