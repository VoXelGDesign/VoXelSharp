# VoXelSharp Roadmap

## Target

Minecraft Java Edition 26.3 · Protocol 777 · Runtime .NET 10

North star: **parity with the official vanilla `server.jar`**, plus the in-house
plugin system already planned.

---

## Status snapshot

| Milestone | Scope | Status |
|-----------|-------|--------|
| M0 Foundation | repo cleanliness, generated protocol data | ✅ done |
| M1 Wire layer | VarInt/VarLong, framing, zlib wrapper, state machine | ✅ done |
| M2 Handshake + Status | server-list ping | ✅ done |
| M3 Login + Configuration | offline login, registry/tags/feature flags | ✅ done |
| M4 Play + flat world | flat world, chunk streaming, block states | ✅ done |
| M5 Basic gameplay | movement + validation, break/place, drops/pickup/inventory, chat echo | ✅ done |
| M6 Persistence + Docker | world diff save/load, graceful shutdown, Docker | ✅ done |
| M7 Server foundations | event seam, `Player`, DI, config, player persistence, multiplayer | ⬜ next |
| M8 Commands & admin | command framework, ops/whitelist/ban, console, RCON | ⬜ |
| M9 World realism | seeded terrain, threaded gen, time/weather, compression | ⬜ |
| M10 Entities & combat | entity model, health/damage/mobs | ⬜ |
| M11 Play long tail | containers/crafting/enchanting/redstone/fluids | ⬜ |
| M12 Dimensions & structures | nether/end, biomes, structures, block entities | ⬜ |
| M13 Scale & hardening | region files, backpressure, rate limiting, fuzzing | ⬜ |
| Plugins | load, lifecycle, events, API, scheduler (parallel) | ⬜ |

Current cumulative test count: **811 passing** (`dotnet test`).

---

## M0 — Foundation ✅

Repo clean, deterministic, ready. Generated protocol data under `docs/data/`.

## M1 — Wire Layer ✅

VarInt, VarLong, frame length encode/decode, PipeReader/PipeWriter integration,
zlib compression wrapper with decompression-size limit, connection-state and
packet-direction enums, and the connection-state transition skeleton.

## M2 — Handshake + Status ✅

Handshake, Status Request/Response, Ping/Pong, connection accept loop, basic
connection session, server startup/shutdown. A vanilla client can query status.

## M3 — Login + Configuration ✅

Login Start (offline), Login Success, Login Acknowledged, Configuration state,
Known Packs, Feature Flags, Registry Data, Finish Configuration, Keep Alive, and
the Configuration → Play transition.

> **Deferred to M7/M9:** online-mode authentication, connection encryption, real
> offline UUID, and login compression negotiation.

## M4 — Play + Flat World ✅

Join Game, spawn, player position/rotation, Set Center Chunk, render distance,
chunk batch start/data/finished, flat world generator, chunk sections, block
states, minimal lighting, and the required registry data. A vanilla client can
enter a flat world and stand on the ground.

## M5 — Basic Gameplay ✅

Player movement with validation and teleport-back, teleport confirmation, block
breaking/placement, creative inventory basics, held item, player information
(tab list), system chat, and block update broadcast.

## M6 — Persistence + Docker ✅

World save/load (`WorldFileStore`, diff-based `data/world/world.bin`), graceful
shutdown, Docker image (chiseled, non-root, read-only), Compose validation,
volume permissions, and persistent world data. See `docs/WORLD_FORMAT.md`.

---

## M7 — Server-side foundations *(next)*

Unblocks every later feature; do before anything stacks on a missing seam.

- Event dispatcher contract (server-level, referenced by PluginApi) — contract only.
- Promote `Player` to a first-class object decoupled from the session (profile,
  position, inventory, health).
- Wire DI in `Program.cs`/`Worker`: register `PacketRegistry`, `IStatusJsonProvider`,
  `IConfigurationDataProvider`, `IPlayDataProvider`; stop `new`-ing them inside
  `MinecraftServer`/`Worker`.
- `server.properties` config (MOTD, port, max players, view distance, seed),
  replacing values hardcoded in `Worker.cs`.
- Player data persistence (`data/players/<uuid>.bin`) mirroring the M6 world-diff pattern.
- Shared player session registry + **chat broadcast** to all players (first visible
  "real server" behavior).
- Keep the server tick loop as the single authoritative tick source (already present).

## M8 — Commands & admin

- Command framework: registry, argument parsing, tab-complete, permission hook.
- Core commands: `/time`, `/gamemode`, `/give`, `/seed`, `/kill`, `/tp`, `/weather`.
- Permissions, `ops.json`, `whitelist.json`, bans; console input loop; RCON.

## M9 — World realism

- `IChunkGenerator` abstraction: keep flat as default, add **seeded noise terrain**
  (biome-lite heightmap).
- **Threaded chunk-generation pool** off the network path.
- Day/night cycle + weather.
- **Compression negotiation** at login.

## M10 — Entity framework + combat

- Non-player entity model (id, type, position, velocity, metadata).
- Health/hunger/damage/death/respawn; fall & fire damage; basic PvP.
- Passive mobs + spawning/despawn; basic hostile AI (pathfinding later).

## M11 — Play-systems long tail

- Inventory/containers/crafting/armor (NBT) → furnace → XP/enchanting/anvil.
- Redstone, pistons, hoppers; fluids (water/lava); lighting recalculation.
- Each is a self-contained sub-milestone; this is the bulk of vanilla surface.

## M12 — Dimensions & structures

- Nether + End (dimension registry + generators), biome generation, structures
  (villages/dungeons), block entities (chests/furnaces/signs/spawners).

## M13 — Scale & hardening

- Region-file (Anvil) chunk persistence (replaces/extends the diff format).
- Outbound `Channel<T>` backpressure; per-entity time-slicing; packet rate
  limiting/anti-cheat; fuzzing (P3); BenchmarkDotNet on measured hot paths.

## Plugins (parallel, built on M7's event seam)

Discovery → `AssemblyLoadContext` load → lifecycle → event delivery → public API
surface → scheduler. Hot reload only after reliable load/unload.

---

## Gap analysis (vs vanilla `server.jar`)

| Domain | Missing (high level) |
|--------|----------------------|
| Security | online auth, encryption, skin verify, real offline UUID, IP forwarding |
| World | seed/noise, biomes, dimensions, structures, block entities, fluids, lighting recalc, time, weather |
| Players | state persistence, health/hunger/damage/respawn, gamemode/abilities, multiplayer list, broadcast |
| Play | containers/crafting/enchanting/XP, redstone, combat, potions |
| Entities | entity framework, mobs, AI/pathfinding, trading |
| Commands | command system, `/…` set, permissions, ops/whitelist/ban, console, RCON |
| Net/perf | compression negotiation, region files, threaded gen, backpressure, rate limiting |
| Plugins | loading, events, API, scheduler |

## Low-hanging fruits (ranked)

1. `server.properties` config
2. Compression negotiation
3. Player data persistence
4. Multi-player list + chat broadcast
5. Command framework + 2–3 commands
6. Time + weather
7. Gamemode/abilities/flying
8. Seeded threaded world generation

---

## Working rules

- One small logical task at a time; `dotnet build -warnaserror`, `dotnet test`, and
  `dotnet format --verify-no-changes` all green after each unit.
- No packet without verified facts in `docs/data/packets.json` or the decompiled
  26.3 client (see `AGENTS.md` rule 1a).
- `UNVERIFIED: <fact>` and stop that portion when a required wire fact is missing.
- New features land as documented increments as they unblock M7→M13.
