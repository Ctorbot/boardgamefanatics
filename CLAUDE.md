# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A hobby site to track board game wins, stats, and collections. Built with ASP.NET Core Blazor (Static SSR) on .NET 10, backed by PostgreSQL via Entity Framework Core.

## Deployment

Hosted on **Azure Container Apps**, database on **Supabase** (hosted Postgres).

Infrastructure is defined in `iac/main.bicep` (Bicep/ARM), which provisions:
- Azure Container Registry (ACR) — name is `boardgamefanatics` + a stable unique suffix derived from the resource group
- Log Analytics workspace — for Container App logs
- Container Apps Environment
- Container App — 0.25 vCPU / 0.5 GiB, scale-to-zero (min 0, max 1 replica), liveness + readiness probes on `/healthz`

CI/CD via `.github/workflows/deploy.yml` — on every push to `main`:
1. Deploys `iac/main.bicep` (idempotent) to provision/update infrastructure
2. Builds and pushes the Docker image to ACR
3. Updates the Container App to the new image

ACR name, login server, and Container App name are read from Bicep deployment outputs — no need to hardcode them as secrets.

**Required GitHub secrets:**
| Secret | Description |
|---|---|
| `AZURE_CLIENT_ID` | Service principal app/client ID |
| `AZURE_CLIENT_SECRET` | Service principal client secret |
| `AZURE_TENANT_ID` | Azure AD tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID |
| `AZURE_RESOURCE_GROUP` | Resource group to deploy into |
| `SUPABASE_CONNECTION_STRING` | Direct connection string (port 5432) |
| `ALERT_EMAIL` | Email to notify when budget thresholds are hit |

The Supabase connection string is stored as a secret on the Container App and injected as `ConnectionStrings__DefaultConnection`.

Use the **direct Supabase connection** (port 5432), not the pooler — EF Core migrations require it.

The app exposes `/healthz` for Container Apps liveness/readiness probes.

## Commands

All commands run from `src/BoardGameFanatics/`:

```bash
# Run the app (requires Postgres running locally on port 5432)
dotnet run

# Build
dotnet build

# Apply EF migrations
dotnet ef database update

# Add a new EF migration
dotnet ef migrations add <MigrationName>

# Run only the database for local development
docker compose -f docker-compose.dev.yml up -d
```

There are no automated tests yet.

## Architecture

Single-project Blazor Web App (Static Server-Side Rendering) using the `Components/` convention:

- **`Program.cs`** — Service registration and middleware pipeline. EF migrations run automatically on startup (`db.Database.Migrate()`).
- **`Components/`** — All Razor components. `App.razor` is the root, `Routes.razor` handles routing, `Layout/` contains `MainLayout` and `NavMenu`, `Pages/` contains page components.
- **`Data/ApplicationDbContext.cs`** — EF Core DbContext. Add new `DbSet<T>` properties here when adding entities.
- **`Models/`** — Plain C# entity classes (e.g., `Player`).

## Database

- **Local dev**: Start Postgres via `docker compose -f docker-compose.dev.yml up -d`, then `dotnet run`. Connection string in `appsettings.Development.json` targets `localhost:5432`.
- **Production**: Supabase (hosted Postgres). Connection string injected into the Container App as `ConnectionStrings__DefaultConnection`.
- Migrations apply automatically at app startup.

## Key Conventions

- Target framework: `net10.0` with nullable reference types and implicit usings enabled.
- ORM: Npgsql EF Core provider (`Npgsql.EntityFrameworkCore.PostgreSQL`).
- New entities go in `Models/`, then add a `DbSet<T>` to `ApplicationDbContext`, then create an EF migration.
- New pages go in `Components/Pages/` as `.razor` files.
