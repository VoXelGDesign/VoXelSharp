# VoXelSharp Protocol Specification

## Target Version

Minecraft Java Edition:

26.3


Protocol version:

777


This document is the repository-level protocol specification.

Protocol facts must be verified before implementation.

---

# Source of Truth

Protocol-specific data must come from the generated data under:

docs/data/


Expected generated files:

docs/data/packets.json docs/data/registries.json docs/data/blocks.json


Additional reference material may be used for interpretation, but packet IDs and generated registry/block information must not be invented.

---

# Protocol States

The protocol contains these connection states:

Handshaking Status Login Configuration Play


The normal login flow is:

Handshaking ↓ Login ↓ Configuration ↓ Play


Status is an optional branch from Handshaking.

Handshaking ↓ Status ↓ Disconnect


---

# Packet Identity

A packet is uniquely identified by:

State Direction Packet ID


Direction is one of:

Clientbound Serverbound


There must not be one global packet ID namespace.

---

# Packet Data

Packet mappings are generated from the Minecraft Wiki "Java Edition protocol"
documentation into:

docs/data/packets.json

Status by area:

Packet IDs (handshaking, status, login, configuration, play): VERIFIED Packet fields (handshaking, status, login, configuration): VERIFIED Packet IDs and fields (play): PRESENT (docs/data/packets.json) Registry IDs: PRESENT (docs/data/registries.json) Update Tags: PRESENT (docs/data/registries.json) Block state IDs: PRESENT (docs/data/blocks.json) Chunk wire format: PRESENT (docs/data/chunk_format.md) Composite wire types: PRESENT (docs/data/wire_types.md)


Do not implement packet codecs using guessed IDs.

Protocol facts that the wiki data is stale or missing for must be verified against the official 26.3 client bytecode (decompiled under `vanilla/decompiled-26.3/`, see `docs/TROUBLESHOOTING.md` "Decompiling the 26.3 client as ground truth").

---

# Handshaking

The Handshaking state contains the initial handshake packet.

The handshake selects the next protocol state.

Expected destinations:

Status Login


## Handshake (serverbound, ID 0, official name "intention")

Source: handshaking/serverbound/0 in docs/data/packets.json.

Fields, in order:

Protocol Version: VarInt

Server Address: String (255)

Server Port: Unsigned Short

Intent: VarInt Enum (1 = Status, 2 = Login, 3 = Transfer)


Intents 2 and 3 both transition to the Login state.

---

# Status

The status flow is:

Client ↓ Status Request ↓ Server ↓ Status Response


The ping flow is:

Client ↓ Ping Request ↓ Server ↓ Pong Response ↓ Server closes the connection


All packets below are sourced from docs/data/packets.json (status/*) and the
Minecraft Wiki "Java Edition protocol/Server List Ping" page (captured under
docs/data/_raw/_server_list_ping.raw.wikitext.txt).

## Status Request (serverbound, ID 0, official name "status_request")

No fields.

## Status Response (clientbound, ID 0, official name "status_response")

JSON Response: String (32767)

The response is the server-list JSON. The client may skip the Status Request
and send a Ping Request directly.

Verified JSON schema (Server List Ping page):

{
  "version": {
    "name": "26.3",
    "protocol": 777
  },
  "players": {
    "max": 20,
    "online": 0,
    "sample": [ { "name": "...", "id": "<UUID>" } ]
  },
  "description": {
    "text": "Hello, world!"
  },
  "favicon": "data:image/png;base64,<data>",
  "enforcesSecureChat": false
}

Field notes:

version.name
: mandatory; the server version string shown in the client

version.protocol
: the protocol version number

players.max / players.online
: player counts; values above 2^31-1 are not supported by the vanilla server

players.sample
: optional; omitted when there are no players

description
: optional; when present it is a text component (e.g. {"text": "..."})

favicon
: optional; Base64 PNG without newlines, prefixed with "data:image/png;base64,"

enforcesSecureChat
: optional bool

The Notchian server delays the Status Response until the Ping Request arrives
(30s timeout); this project responds to the Status Request immediately.

## Ping Request (serverbound, ID 1, official name "ping_request")

Timestamp: Long

May be any number; vanilla clients send the departure time in milliseconds.

## Pong Response (clientbound, ID 1, official name "pong_response")

Timestamp: Long

Should match the timestamp sent by the client.

After the Pong Response is sent, the server closes the connection.

---

# Login

The Login state handles authentication/login negotiation.

The expected flow includes:

Login Start Login Success Login Acknowledged


Optional compression negotiation may occur during Login.

Exact packet IDs and fields are in:

docs/data/packets.json


The offline-mode flow is Login Start directly followed by Login Success
(see the 26.3 protocol page; encryption is skipped in offline mode).

---

# Configuration

Configuration is a first-class protocol state.

It must not be skipped.

The expected configuration process includes data required by the client before entering Play.

Relevant concepts include:

Known Packs Feature Flags Registry Data Update Tags Finish Configuration Acknowledge Finish Configuration


Exact packet IDs, field ordering, registry payloads, and transition details are present in:

docs/data/packets.json docs/data/wire_types.md docs/data/registries.json


Configuration flow (from the 26.3 protocol page and Registries page, 2026-10-09):

- Login Acknowledged moves the connection from Login to Configuration.
- The server sends Known Packs (clientbound). The vanilla server does not continue
  until the client replies with Known Packs (serverbound).
- The server then sends Registry Data (one packet per synchronized registry),
  Update Tags, Feature Flags, and finally Finish Configuration.
- The client answers with Acknowledge Finish Configuration, which switches the
  connection state to Play.

The implemented flow (ConnectionSession) advertises the pack minecraft:core at
version 26.3, sends Registry Data with entry data omitted (Prefixed Optional NBT =
false, sourced from known packs), sends the feature flag minecraft:vanilla, empty
Update Tags, and Finish Configuration, then transitions to Play on Acknowledge
Finish Configuration.

---

# Play

Play is the gameplay protocol state.

The first implementation should contain only the packets required to enter a minimal flat world.

Do not implement the entire Play protocol at once.

Initial targets include:

Join Game Set Center Chunk Chunk Batch Start Chunk Data Chunk Batch Finished Player Position/Rotation Set Render Distance Keep Alive


After the initial chunks are sent, the server must send Game Event (clientbound, ID 39) with Event = 13 (`LEVEL_CHUNKS_LOAD_START`); the client stays on "Loading terrain…" until it receives `LEVEL_CHUNKS_LOAD_START` (event 0 `NO_RESPAWN_BLOCK_AVAILABLE` is not required). Source: 26.3 client `ClientboundGameEventPacket` + `LevelLoadTracker` (CFR 0.152 decompile, 2026-10-11); server behavior in `ConnectionSession`.join.


Exact packet IDs and field layouts are present in:

docs/data/packets.json


Play is now fully documented: the packet table is verified, block- and state IDs are captured
in docs/data/blocks.json (compiled from the official 26.3 server data generator), the chunk
wire format is captured in docs/data/chunk_format.md (sections, paletted containers, data
arrays, heightmaps, light data), and registry NBT payloads are available from the 26.3 data
pack in the server.jar (see the readiness footer).


---

# Wire Types

## VarInt

Maximum encoded length:

5 bytes


Overflow must be rejected.

---

## VarLong

Maximum encoded length:

10 bytes


Overflow must be rejected.

---

## Frame Length

Maximum frame length:

2,097,151 bytes


Frames exceeding this limit must be rejected.

---

## Compression

Compression uses the Minecraft protocol compression mechanism.

The implementation must enforce a decompressed payload safety limit.

Initial server-side limit:

8 MiB


Exact compression negotiation and threshold behavior must be verified against the 26.3 protocol specification before Login compression is implemented.

---

# Strings

Strings must be validated according to the limits of the specific packet field.

Invalid UTF-8 must not be silently accepted.

Do not use arbitrary application-level limits where the protocol defines a different limit.

---

# Arrays and Collections

All collection lengths received from the client must be validated before allocation.

Reject:

negative lengths overflowing lengths unreasonably large lengths truncated collections


---

# Position and Coordinates

Network coordinates must be validated where gameplay semantics require it.

Reject invalid floating-point values such as:

NaN Infinity -Infinity


---

# Chunk Data

Modern Minecraft chunk data uses section-based and palette-based representations.

The exact 26.3 format must be obtained from the protocol data before implementation.

The implementation must account for:

chunk sections palettes block states biomes heightmaps block entities lighting chunk batching


No chunk wire layout should be guessed.

---

# Registries

Registry data is required during Configuration.

Expected registry categories include Minecraft-defined registries such as:

dimensiontype biome damagetype chat_type


The exact registry set and contents for 26.3 must come from:

docs/data/registries.json


---

# Block States

Block state IDs are version-specific.

They must come from:

docs/data/blocks.json


Do not hard-code block state IDs based on another Minecraft version.

---

# Security Limits

The protocol implementation must reject:

VarInt overflow VarLong overflow oversized frames oversized decompressed payloads invalid UTF-8 negative collection lengths invalid packet IDs invalid packet state truncated packets malformed packet data


Protocol violations close the connection.

They must never crash the server process.

---

# Verification Rule

Before implementing a packet, its specification must exist here or be directly represented in generated protocol data.

Required information:

state direction packet ID packet name field order field type field constraints


If any required value is missing:

STOP


Do not guess.

---

# Protocol Data Generation

The owner must obtain the official Minecraft 26.3 server distribution and generate the relevant reports.

The generated data should be reviewed and trimmed before being committed to:

docs/data/


The repository should not contain the entire vanilla distribution.

Only the data required for protocol implementation should be committed.

---

# Status

Current protocol readiness:

Version: VERIFIED Protocol: VERIFIED Wire primitives: READY Packet IDs and fields (handshaking, status, login, configuration): PRESENT (docs/data/packets.json) Composite wire types (Identifier, Prefixed Array, Prefixed Optional, NBT, Tag): PRESENT (docs/data/wire_types.md, wiki-captured for 26.3/protocol 777) Play state: PRESENT (packet table in docs/data/packets.json, block state IDs in docs/data/blocks.json, chunk wire format in docs/data/chunk_format.md) Registry data: PRESENT (data-pack-compiled from the 26.3 server.jar) Block state data: PRESENT (docs/data/blocks.json, data-generator-compiled) Chunk format: PRESENT (docs/data/chunk_format.md, wiki-captured for 26.3)


Packet codecs for the Handshaking, Status, Login, and Configuration states are implemented
and tested; NBT and composite wire types are captured in docs/data/wire_types.md. The
Configuration flow (Known Packs negotiation, Registry Data, Update Tags, Feature Flags,
Finish Configuration, Acknowledge Finish Configuration) is wired in the server session.
See the relevant sections above for the verified mappings.
