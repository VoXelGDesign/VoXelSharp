# VoXelSharp Architecture

## Overview

VoXelSharp is divided into four primary assemblies:

VoXelSharp.Server VoXelSharp.Protocol VoXelSharp.World VoXelSharp.PluginApi


The dependency direction is:

┌────────────────────┐ │ VoXelSharp.Server │ └─────────┬──────────┘ │ ┌───────────────┼───────────────┐ ↓ ↓ ↓ VoXelSharp.Protocol VoXelSharp.World VoXelSharp.PluginApi │ ↓ VoXelSharp.Protocol


Protocol has no dependency on Server or World.

PluginApi has no dependency on Server or World.

---

# VoXelSharp.Protocol

## Responsibility

Protocol owns everything related to Minecraft's binary wire protocol.

It contains:

VarInt VarLong frame encoding frame decoding compression protocol states packet direction packet codecs packet definitions wire-level data types


It must not contain:

world generation player gameplay server lifecycle plugin loading socket ownership


---

# VoXelSharp.Server

## Responsibility

Server coordinates the running application.

It owns:

server startup server shutdown socket listener connection sessions players packet dispatch tick loop world coordination configuration logging


The Server assembly connects Protocol and World.

---

# VoXelSharp.World

## Responsibility

World owns the game world model.

It contains:

World Chunk ChunkSection BlockState IWorldAccess ChunkGenerator FlatWorldGenerator


The World layer should not know about sockets or connection sessions.

---

# VoXelSharp.PluginApi

## Responsibility

PluginApi is the public plugin contract.

It should remain small.

It may eventually contain:

plugin interfaces events event handler contracts plugin metadata plugin API version


It must not contain server implementation details.

---

# Connection Architecture

Each connection follows:

Socket ↓ ConnectionSession ↓ PipeReader / PipeWriter ↓ Frame Codec ↓ Compression ↓ Packet Codec ↓ State + Direction + Packet ID ↓ Typed Packet ↓ Server Logic


Outbound traffic follows the reverse direction.

---

# Connection State

The connection state is one of:

Handshaking Status Login Configuration Play


The state machine must explicitly control which packets are legal.

---

# Packet Dispatch

Packet dispatch must use:

(state, direction, packetId)


Conceptually:

dispatch[state][direction][packetId]


Do not use:

dispatch[packetId]


because packet IDs are state-specific.

---

# Read Loop

The read loop:

ReadAsync ↓ Parse frame ↓ Validate frame length ↓ Decompress if required ↓ Read packet ID ↓ Resolve state/direction/packet ID ↓ Decode packet ↓ Validate packet ↓ Dispatch packet


Malformed input must result in connection cleanup.

It must not escape as an unhandled server exception.

---

# Write Loop

The write loop should consume a bounded channel.

Conceptually:

Server/Game Logic ↓ Channel<OutboundPacket> ↓ Write Loop ↓ Packet Encoder ↓ Compression ↓ Frame Encoder ↓ PipeWriter ↓ Socket


The game/tick thread should not directly perform socket writes.

---

# Backpressure

Outbound traffic must have a bounded queue.

If the client cannot consume data quickly enough, the server must have a defined backpressure policy.

Do not allow an unbounded per-player queue to grow indefinitely.

---

# Cancellation

Each connection should have a cancellation source controlling:

read loop write loop timeouts connection cleanup


All connection tasks must terminate during disconnect.

---

# Connection Cleanup

There should be one central cleanup path.

Conceptually:

Disconnect ↓ Cancel connection ↓ Stop read loop ↓ Stop write loop ↓ Complete pipes ↓ Close socket ↓ Remove player/session ↓ Dispose resources


Cleanup must be safe if called more than once.

---

# World Architecture

The minimal world model is:

World ├── Chunk │ ├── ChunkSection │ ├── ChunkSection │ └── ... └── Chunk


A Chunk is 16×16 blocks horizontally.

Chunk sections are 16×16×16.

The exact vertical world range is determined from the 26.3 registry data.

---

# Flat World

The first world generator should be a simple flat generator.

It should produce:

bedrock dirt grass_block air


The exact Y coordinates must be confirmed from the target version's world-height configuration.

---

# Chunk Serialization

Chunk serialization belongs to the wire/protocol boundary.

The World model should represent semantic world data.

The Protocol layer should represent the binary format.

Do not mix:

Chunk


with:

network packet framing


---

# Performance

Optimize only measured hot paths.

Likely hot paths:

VarInt decoding packet dispatch frame decoding packet encoding chunk serialization tick loop


Use BenchmarkDotNet where microbenchmarking is appropriate.

Use runtime diagnostics for server-level performance.

Do not optimize login/configuration code prematurely.

---

# Garbage Collection

The current project configuration uses workstation/non-concurrent GC settings as an initial experiment.

This is not considered permanently optimal.

Measure before changing it.

---

# Plugin Architecture

Plugins are intentionally deferred.

Initial milestones should not load arbitrary assemblies.

When implemented, plugin loading should use:

AssemblyLoadContext


with explicit lifecycle management.

The intended lifecycle is:

Discover ↓ Load ↓ Enable ↓ Run ↓ Disable ↓ Unload


Hot reload should only be added after normal loading/unloading works reliably.

---

# Security Architecture

Untrusted network input must be considered hostile.

Protocol validation occurs before gameplay logic.

Examples:

bad VarInt bad packet ID wrong state oversized frame bad UTF-8 invalid collection size decompression bomb


These are protocol violations.

Gameplay validation happens after decoding.

Examples:

movement too fast invalid block interaction invalid inventory action


These should generally reject/correct the action rather than immediately terminating the connection.

---

# Testing Architecture

Tests are located in:

tests/VoXelSharp.Tests/


Testing layers:

Unit ↓ Protocol ↓ Connection ↓ World ↓ Integration ↓ Docker


Wire primitives receive the strongest unit-test coverage.

Every packet receives:

round-trip test known-good byte test malformed input test


where applicable.

---

# Architectural Principles

The project follows these principles:

Small components Explicit state Explicit ownership No global packet ID namespace No guessed protocol facts No premature abstractions No premature optimization No unnecessary dependencies Fail closed on malformed network input