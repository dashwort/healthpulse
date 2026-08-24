# HealthPulse repository instructions

This file is the working guide for contributors and coding agents modifying the HealthPulse repository.

## Project overview

HealthPulse is a .NET 10 Blazor Web App with a controller-based REST API and Android companion app for recording personal health readings. It supports built-in and user-created measurement templates, normalized units for built-in measurements, local date/time entry with UTC persistence, backdated readings, offline Android queueing and synchronisation, soft deletion, and trend charts.

The application is strictly user-scoped, with a small administrator role for allow-list, token, and diagnostics management. A request must never accept a user ID from the UI or trust a user ID supplied by a client; the authenticated subject is resolved by the web adapter and passed through the application current-user port.

## Repository structure

```text
src/
  HealthTracker.Domain/          Dependency-free domain POCOs and built-in catalogue
  HealthTracker.Application/    Use cases, ports, DTOs, mappings, and unit conversion
  HealthTracker.Infrastructure/ EF Core adapter, SQLite mappings, migrations, and seeding
  HealthTracker.Web/             Blazor UI, API controllers, OIDC, hosting, and purge worker
android/                          Android companion app, offline cache/queue, mobile authentication, and APK build
tests/
  HealthTracker.Application.Tests/ Application service and unit-conversion tests
```

The internal project and namespace names retain `HealthTracker`; `HealthPulse` is the public product name and repository branding.

## Architecture and dependency rules

- Domain contains simple POCO models and must not depend on EF Core, ASP.NET Core, MudBlazor, or browser APIs.
- Application owns use cases and ports. `HealthTrackerService` is the application façade used by both controllers and the Blazor UI.
- Application accesses persistence through `IHealthDataStore` and identity through `ICurrentUser`.
- DTOs are application/API contracts. Keep conversion between domain and DTO layers in extension methods under `Mappings`; do not leak persistence models into controllers or components.
- Infrastructure implements application ports with EF Core. SQLite is the current adapter, but provider-specific code belongs in Infrastructure's composition root and persistence mappings.
- Web is the composition root. Controllers should remain thin and delegate authorization, validation, ownership checks, normalization, and persistence orchestration to the application service.

## Data and security invariants

- Every read, create, update, track/untrack, and delete operation is scoped to the authenticated user.
- Built-in templates are shared catalogue definitions; tracking selections are per user.
- Custom templates are owned by their creating user and can only be managed by that owner.
- Untracking a template does not delete its historic readings. Archived history is exposed through the archive filter.
- Deletes are soft deletes. The hosted purge worker permanently removes records after 60 days.
- Store timestamps as UTC (`DateTimeOffset`); convert to browser-local time only at the UI boundary.
- Built-in readings are stored in the template's normalized unit. Custom-template readings retain the custom template's unit and are not converted.
- Reading notes are plain text and limited to 140 characters. Preserve server-side validation even when UI validation exists.
- Never commit client secrets, development settings, SQLite databases, data-protection keys, or other generated/local state. Use environment variables, secret stores, or an ignored development settings file.

## Web UI conventions

- The UI uses Blazor Interactive Server rendering, MudBlazor components, and Blazor-ApexCharts.
- Main pages are Dashboard (`Home.razor`), Readings, and Templates. Keep navigation reachable and responsive at narrow widths.
- The web theme follows the system preference by default, can be toggled, and is persisted in browser local storage. The Android app uses its dark theme. MudBlazor and chart palettes should remain visually consistent when changing the web theme.
- Reading entry/edit dialogs must validate numeric values, units, timestamps, and note length before saving. Web and Android entry surfaces collect local date/time; the server and API contract persist `DateTimeOffset` values in UTC. Historical dates are allowed, but timestamps more than five minutes in the future are rejected.
- Charts show one metric at a time in its stored normalized unit and need an explicit empty-data state.
- Destructive actions require confirmation and should report success/failure through the existing snackbar/dialog patterns rather than uncaught exceptions.

## Android conventions

- The Android app is a companion client for the server API; it must not create a second source of truth for health data.
- New readings are written to the local cache immediately and queued for `POST /api/readings` when offline. Preserve the queue and visible sync status when changing the client.
- Android authentication uses the server's PKCE mobile hand-off and bearer sessions. Never log access tokens, refresh tokens, or health-data payloads.
- Use the device local time zone for entry and display, then send `recordedAtUtc` as an ISO 8601 UTC timestamp.
- The release workflow is `.github/workflows/android-release.yml`; release tags use `android-v<major>.<minor>.<patch>` and produce a signed `HealthPulse-<version>.apk` GitHub Release asset.

## Authentication and configuration

- Production authentication uses generic OpenID Connect configured through the web application's configuration binding; the documented production provider is Google.
- HealthPulse is invitation-only: `AccessControl__InitialAdministratorEmail` seeds the first admin when the allow-list is empty. Only active, verified emails from the configured OIDC provider may access the app; production is currently configured for Google. Admins manage the allow-list and the final active admin cannot be removed or demoted.
- Personal access tokens are user-scoped credentials for the HTTPS `/mcp` Streamable HTTP endpoint. Store only token hashes, never log tokens or health-data arguments, and preserve immediate revocation when users or tokens are archived/revoked.
- Tokens expire after one year and each user may have at most five active tokens. Administrators can revoke any user's token but must never be able to recover or view its secret.
- MCP requests inherit the token owner's current role. Members are limited to their own health data; administrator tokens may manage users and tokens. Enforce both limits on every request: 60 calls per minute in process and 1,000 calls per token/day from persisted audit history; return HTTP 429 when a limit is exceeded.
- MCP audit records retain only token/user IDs, method name, timestamp, and outcome for one year. Never store request bodies, arguments, token values, or health data. Keep JSON imports additive, fully validate them before persistence, restore needed template tracking, and persist them transactionally.
- Development may use the local development authentication handler when no OIDC authority is configured; this fallback must not be enabled outside Development.
- Do not add provider-specific secrets to tracked JSON files. Document required configuration keys and use environment variables or deployment secret storage.

## Database and migrations

- Migrations live under `src/HealthTracker.Infrastructure/Persistence/Migrations`.
- Startup applies pending migrations automatically and seeds missing built-in templates.
- When changing the persistence model, update the domain/application mappings, add an EF migration, and verify the model snapshot. Do not edit an old migration that may already have been deployed.
- SQLite does not translate all `DateTimeOffset` comparisons. For queries that must compare persisted UTC timestamps with the current time, load the narrowly scoped records first and apply the comparison in memory, as done by the soft-deletion and active-token paths.
- Generate a migration with:

  ```powershell
  dotnet ef migrations add <Name> --project src/HealthTracker.Infrastructure --startup-project src/HealthTracker.Web --context HealthTrackerDbContext --output-dir Persistence/Migrations
  ```

## Validation workflow

Run from the repository root:

```powershell
dotnet restore HealthTracker.slnx
dotnet test HealthTracker.slnx --no-restore
dotnet build HealthTracker.slnx --no-restore
```

For Android changes, set `JAVA_HOME` to a JDK 21 installation (Android Studio's bundled JBR is suitable on Windows), then run:

```powershell
Push-Location android
./gradlew.bat test
./gradlew.bat assembleDebug
Pop-Location
```

The debug APK is written under the Gradle build directory configured in `android/app/build.gradle.kts`. For a production-style container check, run `docker build --tag healthpulse:local .`.

The automated .NET suite is in `tests/HealthTracker.Application.Tests`; Android unit tests are in `android/app/src/test`. Add tests for application behavior, validation boundaries, normalization, paging/filtering, ownership isolation, offline queueing, and timestamp conversion when changing those areas.

For UI changes, run the web project and verify navigation, responsive layout, modal validation, local date/time entry including backdated readings, CRUD behavior, archive visibility, pagination, chart rendering, Android sync status, and APK update checks in the browser/device flow. Avoid leaving multiple app instances running because the executable and referenced assemblies can be locked during a build.

## Change and commit guidance

- Keep changes focused and preserve unrelated user work in a dirty tree.
- Prefer `apply_patch` for edits and do not commit generated output.
- Update tests and README/AGENTS.md when behavior or developer workflow changes.
- Before committing, inspect `git diff`, run the relevant tests, check for secrets, and confirm `git status` contains only intended changes.
- Use clear imperative commit messages and push only after the working tree is clean and validation has completed.
