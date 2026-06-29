# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A hobby site to track board game wins, stats, and collections. Built with **C# Blazor Web App (.NET 10)** + **MudBlazor**, backed by PostgreSQL via **Entity Framework Core + Npgsql**.

## Deployment

Hosted on **Azure Container Apps**, database on **Supabase** (hosted Postgres).

Infrastructure is defined in `iac/main.bicep` (Bicep/ARM), which provisions:
- Azure Container Registry (ACR) — name is `boardgamefanatics` + a stable unique suffix derived from the resource group
- Log Analytics workspace — for Container App logs
- Container Apps Environment
- Container App — 0.25 vCPU / 0.5 GiB, scale-to-zero (min 0, max 1 replica), liveness + readiness probes on `/healthz`

CI/CD via `.github/workflows/deploy.yml` — on every push to `main`:
1. Deploys `iac/main.bicep` (idempotent) to provision/update infrastructure
2. Applies EF Core migrations against Supabase (`dotnet ef database update`)
3. Builds and pushes the Docker image to ACR
4. Updates the Container App to the new image and sets `SUPABASE_URL` / `SUPABASE_ANON_KEY` runtime env vars

ACR name, login server, and Container App name are read from Bicep deployment outputs — no need to hardcode them as secrets.

**Required GitHub secrets:**
| Secret | Description |
|---|---|
| `AZURE_CLIENT_ID` | Service principal app/client ID |
| `AZURE_CLIENT_SECRET` | Service principal client secret |
| `AZURE_TENANT_ID` | Azure AD tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID |
| `AZURE_RESOURCE_GROUP` | Resource group to deploy into |
| `SUPABASE_CONNECTION_STRING` | Session pooler connection string (port 5432): `postgresql://postgres.<project-ref>:<password>@aws-0-<region>.pooler.supabase.com:5432/postgres` |
| `NEXT_PUBLIC_SUPABASE_URL` | Supabase project URL — reused as `SUPABASE_URL` runtime env var |
| `NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY` | Supabase anon key — reused as `SUPABASE_ANON_KEY` runtime env var |
| `ALERT_EMAIL` | Email to notify when budget thresholds are hit |

`DATABASE_URL` must be the `postgresql://` URI format. Npgsql accepts this format directly.

`SUPABASE_URL` and `SUPABASE_ANON_KEY` are **runtime** env vars (not build-time), injected by the `az containerapp update` step in `deploy.yml`.

Use the **Session pooler** connection string (Supabase dashboard: **Project Settings → Database → Connection string → Session**) — the direct host is IPv6-only and GitHub Actions runners can't reach it.

The app exposes `/healthz` (minimal API endpoint in `Program.cs`) for Container Apps liveness/readiness probes.

## Commands

All commands run from `src/BlazorApp/BoardGameFanatics/`:

```bash
# Install/restore dependencies
dotnet restore

# Run the app in dev mode (requires Postgres running locally on port 5432)
dotnet run

# Build for production
dotnet build -c Release

# Apply EF Core migrations to the database
dotnet ef database update

# Create a new migration from model changes
dotnet ef migrations add <MigrationName>

# Run only the database for local development
docker compose -f docker-compose.dev.yml up -d
```

There are no automated tests yet.

## Architecture

Single Blazor Web App (.NET 10) using the `Components/Pages/` convention:

- **`Components/App.razor`** — Root HTML document; imports MudBlazor CSS/JS.
- **`Components/Layout/MainLayout.razor`** — MudBlazor layout with drawer, app bar, theme toggle. Initializes `MudThemeProvider`, `MudSnackbarProvider`, `MudDialogProvider`.
- **`Components/Layout/NavMenu.razor`** — `MudNavMenu` with `AuthorizeView` to show/hide protected links.
- **`Components/Pages/Home.razor`** — Welcome page (`@rendermode InteractiveServer`).
- **`Components/Pages/Login.razor`** — SSR login form using Blazor's `[SupplyParameterFromForm]` pattern; calls `AuthService.SignInAsync()` which uses `IHttpContextAccessor` to set the auth cookie.
- **`Components/Pages/Signup.razor`** — SSR signup form; calls `AuthService.SignUpAsync()` which passes `display_name` metadata to Supabase (the DB trigger reads it to create the `Player` row).
- **`Components/Pages/Players.razor`** — Lists approved players (`@rendermode InteractiveServer`).
- **`Components/Pages/PlayerCollection.razor`** — Player's game bookshelf; owners can search BGG and add/remove games (`@rendermode InteractiveServer`).
- **`Components/Pages/Admin/Players.razor`** — Admin-only pending-player approval queue (`@rendermode InteractiveServer`, `[Authorize(Roles = "Admin")]`).
- **`Data/AppDbContext.cs`** — EF Core `DbContext` with Npgsql. Maps PostgreSQL native enums `PlayerStatus`/`PlayerRole` via `UpperCaseNameTranslator` (C# `Pending` ↔ PG `PENDING`).
- **`Data/UpperCaseNameTranslator.cs`** — `INpgsqlNameTranslator` that converts PascalCase enum member names to UPPERCASE for PostgreSQL native enum mapping.
- **`Services/AuthService.cs`** — Wraps `supabase-csharp` for credential validation; issues ASP.NET Core cookie sessions with claims `{sub, displayName, status, role}`. `GetCurrentPlayer(ClaimsPrincipal)` reads claims without a DB round-trip.
- **`Services/BggService.cs`** — BoardGameGeek XML API v2 client using `HttpClient` + `System.Xml.Linq`. `FindOrCacheGameAsync(bggId)` checks the local DB first.
- **`Services/CollectionService.cs`** — `AddGameAsync` / `RemoveGameAsync` with ownership enforcement.
- **`Migrations/`** — EF Core migrations. `InitialCreate` is a **baseline** (empty Up/Down) because the schema already exists in Supabase; it just stamps `__EFMigrationsHistory`. Future schema changes create real migrations on top.
- **`Program.cs`** — Registers EF Core (Npgsql with enum mapping), cookie auth, Supabase client (singleton, `AutoRefreshToken = false`), `AuthService`, `CollectionService`, `BggService` (typed `HttpClient`), `AddMudServices()`. Minimal API endpoints: `GET /healthz`, `GET /account/logout`, `GET /auth/confirm`.

## Authentication & Players

Supabase Auth handles signup/login. The `Player` table extends Supabase's `auth.users`:

- `id` is the same UUID as `auth.users.id` — populated by a database trigger on signup.
- `status`: `PENDING` (default) or `APPROVED`.
- `role`: `PLAYER` (default) or `ADMIN`.

Auth flow:
1. **Signup**: `AuthService.SignUpAsync()` calls Supabase with `display_name` in user metadata. The DB trigger `handle_new_player()` auto-inserts a `PENDING` `Player` row.
2. **Login**: `AuthService.SignInAsync()` calls Supabase, loads the `Player` record, then calls `HttpContext.SignInAsync()` to create a cookie with claims. Login/Signup pages are **SSR** (no `@rendermode`), which makes `IHttpContextAccessor.HttpContext` available in the form handler.
3. **Logout**: `GET /account/logout` minimal API endpoint clears the ASP.NET Core cookie.
4. **Email confirm**: `GET /auth/confirm?token_hash=...&type=...` minimal API endpoint calls Supabase's `/auth/v1/verify` REST endpoint, then redirects to `/login`.

Authorization in pages uses `[Authorize]` attribute and `<AuthorizeView>` components. Role check uses `ClaimTypes.Role` claim set to `Player` or `Admin` (PascalCase — matches the C# enum values in the cookie).

## Database

- **Local dev**: Start Postgres via `docker compose -f docker-compose.dev.yml up -d`, then `dotnet run` from `src/BlazorApp/BoardGameFanatics/`. Set `DATABASE_URL` in `src/BlazorApp/BoardGameFanatics/appsettings.Development.json` or as an environment variable.
- **Production**: Supabase (hosted Postgres). `DATABASE_URL` injected as a Container App secret.
- Migrations do **not** run automatically at app startup — applied via `dotnet ef database update` in CI/CD.

## BGG API

`BggService` calls `GET /xmlapi2/search` and `GET /xmlapi2/thing` on `boardgamegeek.com`. The API requires `Authorization: Bearer <token>` — set `BGG_API_TOKEN` in the local environment or Container App env vars. `FindOrCacheGameAsync` checks `AppDbContext.Games` first; on miss, fetches from BGG and inserts.

## Key Conventions

- C# (no TypeScript). Blazor `.razor` files for all UI.
- ORM: EF Core with Npgsql. Models in `Data/`. DbContext: `AppDbContext`.
- New entities: add to `Data/`, update `AppDbContext.OnModelCreating`, run `dotnet ef migrations add <Name>`.
- New pages: add `.razor` files in `Components/Pages/` following Blazor routing (`@page "/path"`). Use `@rendermode InteractiveServer` for pages that need reactive UI. Omit `@rendermode` for SSR-only pages (e.g. forms that set cookies).
- UI components: MudBlazor. Theme colors: Primary `#C1622D`/`#E0954C`, Secondary `#2F5D50`/`#5FA98C` (light/dark). Theme defined in `MainLayout.razor`.

## Roadmap

**Done:** Player auth + admin approval workflow, game catalog (BGG integration), game collections (bookshelf).

**Next, in order:**

### Phase 4: Plays

`Play` (gameId, playedAt, notes) + a join table (`PlayParticipant`: playId, playerId, `won`/`score` fields). **Per-game win conditions and metrics are explicitly undesigned/deferred** — revisit the participant model once that design is settled.
