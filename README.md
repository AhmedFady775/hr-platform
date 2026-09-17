# HR Leave Requests

A small internal module for managing employee leave requests. It owns one table
(`LeaveRequests`) in SQL Server and enriches every response with live employee data
pulled from a third-party HR system ([dummyjson.com/users](https://dummyjson.com/users)) — the
employees themselves are never stored locally.

## Stack

- **API**: ASP.NET Core Web API (.NET 8), EF Core (SQL Server provider)
- **UI**: Blazor Web App (.NET 8, Interactive Server render mode)
- **DB**: SQL Server (via Docker Compose, or any reachable SQL Server instance)
- **Cache**: Redis (`IDistributedCache`), shared across API instances
- **Contracts**: a small class library (`HrPlatform.Contracts`) with the
  DTOs/enums that are the wire contract between the API and the UI

Projects are named `HrPlatform.Api` / `.Web` / `.Contracts` / `.Tests` — flat, not
`HrPlatform.LeaveRequests.*`. Leave requests is the first module this platform has,
not the only one it'll ever have; baking that module's name into every project would
stop making sense the moment a second module (payroll, onboarding, whatever) shows up.
Domain-specific names still show up exactly where they're accurate — `LeaveRequest`
the entity, `LeaveRequestsController`, `LeaveRequestsDbContext` — just not smeared
across the project/namespace scaffolding itself. Tests live in a top-level `tests/`
folder, separate from `src/`, so `src/` only ever contains what actually ships.

```
src/
  HrPlatform.Api/          ASP.NET Core Web API (+ Dockerfile)
  HrPlatform.Web/          Blazor Web App (UI) (+ Dockerfile)
  HrPlatform.Contracts/    Shared DTOs/enums (the API/UI wire contract)
tests/
  HrPlatform.Tests/        xUnit tests (status-transition logic)
database/
  01_create_schema.sql                   Idempotent schema script (generated from EF migrations)
  02_seed_data.sql                       Seed script (~10 leave requests, employee ids 1-10)
docker-compose.yml                       SQL Server + API + UI, fully containerized
LeaveRequests.Api.http                   Sample requests for every endpoint
```

## Running locally

### Option A: everything in Docker (fastest)

```bash
cp .env.example .env
docker compose up -d --build
```

`.env` holds the SA password, JWT signing key, ports, and the other bits that vary
per environment (see `.env.example` for the full list) — it's gitignored, so edit it
locally and never commit it. Docker Compose picks it up automatically.

This builds and runs all five services — SQL Server, Redis, a one-shot `migrator`,
the API, and the UI — on one Docker network (`docker-compose.yml`):

- UI: http://localhost:8091 (sign in at http://localhost:8091/login)
- API: http://localhost:8090 (Swagger at http://localhost:8090/swagger)
- SQL Server: `localhost,1433` (`sa` / whatever you set `MSSQL_SA_PASSWORD` to in `.env`)

Demo HR login: `hr@company.com` / `Password123!` — the only account this system lets in.
Demo non-HR login: `employee@company.com` / `Password123!` — valid credentials, but the
UI rejects it at `/login` with an explanation, and the API 403s it on every business
endpoint; useful for seeing that path (`.http` file has a raw example against the API).

`migrator` waits for SQL Server's healthcheck, applies EF Core migrations, seeds ~10
leave requests, then exits; `api` waits for `migrator` to finish successfully before it
starts — no manual steps, and no risk of concurrent `api` replicas racing to apply the
same migration (see `Program.cs`'s `--migrate-only` mode). The `web` service talks to
the API over the Docker network (`http://api:8080`), not through the host port. Tear
down with `docker compose down` (add `-v` to also drop the DB volume and reset the seed
data).

### Option B: run each piece with `dotnet run` (for iterating on code)

**1. Start SQL Server and Redis** (or point at instances you already have):

```bash
docker compose up -d sqlserver redis
```

**2. Apply migrations and seed data** (once, not on every `dotnet run`):

```bash
cd src/HrPlatform.Api
dotnet run -- --migrate-only
```

This applies EF Core migrations and seeds ~10 leave requests
(`Data/LeaveRequests/DbInitializer.cs`), then exits — the API itself no longer does
this on startup (see "on startup" note below). If you'd rather run the SQL directly,
`database/01_create_schema.sql` and `database/02_seed_data.sql` are equivalent and
idempotent.

**3. Run the API:**

```bash
dotnet run --urls "https://localhost:7212;http://localhost:5121"
```

Connection strings and the third-party API base URL live in
`src/HrPlatform.Api/appsettings.json` (`ConnectionStrings:LeaveRequestsDb`,
`ConnectionStrings:Redis`, `EmployeeApi:BaseUrl`). Swagger is available at
`https://localhost:7212/swagger` in Development.

**4. Run the UI:**

```bash
cd src/HrPlatform.Web
dotnet run --urls "https://localhost:7112;http://localhost:5122"
```

Open `https://localhost:7112`. The UI's API base URL is in
`src/HrPlatform.Web/appsettings.json` (`LeaveRequestsApi:BaseUrl`) and the
API's CORS policy (`Cors:AllowedOrigins` in the API's `appsettings.json`) must include
the UI's origin — both already point at each other with the ports above.

### Tests

```bash
dotnet test tests/HrPlatform.Tests/HrPlatform.Tests.csproj
```

## Assumptions & design notes

- **Why no DB-level FK on `EmployeeId`**: the employee data lives in a system this
  module doesn't own (dummyjson.com), so there's no local `Employees` table to
  reference. Existence is instead validated in `LeaveRequestService.CreateAsync` by
  calling the third-party API before insert, returning 404 if the id doesn't exist.
  This is a deliberate trade-off: it moves referential integrity from the database
  to application code and a network call, which is slower and racier than a real FK,
  but it's the only option when the source of truth is external.
- **Caching**: `EmployeeClient` caches the full employee list (`GetAllAsLookupAsync`)
  in Redis (`IDistributedCache`) for 2 minutes, so listing/paginating leave requests
  does one batch fetch instead of one call per row, and the cache is shared across
  every API instance rather than going cold per-replica. Single-employee lookups also
  check that cache before hitting the network. Tests swap in an in-process
  `AddDistributedMemoryCache()` (same `IDistributedCache` interface) so the suite never
  touches real Redis.
- **Third-party failures**: `EmployeeClient` uses a typed `HttpClient` (via
  `IHttpClientFactory`, registered in `Program.cs`) with a 5s timeout. Timeouts and
  connection failures are translated into `EmployeeServiceUnavailableException`,
  which the global exception middleware turns into a `502` with a clean
  `{ "error": "..." }` body instead of a raw exception/stack trace or a hung request.
- **Error shape**: every non-2xx response (validation, not-found, conflict, upstream
  failure, database outage, unhandled exception) goes through
  `Middleware/ExceptionHandlingMiddleware.cs` and comes back as `{ "error": "<message>" }`
  with the right status code, so the UI (and any other consumer) only needs one
  error-handling path. Domain exceptions carry their own HTTP mapping via
  `Middleware/IApiException.cs` instead of the middleware switching on concrete
  exception types, so a new module's exceptions don't require editing shared
  middleware. A `SqlException` mid-request (the database drops out after the app has
  already started) maps to `503` with a specific message instead of falling into the
  generic 500 case.
- **Migrations run once, not per API replica**: `Program.cs` supports a
  `--migrate-only` mode (applies EF Core migrations + seeds, retrying up to 5 times
  with backoff, then exits) instead of doing this on every normal startup. In Docker
  Compose this runs as a separate one-shot `migrator` service that `api` depends on
  (`service_completed_successfully`); running the API directly, do
  `dotnet run -- --migrate-only` once before `dotnet run`. This avoids multiple `api`
  replicas racing to apply the same migration concurrently.
- **Frontend failure states**: the leave-requests table and the employee picker each
  distinguish a genuinely empty result from a failed load — a `_loadFailed` /
  `_employeesLoadFailed` flag drives a distinct "Couldn't load..." panel with a Retry
  button, rather than showing the same "No leave requests found" either way. A crashed
  Blazor Server circuit shows a styled reconnect banner (`#blazor-error-ui` in
  `app.css`) instead of the framework's unstyled default.
- **Status transitions**: only `Pending -> Approved` and `Pending -> Rejected` are
  legal; anything else returns `409`. Delete is only allowed while `Pending`
  (`400` otherwise). The transition rule is a pure static method
  (`LeaveRequestService.IsValidTransition`) specifically so it's unit-testable without
  spinning up a database — see `HrPlatform.Tests`.
- **Raw SQL**: none in application code — all local data access goes through EF Core
  with LINQ (parameterized automatically). The only raw SQL in the repo is the
  generated/idempotent migration script in `database/01_create_schema.sql`, which is a
  deployment artifact, not a query path.
- **Auth**: this is an HR-only internal system, so every API endpoint requires a
  valid JWT for the `HR` role, not just approve/reject/delete. `Program.cs` sets a
  global `AuthorizationOptions.FallbackPolicy` (`RequireAuthenticatedUser()` +
  `RequireRole("HR")`) that applies to every endpoint by default; `POST
  /api/auth/login` is the one `[AllowAnonymous]` exception, since it's how you get a
  token in the first place. Tokens come from that login endpoint against two
  hardcoded demo accounts in `Api/Services/Auth/DemoUsers.cs` (`hr@company.com` /
  `Password123!` role `HR`, `employee@company.com` / `Password123!` role `Employee`,
  kept around specifically to demonstrate the `403` path) — there's no real user
  store in scope, and this is documented inline as a stand-in. A missing/invalid
  token returns `401`, a valid token with the wrong role returns `403`, both in the
  same `{ "error": "..." }` shape as everything else (see `JwtBearerEvents.OnChallenge`
  and `ApiAuthorizationMiddlewareResultHandler` in `Program.cs`).

  On the UI side, sign-in uses a real auth cookie, not in-memory state. The first
  version of this held the token in a `Scoped` service, which — being tied to one
  Blazor Server SignalR circuit — was wiped by any full page reload and caused a
  signed-in user to bounce right back to `/login`. Cookie auth
  (`AddAuthentication().AddCookie()` + `AddCascadingAuthenticationState()` in
  `HrPlatform.Web/Program.cs`) survives reloads: `/login` is a plain
  (non-interactive) HTML form that posts to `POST /login-submit`, a minimal-API
  endpoint that calls the API's login endpoint and, on success, signs the browser into
  a cookie carrying the username, role, and the API's JWT as a claim.
  `LeaveRequestsApiClient` reads that JWT via `AuthenticationStateProvider` on every
  call. `Home.razor` is `[Authorize(Roles = "HR")]`; `Routes.razor` redirects
  `NotAuthorized` to `/login`.
- **Blazor render mode**: the UI uses Interactive Server rendering so the whole app is
  one process talking server-to-server to the API — no CORS-sensitive WASM fetches to
  worry about (CORS is still configured on the API for completeness/flexibility).
- **Validation error shape**: `[ApiController]`'s automatic 400 for bad model binding
  (missing/invalid fields caught by data annotations before a controller action even
  runs) uses ASP.NET's default `ProblemDetails` format out of the box. `Program.cs`
  overrides `ApiBehaviorOptions.InvalidModelStateResponseFactory` so those responses
  also come back as `{ "error": "..." }`, matching every other error path.

## What I'd do next with more time

- Replace the hardcoded demo accounts with a real user store (or an actual identity
  provider) and a proper password hash instead of plaintext comparison.
- Add retry/circuit-breaker (e.g. Polly) around the third-party employee-service
  calls instead of a bare timeout.
- Add integration tests against the API (WebApplicationFactory + a test SQL Server
  container) covering the controller/validation layer, not just the pure transition
  logic.
- Server-side search/typeahead for the employee picker instead of loading a flat list
  of 20.
- Add a healthcheck endpoint on the API and wire `web`'s `depends_on` in
  `docker-compose.yml` to it (currently `web` just waits for `api` to start, not to be
  ready).
