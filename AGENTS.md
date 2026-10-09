# AGENTS.md

## Project

VoXelSharp is a Minecraft Java Edition server implementation targeting:

- Minecraft: 26.3
- Protocol: 777
- Runtime: .NET 10
- Language: C#

The project is intentionally implemented incrementally.

Do not implement protocol packets based on memory or assumptions.

Before starting any task, read `docs/TROUBLESHOOTING.md` for resolved
pitfalls (networking API, analyzer rules, data gaps).

---

## Repository Layout

VoXelSharp/ ├── src/ │ ├── VoXelSharp.Server/ │ ├── VoXelSharp.Protocol/ │ ├── VoXelSharp.World/ │ └── VoXelSharp.PluginApi/ ├── tests/ │ └── VoXelSharp.Tests/ ├── docs/ │ ├── PROTOCOL.md │ ├── ARCHITECTURE.md │ ├── ROADMAP.md │ ├── TROUBLESHOOTING.md │ └── data/ ├── Dockerfile ├── docker-compose.yml ├── Directory.Build.props ├── Directory.Build.targets ├── Directory.Packages.props ├── global.json ├── .editorconfig ├── .gitignore ├── opencode.jsonc └── VoXelSharp.slnx


---

## Hard Rules

### 1. Never invent protocol facts

Protocol facts must come from:

- `docs/PROTOCOL.md`
- `docs/data/`

This includes:

- packet IDs
- packet direction
- packet state
- field order
- field types
- registry IDs
- block state IDs
- chunk wire format
- protocol-specific limits

If a required fact is missing, do not guess.

Instead state:

`UNVERIFIED: <exact fact required>`

and stop that portion of the task.

---

### 2. Minecraft version is fixed

The target version is:

- Minecraft 26.3
- Protocol 777

Do not silently target another Minecraft version.

Do not introduce multi-version abstractions until another version is explicitly supported.

---

### 3. Protocol states are first-class

The protocol must distinguish:

- Handshaking
- Status
- Login
- Configuration
- Play

Packet dispatch must be keyed by:

state + direction + packet ID


Do not create one global packet-ID enum.

Packet IDs are scoped to protocol state and direction.

---

### 4. Configuration is mandatory

Modern Minecraft uses:

Handshaking ↓ Login ↓ Configuration ↓ Play


Do not implement the old simplified:

Login → Play


flow.

Configuration must be represented as a real connection state.

---

### 5. Every implemented packet needs tests

For every packet implemented:

1. Encode/decode round-trip test.
2. At least one known-good byte sequence.
3. At least one malformed-input test.

Where applicable also test:

- truncated input
- invalid lengths
- invalid counts
- invalid state
- unknown packet ID
- overflow
- boundary values

---

### 6. Validate all network input

Never trust client input.

Validate:

- VarInt length
- VarLong length
- frame length
- array lengths
- string lengths
- UTF-8
- packet IDs
- packet state
- compression limits
- coordinates
- floating-point values
- collection counts

Malformed protocol input must never crash the server.

Protocol violations should normally close the connection.

---

### 7. Keep hot paths allocation-conscious

Hot paths include:

- frame decoding
- packet decoding
- packet encoding
- VarInt/VarLong operations
- tick loop
- chunk serialization

Use allocation-free techniques only where they improve measured hot paths.

Do not turn the entire codebase into unsafe/manual-memory code.

Readable C# is preferred.

No optimization without measurement.

---

### 8. Do not add dependencies without approval

Do not add or update NuGet packages unless explicitly requested or approved.

If a package change is required:

1. Explain why.
2. Identify the exact package.
3. Ask for approval if the change is not already authorized.

---

### 9. Do not modify build infrastructure unnecessarily

Do not casually modify:

- `global.json`
- `Directory.Build.props`
- `Directory.Build.targets`
- `Directory.Packages.props`
- `.editorconfig`
- Docker configuration

Only change these when the task explicitly requires it.

---

### 10. One logical task at a time

Prefer small changes.

A good task looks like:

Implement VarInt + tests


or:

Implement Status Request + Response


Avoid combining unrelated refactors with implementation work.

---

### 11. Build and test after logical units

Do not rebuild after every single line or file.

After completing a logical unit:

dotnet build -warnaserror dotnet test dotnet format --verify-no-changes


All three should pass before considering the task complete.

---

### 12. Do not leave unfinished implementation

Finished code must not contain:

TODO


or:

throw new NotImplementedException();


unless the task explicitly creates a documented placeholder.

---

### 13. Never hide errors

Do not:

- swallow exceptions
- ignore failed validation
- silently fall back to guessed protocol behavior
- disable tests to make CI green
- suppress compiler warnings without justification

---

## Project Responsibilities

### VoXelSharp.Protocol

Owns:

- protocol primitives
- VarInt
- VarLong
- framing
- compression
- protocol states
- packet definitions
- packet encoding
- packet decoding
- wire-level types

Must not depend on World or Server.

---

### VoXelSharp.World

Owns:

- worlds
- chunks
- chunk sections
- blocks
- block states
- world access
- generators
- persistence

World serialization should remain separated from networking where practical.

---

### VoXelSharp.Server

Owns:

- sockets
- connections
- connection sessions
- packet dispatch
- players
- server lifecycle
- ticking
- world coordination
- configuration
- logging

---

### VoXelSharp.PluginApi

Owns only plugin contracts.

Keep it small.

Do not put server implementation details in this assembly.

---

## Networking Model

The intended architecture is:

Socket ↓ ConnectionSession ↓ PipeReader / PipeWriter ↓ Frame Codec ↓ Compression ↓ Packet Codec ↓ State + Direction + Packet ID dispatch ↓ Typed packet ↓ Server/session logic


Outbound traffic should use a bounded:

Channel<T>


rather than manually implemented lock-free structures.

---

## Error Handling

Protocol errors:

invalid packet invalid packet ID invalid state invalid VarInt oversized frame invalid UTF-8 invalid length decompression limit exceeded


normally result in:

log → disconnect → cleanup


They must not bring down the server.

Gameplay validation failures are different.

For example:

movement too fast block interaction too far away invalid gameplay action


should generally reject/correct the action while keeping the connection alive.

---

## Security Limits

The implementation must enforce at least:

VarInt maximum: 5 bytes VarLong maximum: 10 bytes Maximum frame length: 2,097,151 bytes Maximum decompressed payload: 8 MiB


Additional per-packet limits must be documented before implementation.

---

## Testing

Unit tests live in:

tests/VoXelSharp.Tests/


Priority:

P0 VarInt VarLong Frame codec Compression

P1 Protocol state machine Packet codecs

P2 World/chunk Gameplay

P3 Fuzzing Docker integration


---

## Current Implementation Order

Follow:

M0 Foundation ↓ M1 Wire Layer ↓ M2 Handshake + Status ↓ M3 Login + Configuration ↓ M4 Play + Flat World ↓ M5 Basic Gameplay ↓ M6 Persistence + Docker ↓ M7 Plugins


Do not skip dependencies.

---

## Protocol Data Rule

The directory:

docs/data/


is authoritative generated protocol data.

Expected files include:

docs/data/packets.json docs/data/registries.json docs/data/blocks.json


Do not fabricate these files.

If they do not exist, packet implementation beyond protocol-independent primitives is blocked.

---

## Definition of Done

A task is complete only when:

1. The requested functionality exists.
2. The implementation follows the architecture.
3. Tests exist.
4. Tests pass.
5. Build passes with warnings treated as errors.
6. Format verification passes.
7. No unrelated files were changed.
8. No protocol facts were guessed.
9. No unfinished implementation remains.
10. The final response clearly states what changed and what was verified.
