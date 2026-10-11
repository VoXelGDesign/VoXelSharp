# VoXelSharp Roadmap

## Target

Minecraft Java Edition 26.3

Protocol:

777


Runtime:

.NET 10


---

# M0 — Foundation

## Goal

Make the repository clean, deterministic, and ready for implementation.

## Tasks

- Fix project naming references.
- Fix Dockerfile project paths.
- Fix Docker entrypoint.
- Fix AGENTS.md paths.
- Fix ROADMAP references.
- Fix .NET 10 test runner configuration.
- Normalize source encoding and line endings.
- Remove stale build artifacts.
- Pin Minecraft 26.3 / protocol 777 in PROTOCOL.md.
- Generate protocol data from the official vanilla 26.3 server.
- Add generated data under `docs/data/`.

## Verification

dotnet build -warnaserror dotnet test dotnet format --verify-no-changes


All must pass.

---

# M1 — Wire Layer

## Goal

Implement the version-independent network primitives.

## Tasks

- VarInt
- VarLong
- frame length encoding
- frame decoding
- PipeReader integration
- PipeWriter integration
- compression wrapper
- decompression size limit
- connection state enum
- packet direction enum
- connection state transition skeleton

## Tests

VarInt:

- zero
- one
- 127
- 128
- 255
- 25565
- int.MaxValue
- -1
- truncated input
- overflow

VarLong:

- zero
- positive boundaries
- long.MaxValue
- -1
- truncated input
- overflow

Frame:

- empty payload
- one-byte payload
- multi-byte length
- maximum valid frame
- oversized frame
- partial input

Compression:

- round-trip
- threshold boundary
- decompressed-size limit
- malformed compressed data

## Completion

All wire-layer tests pass.

---

# M2 — Handshake + Status

## Prerequisite

`docs/data/packets.json` exists and protocol facts are verified.

## Tasks

- Handshake packet
- Status Request
- Status Response
- Ping
- Pong
- connection accept loop
- basic connection session
- server startup
- server shutdown

## Completion

A vanilla client can query the server status.

---

# M3 — Login + Configuration

## Tasks

- Login Start
- offline UUID
- Login Success
- Login Acknowledged
- optional compression
- Configuration state
- Known Packs
- Feature Flags
- Registry Data
- Finish Configuration
- Acknowledge Finish Configuration
- Keep Alive
- Configuration → Play transition

## Completion

A vanilla 26.3 client completes Login and Configuration.

---

# M4 — Play + Flat World

## Tasks

- Join Game
- player spawn
- player position
- player rotation
- Set Center Chunk
- render distance
- Chunk Batch Start
- Chunk Data
- Chunk Batch Finished
- flat world generator
- chunk sections
- block states
- minimal lighting
- required registry data

## World

Initial world:

Y = -64 through -61 bedrock

Y = -60 through -58 dirt

Y = -57 grass_block

Y = -56 through 319 air


The exact height and registry data must be confirmed from generated protocol data before implementation.

## Completion

A vanilla 26.3 client can enter a flat world and stand on the ground.

---

# M5 — Basic Gameplay

## Tasks

- player movement
- movement validation
- teleport confirmation
- block breaking
- block placement
- creative inventory basics
- held item
- player information
- system chat
- block update broadcast

## Security

Reject:

- NaN coordinates
- Infinity coordinates
- impossible movement
- invalid block positions
- invalid interaction distances

## Completion

A player can:

- move
- break blocks
- place blocks
- see chat
- remain connected

---

# M6 — Persistence + Docker

## Tasks

- world save
- world load
- graceful shutdown
- Docker build
- Docker Compose validation
- volume permissions
- persistent world data

## Completion

The server can restart without losing the world.

---

# M7 — Plugins

## Tasks

- PluginApi contracts
- plugin discovery
- AssemblyLoadContext
- plugin lifecycle
- event dispatcher
- join event
- unload testing

Hot reload is optional and should come after reliable startup loading.

## Completion

A test plugin can load and receive an event.

---

# M8 — Architecture Hardening (audit findings)

Findings from the architecture/quality audit (docs/TROUBLESHOOTING.md, this point
added 2026-10-11). Work through the items before or alongside M5/M6 as they unblock
quality work; each is a small logical task.

## Tasks

- Create a `Player` object and per-session event sink (join → play-ready →
  disconnected) so plugin events have something to attach to (currently the session
  owns `_playerProfile` and teleport/keep-alive state privately).
- Wire DI in `Program.cs`/`Worker`: register `PacketRegistry`,
  `IStatusJsonProvider`, `IConfigurationDataProvider`, `IPlayDataProvider`; stop
  `new`-ing providers inside `MinecraftServer`/`Worker`.
- Add an event dispatcher abstraction (server-level, referenced by PluginApi in M7);
  keep it a contract, not an implementation.
- Add the server tick loop (required by M5 and the event source for plugins).
- Data-provider factory helpers (done: `RegistryDataPacket.Create`,
  `RegistryTag.With`/`Empty`, `TaggedRegistry.WithEmptyTags`) — keep using them for
  new registries; consider *generating* registry/tag data from
  `docs/data/registries.json` instead of hand-maintaining it.
- Adopt the packet-test helpers (`Encode`, `DecodeAs`, `AssertInvalidData`) across
  the remaining repetitive test files (ConnectionSessionTests, LoginPacketTests,
  PlayerInfoPacketTests, PlayChunkPacketTests, etc.).
- Delete dead placeholders (`Class1` in Protocol/PluginApi, `UnitTest1.cs`) — done.
- Improve class/member documentation and naming for consistency (player vs entity,
  provider naming) as new work touches those areas; no broad rename churn.

## Completion

The codebase stays buildable and green while the shared seams (player, DI, events,
tick loop) that both M5–M7 depend on are introduced incrementally.

---

# Working Rules

Each implementation task should be small.

Preferred:

VarInt + tests


rather than:

Implement the entire protocol


Do not implement packets until their protocol information exists in:

docs/PROTOCOL.md docs/data/


---

# Current Critical Path

Generate official 26.3 protocol data ↓ M0 Foundation ↓ M1 Wire Layer ↓ M2 Handshake/Status ↓ M3 Login/Configuration ↓ M4 Play/World ↓ M5 Basic Gameplay ↓ M6 Persistence/Docker ↓ M7 Plugins ↓ M8 Architecture Hardening (audit findings; incremental, alongside M5–M7)