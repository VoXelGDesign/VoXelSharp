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

Packet mappings will be populated after the official Minecraft 26.3 data has been generated.

Until then:

Packet IDs: UNVERIFIED Packet fields: UNVERIFIED Registry IDs: UNVERIFIED Block state IDs: UNVERIFIED Chunk wire format: UNVERIFIED


Do not implement packet codecs using guessed IDs.

---

# Handshaking

The Handshaking state contains the initial handshake packet.

The handshake selects the next protocol state.

Expected destinations:

Status Login


Exact packet ID and fields must be populated from:

docs/data/packets.json


---

# Status

The Status flow is:

Client ↓ Status Request ↓ Server ↓ Status Response


The ping flow is:

Client ↓ Ping ↓ Server ↓ Pong


Exact packet IDs and fields are currently:

UNVERIFIED


---

# Login

The Login state handles authentication/login negotiation.

The expected flow includes:

Login Start Login Success Login Acknowledged


Optional compression negotiation may occur during Login.

Exact packet IDs and fields are:

UNVERIFIED


Do not implement until verified against generated data.

---

# Configuration

Configuration is a first-class protocol state.

It must not be skipped.

The expected configuration process includes data required by the client before entering Play.

Relevant concepts include:

Known Packs Feature Flags Registry Data Finish Configuration Acknowledge Finish Configuration


Exact packet IDs, field ordering, registry payloads, and transition details are:

UNVERIFIED


They must be obtained from the official 26.3 protocol data and reference documentation.

---

# Play

Play is the gameplay protocol state.

The first implementation should contain only the packets required to enter a minimal flat world.

Do not implement the entire Play protocol at once.

Initial targets include:

Join Game Set Center Chunk Chunk Batch Start Chunk Data Chunk Batch Finished Player Position/Rotation Set Render Distance Keep Alive


Exact packet IDs and field layouts are:

UNVERIFIED


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

Version: VERIFIED Protocol: VERIFIED Wire primitives: READY Packet IDs: BLOCKED Packet fields: BLOCKED Registry data: BLOCKED Block state data: BLOCKED Chunk format: BLOCKED


Once `docs/data/` is populated, this document should be expanded with the verified packet mappings.