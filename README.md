# Kestrelle

Kestrelle is a Discord music bot plus a modern web dashboard. The bot handles Discord interactions and audio playback (via Lavalink). The dashboard is a React + TypeScript single-page app served by Nginx, which proxies API calls to a .NET 10 Minimal API.

This repo is designed to run cleanly with **Docker Compose** and keeps the services decoupled so you can deploy them independently later (client, API, bot, Lavalink, DB).

---

## What’s in this repo

### Services (Docker Compose)

- **kestrelle-client**: React + TypeScript + Vite + Tailwind, served by Nginx
- **kestrelle-api**: .NET 10 Minimal API (OAuth, endpoints, SignalR later)
- **kestrelle-bot**: .NET 10 Discord bot (Discord.Net + Lavalink4NET)
- **lavalink**: Lavalink server (audio node)
- **db**: Postgres 16 (EF Core persistence)

### How traffic flows locally

- You visit: `http://localhost:8080`
- `kestrelle-client` serves the SPA
- Requests to `/api/*` are reverse-proxied by Nginx to `kestrelle-api` (same origin = cookies/OAuth work cleanly)

**Important:** In this setup, you normally do **not** browse the API directly via a host port. You access it via the client proxy:
- `http://localhost:8080/api/status`

---

## Repository structure

Top-level overview (paths abbreviated):

```
.
├─ docker-compose.yml
├─ .env
└─ Kestrelle/Kestrelle/Kestrelle/
   ├─ Kestrelle.Api/       # .NET 10 Minimal API
   ├─ Kestrelle.Bot/       # Discord bot
   ├─ Kestrelle.Client/    # React + TS dashboard
   ├─ Kestrelle.Models/    # EF Core DbContext + entities + migrations
   └─ Kestrelle.Shared/    # Shared utilities
```

### Project notes

#### `Kestrelle.Bot`
- Connects to Discord using `Discord__Token`
- Connects to Lavalink using `Lavalink__BaseAddress`, `Lavalink__WebSocketUri`, `Lavalink__Passphrase`
- (Optionally) uses DB via `ConnectionStrings__KestrelleDb`

#### `Kestrelle.Api`
- Minimal API under `/api/*`
- Discord OAuth endpoints (cookie/session based)
- Endpoint to return “available guilds” (intersection of user guilds and bot guilds)

#### `Kestrelle.Client`
- React + TS + Vite + Tailwind
- Calls `/api/*` with `credentials: "include"` so cookie auth works
- Guild dropdown includes a pinned “Kestrelle Dev Server” entry and merges it with API results

#### `Kestrelle.Models`
- EF Core models/migrations live here
- Postgres provider (Npgsql)

---

## Configuration

### `.env` (required)

Example keys (yours may already exist):

```
DISCORD_TOKEN=...
DISCORD_CLIENT_ID=...
DISCORD_CLIENT_SECRET=...
DISCORD_REDIRECT_URI=http://localhost:8080/api/auth/discord/callback

ConnectionStrings_KestrelleDb=Host=db;Port=5432;Database=kestrelle;Username=kestrelle;Password=kestrelle
POSTGRES_DB=kestrelle
POSTGRES_USER=kestrelle
POSTGRES_PASSWORD=kestrelle
```

**OAuth redirect URI must match exactly** what is configured in the Discord Developer Portal:
- `http://localhost:8080/api/auth/discord/callback`

---

## Running locally

Run all commands from the repo root (where `docker-compose.yml` is).

### Quick start (client + API only)

**Build (no cache):**
```bash
docker compose build --no-cache kestrelle-api kestrelle-client
```

**Run (foreground):**
```bash
docker compose up --build kestrelle-api kestrelle-client
```

Open:
- UI: `http://localhost:8080`
- API (through proxy): `http://localhost:8080/api/status`

### Run in the background (`-d`)

The `-d` flag means **detached** mode (containers run in the background; you get your terminal back).

```bash
docker compose up -d --build kestrelle-api kestrelle-client
```

To view logs:
```bash
docker compose logs -f kestrelle-api kestrelle-client
```

To stop:
```bash
docker compose down
```

### Full stack (DB + Lavalink + Bot + API + Client)

```bash
docker compose up --build
```

---

## EF Core migrations (Postgres)

### If running migrations from your host machine

When you run `dotnet ef ...` on your host, the connection string host should usually be `localhost` (because you’re connecting to the mapped port), not the docker service name.

Example host-side connection string:
```
Host=localhost;Port=5432;Database=kestrelle;Username=kestrelle;Password=kestrelle
```

### If running inside containers

Inside Docker Compose, service discovery works by service name:
- DB host is `db`

So container-side connection string:
```
Host=db;Port=5432;Database=kestrelle;Username=kestrelle;Password=kestrelle
```

---

## Discord guild intersection behavior

The API endpoint (example):
- `GET /api/discord/available-guilds`

Works by:
1. Fetching **user guilds** via OAuth access token (`/users/@me/guilds`) — requires scope `guilds`
2. Fetching **bot guilds** via bot token (`/users/@me/guilds` as Bot)
3. Returning the intersection (`userGuilds ∩ botGuilds`)

---

## Troubleshooting

### “localhost refused to connect” for `/api/...`
You should browse the API through the client proxy:
- ✅ `http://localhost:8080/api/status`
- ❌ `http://localhost:8081/api/status` (unless you explicitly map that port)

### OAuth “Invalid Redirect URI”
Ensure:
- `.env` redirect is `http://localhost:8080/api/auth/discord/callback`
- Discord Developer Portal OAuth redirect URI matches exactly

### Client shows only the Dev Server
Common causes:
- API endpoint not mapped in `Program.cs`
- API returning `401` (cookie not present)
- JSON property shape mismatch (e.g., `Id` vs `id`)

Check in browser DevTools → Network:
- `GET /api/discord/available-guilds` should return `200` with JSON.

---

## Future work (planned)

- SignalR hubs (`/hubs/*`) for live now-playing + queue updates
- Bot ↔ API messaging for state synchronization
- Soundboard uploads backed by DB + storage
- Role-based auth and per-guild settings
