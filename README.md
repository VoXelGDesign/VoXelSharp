# VoXelSharp

A from-scratch **Minecraft: Java Edition** server implementation in **C# on .NET 10**.

| | |
|---|---|
| **Target version** | Minecraft **26.3** (protocol **777**) |
| **Runtime** | .NET **10** |
| **Language** | C# |
| **Status** | Active development — playable flat world, not yet vanilla-complete |

VoXelSharp is built incrementally and intentionally: the wire protocol, world
model, and gameplay are landeded milestone by milestone, each backed by tests and
verified against the official 26.3 client bytecode when community data is stale.

---

## What works today

- **Full connection lifecycle** — handshake → status (server-list ping) → login →
  configuration → play, matching vanilla's mandatory modern flow (no legacy
  login→play shortcut).
- **Playable flat world** — a vanilla client connects, spawns, walks on the
  surface, breaks and places blocks, drops/picks up items with a real inventory,
  and stays connected.
- **Protocol-correct wire layer** — VarInt/VarLong, frame codec, zlib compression
  wrapper, state-scoped packet IDs (`state + direction + packetId`).
- **Persistence** — world changes are saved to `data/world/` and restored on
  restart (see `docs/WORLD_FORMAT.md`).
- **Docker** — a hardened (chiseled, non-root, read-only) image with reproducible
  build/run scripts.

Built so far (M0–M6): handshake/status, offline login, configuration, flat-world
chunk streaming, player movement + validation, block break/place, item
drop/pickup/inventory, chat echo, world save/load, graceful shutdown, Docker.

## Quick start

### Run locally

```powershell
dotnet run --project src\VoXelSharp.Server
```

The server listens on port **25565**. Point a vanilla 26.3 client (licensed, from
the official launcher) at `localhost:25565`. Login is offline (no Mojang account
check) for now.

### Run in Docker

```powershell
tools\docker\dev.ps1 up        # build + run in the foreground
tools\docker\dev.ps1 up -d     # build + run detached
tools\docker\dev.ps1 logs      # follow logs
tools\docker\dev.ps1 down      # stop + remove
```

or with Compose:

```bash
docker compose up --build -d
docker compose down
```

World data persists to `data/world/`; plugin directory is `data/plugins/`.

### Verify

```powershell
dotnet build -warnaserror
dotnet test              # currently 811 tests
dotnet format --verify-no-changes
```

---

## Project structure

| Path | Responsibility |
|------|----------------|
| `src/VoXelSharp.Protocol` | wire primitives, framing, compression, packet codecs |
| `src/VoXelSharp.World` | world model, chunks, block states, world generators, persistence |
| `src/VoXelSharp.Server` | sockets, sessions, packet dispatch, players, tick loop, lifecycle |
| `src/VoXelSharp.PluginApi` | plugin contracts (intentionally minimal; empty for now) |
| `docs/` | architecture, protocol spec, roadmap, troubleshooting, protocol data |
| `tests/VoXelSharp.Tests` | unit + protocol + connection + world + integration tests |
| `tools/` | container dev scripts |

Dependency direction is strict: **Server → (Protocol, World, PluginApi)**;
Protocol and PluginApi depend on nothing in the repo.

## Roadmap

The north star is **parity with the official vanilla `server.jar`**, plus an
in-house plugin system. The full milestone plan lives in
[`docs/ROADMAP.md`](docs/ROADMAP.md).

| Milestone | Scope | Status |
|-----------|-------|--------|
| M0–M6 | foundation → wire → handshake → login → flat world → gameplay → persistence | ✅ done |
| M7 | server foundations: event seam, `Player` object, DI, config, player persistence, multiplayer | ⬜ next |
| M8 | commands & admin (registry, `/…` commands, ops/whitelist/ban, console, RCON) | ⬜ |
| M9 | world realism (seeded terrain, threaded generation, time/weather, compression) | ⬜ |
| M10 | entities & combat (entity model, health/damage, mobs) | ⬜ |
| M11 | play long tail (containers/crafting/enchanting, redstone, fluids) | ⬜ |
| M12 | dimensions & structures (nether/end, biomes, block entities) | ⬜ |
| M13 | scale & hardening (region files, backpressure, rate limiting, fuzzing) | ⬜ |
| Plugins | load, lifecycle, events, API, scheduler (parallel) | ⬜ |

## Documentation

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — assemblies, connection and world model.
- [`docs/PROTOCOL.md`](docs/PROTOCOL.md) — protocol spec + source-of-truth rules.
- [`docs/ROADMAP.md`](docs/ROADMAP.md) — full milestone plan + gap analysis.
- [`docs/TROUBLESHOOTING.md`](docs/TROUBLESHOOTING.md) — resolved pitfalls and decisions.
- [`docs/WORLD_FORMAT.md`](docs/WORLD_FORMAT.md) — the `world.bin` persistence format.
- [`docs/data/`](docs/data) — authoritative protocol data (packets, registries, blocks, chunk/wire formats).

## Contributing

Development follows strict rules (see [`AGENTS.md`](AGENTS.md)): protocol facts
come only from `docs/data/` or the decompiled 26.3 client (`UNVERIFIED:` and stop
otherwise); every packet needs round-trip + known-good-byte + malformed-input
tests; malformed network input never crashes the server; one logical task at a
time with all three verification gates green.

---

*VoXelSharp is a hobby/educational reimplementation. Minecraft is a trademark of
Mojang Studios; this project is not affiliated with or endorsed by Mojang.*
