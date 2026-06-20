# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A hobby site to track board game wins, stats, and collections. Built with Next.js (App Router, React) on Node.js, backed by PostgreSQL via Prisma.

## Deployment

Hosted on **Azure Container Apps**, database on **Supabase** (hosted Postgres).

Infrastructure is defined in `iac/main.bicep` (Bicep/ARM), which provisions:
- Azure Container Registry (ACR) — name is `boardgamefanatics` + a stable unique suffix derived from the resource group
- Log Analytics workspace — for Container App logs
- Container Apps Environment
- Container App — 0.25 vCPU / 0.5 GiB, scale-to-zero (min 0, max 1 replica), liveness + readiness probes on `/healthz`

CI/CD via `.github/workflows/deploy.yml` — on every push to `main`:
1. Deploys `iac/main.bicep` (idempotent) to provision/update infrastructure
2. Applies Prisma migrations against Supabase (`prisma migrate deploy`)
3. Builds and pushes the Docker image to ACR
4. Updates the Container App to the new image

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

The Supabase connection string is stored as a secret on the Container App and injected as `DATABASE_URL` (the env var Prisma reads).

Use the **direct Supabase connection** (port 5432), not the pooler — Prisma migrations require it.

The app exposes `/healthz` (a Route Handler) for Container Apps liveness/readiness probes.

## Commands

All commands run from `src/web/`:

```bash
# Install dependencies (also runs `prisma generate` via postinstall)
npm install

# Run the app in dev mode (requires Postgres running locally on port 5432)
npm run dev

# Build for production
npm run build

# Run the production build
npm start

# Apply Prisma migrations to the database
npx prisma migrate deploy

# Create a new migration from schema changes
npx prisma migrate dev --name <migration-name>

# Run only the database for local development
docker compose -f docker-compose.dev.yml up -d
```

There are no automated tests yet.

## Architecture

Single Next.js app (App Router) using the `app/` convention:

- **`app/layout.js`** — Root layout; renders the nav (`app/nav-menu.js`) and imports global styles.
- **`app/page.js`**, **`app/players/page.js`** — Page components (Server Components by default).
- **`app/healthz/route.js`** — Route Handler used by the Container Apps health probes.
- **`lib/db.js`** — Exports a singleton `PrismaClient` instance. Import this wherever DB access is needed.
- **`prisma/schema.prisma`** — Prisma schema. Add new models here, then run a migration.

## Database

- **Local dev**: Start Postgres via `docker compose -f docker-compose.dev.yml up -d`, then `npm run dev` from `src/web/`. Connection string in `src/web/.env` (`DATABASE_URL`, gitignored) targets `localhost:5432`.
- **Production**: Supabase (hosted Postgres). Connection string injected into the Container App as `DATABASE_URL`.
- Migrations do **not** run automatically at app startup — they're applied via `prisma migrate deploy` as a CI/CD step in `deploy.yml`.

There are two Compose files at the repo root, for different purposes:
- `docker-compose.dev.yml` — Postgres only, port 5432 published to the host. Use this when running the app with `npm run dev` directly on the host.
- `docker-compose.yml` — full stack (`app` built from the root `Dockerfile`, plus `db`), for testing the production container image locally. `db` here also publishes port 5432, so don't run both files at once. This stack does **not** run migrations automatically — after `docker compose up -d --build`, run `npx prisma migrate deploy` from `src/web/` (with `DATABASE_URL` pointed at `localhost:5432`) before hitting any DB-backed route.

## Key Conventions

- Plain JavaScript (no TypeScript) for app code. `prisma.config.ts` is a Prisma tooling file and not part of the app build.
- ORM: Prisma (`prisma-client-js` generator, with `linux-musl-openssl-3.0.x` added to `binaryTargets` for the Alpine-based Docker runtime).
- New entities go in `prisma/schema.prisma`, then run `npx prisma migrate dev --name <name>`.
- New pages go in `app/` as `page.js` files following Next.js App Router routing conventions.
