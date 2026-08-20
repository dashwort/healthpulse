# HealthPulse repository instructions

This file is the working guide for contributors and coding agents modifying the HealthPulse repository.

## Project overview

HealthPulse is a .NET 10 Blazor Web App with a controller-based REST API for recording personal health readings. It supports built-in and user-created measurement templates, normalized units for built-in measurements, local-time entry/display, UTC persistence, soft deletion, and trend charts.

The application is strictly user-scoped. There are no administrator roles. A request must never accept a user ID from the UI or trust a user ID supplied by a client; the authenticated subject is resolved by the web adapter and passed through the application current-user port.

## Repository structure

```text
src/
  HealthTracker.Domain/          Dependency-free domain POCOs and built-in catalogue
  HealthTracker.Application/    Use cases, ports, DTOs, mappings, and unit conversion
  HealthTracker.Infrastructure/ EF Core adapter, SQLite mappings, migrations, and seeding
  HealthTracker.Web/             Blazor UI, API controllers, OIDC, hosting, and purge worker
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
- Theme preference follows the system by default and is persisted in browser local storage. MudBlazor and chart palettes should remain visually consistent when changing theme.
- Reading entry/edit dialogs must validate numeric values, units, timestamps, and note length before saving. User-facing timestamps are local time.
- Charts show one metric at a time in its stored normalized unit and need an explicit empty-data state.
- Destructive actions require confirmation and should report success/failure through the existing snackbar/dialog patterns rather than uncaught exceptions.

## Authentication and configuration

- Production authentication uses generic OpenID Connect configured through the web application's configuration binding.
- Development may use the local development authentication handler when no OIDC authority is configured; this fallback must not be enabled outside Development.
- Do not add provider-specific secrets to tracked JSON files. Document required configuration keys and use environment variables or deployment secret storage.

## Database and migrations

- Migrations live under `src/HealthTracker.Infrastructure/Persistence/Migrations`.
- Startup applies pending migrations automatically and seeds missing built-in templates.
- When changing the persistence model, update the domain/application mappings, add an EF migration, and verify the model snapshot. Do not edit an old migration that may already have been deployed.
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

The current automated suite is in `tests/HealthTracker.Application.Tests`. Add tests for application behavior, validation boundaries, normalization, paging/filtering, and ownership isolation when changing those areas.

For UI changes, run the web project and verify navigation, responsive layout, modal validation, theme persistence, CRUD behavior, archive visibility, pagination, and chart rendering in the browser. Avoid leaving multiple app instances running because the executable and referenced assemblies can be locked during a build.

## Change and commit guidance

- Keep changes focused and preserve unrelated user work in a dirty tree.
- Prefer `apply_patch` for edits and do not commit generated output.
- Update tests and README/AGENTS.md when behavior or developer workflow changes.
- Before committing, inspect `git diff`, run the relevant tests, check for secrets, and confirm `git status` contains only intended changes.
- Use clear imperative commit messages and push only after the working tree is clean and validation has completed.
