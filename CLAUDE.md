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
| `SUPABASE_CONNECTION_STRING` | Session pooler connection string (port 5432), in Prisma's URI format: `postgresql://postgres.<project-ref>:<password>@aws-0-<region>.pooler.supabase.com:5432/postgres` |
| `NEXT_PUBLIC_SUPABASE_URL` | Supabase project URL (e.g. `https://<project-ref>.supabase.co`) — not sensitive, but must be a GitHub secret (or repo variable) so it reaches the Docker build step |
| `NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY` | Supabase publishable/anon key — same deal, browser-safe but still needs to flow through CI |
| `ALERT_EMAIL` | Email to notify when budget thresholds are hit |

The Supabase connection string is stored as a secret on the Container App and injected as `DATABASE_URL` (the env var Prisma reads). It must be the `postgresql://` URI format — not the ADO.NET/Npgsql key-value format (`Host=...;Database=...;Username=...;Password=...`) used by the old EF Core app.

`NEXT_PUBLIC_SUPABASE_URL` and `NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY` are different from the other secrets: Next.js inlines `NEXT_PUBLIC_*` env vars into the production bundle **at build time**, not runtime, regardless of whether the referencing code is client- or server-side. They're passed into `docker build` as `--build-arg`s in `deploy.yml` and consumed by `ARG`/`ENV` in the Dockerfile's `build` stage — setting them only as Container App runtime env vars would not work, since the compiled bundle would already have `undefined` baked in for them.

Use the **Session pooler** connection string (Supabase dashboard: **Project Settings → Database → Connection string → Session**), not the raw direct host (`db.<project-ref>.supabase.co`) and not the **Transaction** pooler. The direct host now resolves to an IPv6-only address, which GitHub Actions' hosted runners can't reach — the session pooler has an IPv4 endpoint and (unlike the transaction pooler on port 6543) preserves session semantics, so `prisma migrate deploy` still works correctly. Note the pooler username is `postgres.<project-ref>`, not just `postgres`.

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

- **`app/layout.js`** — Root layout; calls `getCurrentPlayer()` and passes the result to the nav (`app/nav-menu.js`), imports global styles.
- **`app/page.js`**, **`app/players/page.js`** — Page components (Server Components by default).
- **`app/login/`**, **`app/signup/`** — Auth pages + their Server Actions (`actions.js`).
- **`app/admin/players/`** — Admin-only pending-player approval queue.
- **`app/healthz/route.js`** — Route Handler used by the Container Apps health probes.
- **`lib/db.js`** — Exports a singleton `PrismaClient` instance. Import this wherever DB access is needed.
- **`lib/auth.js`** — `getCurrentPlayer()`, `requireApproved()`, `requireAdmin()` — call these (not raw Supabase calls) to gate any page/action that needs an authenticated, approved, or admin player.
- **`lib/supabase/`** — Supabase client factories: `client.js` (browser), `server.js` (Server Components/Actions, uses `next/headers` cookies), `proxy.js` (session refresh, used by root `proxy.js` — Next.js 16 renamed the `middleware` file convention to `proxy`).
- **`prisma/schema.prisma`** — Prisma schema. Add new models here, then run a migration.

## Authentication & Players

Supabase Auth (not just Supabase Postgres) handles signup/login/sessions. The `Player` table (`prisma/schema.prisma`) extends Supabase's `auth.users`:

- `id` is the same UUID as `auth.users.id` — populated by a database trigger on signup, not by the app.
- `status`: `PENDING` (default on signup, read-only everywhere) or `APPROVED`.
- `role`: `PLAYER` (default) or `ADMIN`.

The `auth.users` → `Player` link (FK + `handle_new_player()` trigger that inserts a `PENDING` row on signup) lives in the `add_auth_trigger` migration, guarded with `IF EXISTS (... schema_name = 'auth')` — the `auth` schema only exists on Supabase-hosted Postgres, not local dev Postgres (`docker-compose.dev.yml`), so this migration is a no-op locally and only takes effect against Supabase.

New env vars in `src/web/.env` (gitignored): `NEXT_PUBLIC_SUPABASE_URL`, `NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY` (Supabase's newer publishable-key format — browser-safe, unlike `DATABASE_URL`). Admin approval: `/admin/players` lists `PENDING` players for an `ADMIN`-role player to approve.

## Database

- **Local dev**: Start Postgres via `docker compose -f docker-compose.dev.yml up -d`, then `npm run dev` from `src/web/`. Connection string in `src/web/.env` (`DATABASE_URL`, gitignored) targets `localhost:5432`.
- **Production**: Supabase (hosted Postgres). Connection string injected into the Container App as `DATABASE_URL`.
- Migrations do **not** run automatically at app startup — they're applied via `prisma migrate deploy` as a CI/CD step in `deploy.yml`.

There are two Compose files at the repo root, for different purposes:
- `docker-compose.dev.yml` — Postgres only, port 5432 published to the host. Use this when running the app with `npm run dev` directly on the host.
- `docker-compose.yml` — full stack (`app` built from the root `Dockerfile`, plus `db`), for testing the production container image locally. `db` here also publishes port 5432, so don't run both files at once. This stack does **not** run migrations automatically — after `docker compose up -d --build`, run `npx prisma migrate deploy` from `src/web/` (with `DATABASE_URL` pointed at `localhost:5432`) before hitting any DB-backed route.

## Roadmap

The product vision: a board game collection + plays tracker. Players sign up (admin-approval gated, read-only until approved), track their own game collections (public read, self-only write), and log plays of games sourced from BoardGameGeek's catalog.

**Done:** Player auth + admin approval workflow (see Authentication & Players above).

**Next, in order:**

### Phase 2: Game catalog (BoardGameGeek integration)

Games are sourced from BoardGameGeek's XML API2, not freeform entries — one shared `Game` table caches BGG data, keyed by BGG's own id, referenced by every player's collection/plays. Any approved player can add a game to the local catalog if it isn't already cached — **no admin approval needed for games** (unlike player signups), since BGG itself is the authority on what's valid.

BGG's API now requires `Authorization: Bearer <token>` — a real change from its historically open access (confirmed by testing; the docs page itself is behind a Cloudflare bot challenge that blocks automated fetching, so this was learned empirically, not from the wiki). `BGG_API_TOKEN` is already in `.env`. Confirmed endpoint shapes (tested directly):
- `GET /xmlapi2/search?query={name}&type=boardgame` → `<items total="N"><item id="ID"><name type="primary|alternate" value="..."/><yearpublished value="YYYY"/></item>...</items>`
- `GET /xmlapi2/thing?id={id}&stats=1` → full details: `thumbnail`, `image`, multiple `name` entries (primary + many alternates — use `type="primary"`), `description`, `yearpublished`, `minplayers`/`maxplayers`, `playingtime`/`minplaytime`/`maxplaytime`, `minage`

Planned `Game` model: `bggId Int @id`, `name`, `yearPublished`, `minPlayers`, `maxPlayers`, `playingTimeMinutes`, `minAge`, `description` (`@db.Text`), `thumbnailUrl`, `imageUrl`, `cachedAt`. Needs an XML parser dependency (Node has none built in) — `fast-xml-parser` is the standard choice. Planned `lib/bgg.js`: `searchGames(query)`, `getGameDetails(bggId)`, `findOrCacheGame(bggId)` (check local cache first, fetch + `db.game.create` on miss).

### Phase 3: Collections ("bookshelves")

`CollectionEntry` joins `Player` ↔ `Game` (`@@unique([playerId, gameId])`). Public read (anyone can view anyone's collection), but only the owning player can edit their own — gate writes with `requireApproved()` from `lib/auth.js` plus an explicit ownership check (`session player.id === collection owner's id`) in the Server Action.

### Phase 4: Plays

`Play` (gameId, playedAt, notes) + a join table (`PlayParticipant`: playId, playerId, generic `won`/`score` fields). **Per-game win conditions and metrics are explicitly undesigned/deferred** — revisit the participant model once that design is settled, rather than guessing at a schema now.

## Key Conventions

- Plain JavaScript (no TypeScript) for app code. `prisma.config.ts` is a Prisma tooling file and not part of the app build.
- ORM: Prisma (`prisma-client-js` generator, with `linux-musl-openssl-3.0.x` added to `binaryTargets` for the Alpine-based Docker runtime).
- New entities go in `prisma/schema.prisma`, then run `npx prisma migrate dev --name <name>`.
- New pages go in `app/` as `page.js` files following Next.js App Router routing conventions.
- `BGG_API_TOKEN` (`src/web/.env`, gitignored) is a bearer token for BoardGameGeek's XML API2 (`boardgamegeek.com/xmlapi2/...`), which now requires `Authorization: Bearer <token>` — used by the (not yet built) Game catalog caching feature.
