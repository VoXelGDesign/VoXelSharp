# Troubleshooting & Decisions

This file documents problems, wrong turns, and resolved uncertainties
encountered during implementation.  Future agents should read this before
repeating the same mistakes.

---

# .NET 10 Networking API

## The mistake

Earlier agents used Java-style class names:

- `ServerSocket` — **does not exist** in C#/.NET.  This is a `java.net` type.
- `Socket` (as a server-side accept type) — also Java-style.
- `System.Networking.TcpListener` — correct type *name* but wrong *namespace*.

## The correct API

The .NET 10 networking types for TCP are in **`System.Net.Sockets`**:

| What you need | Type | Namespace |
|---|---|---|
| TCP server (listener) | `TcpListener` | `System.Net.Sockets` |
| Accepted client connection | `TcpClient` | `System.Net.Sockets` |
| Bidirectional data stream | `NetworkStream` | `System.Net.Sockets` |
| IP address | `IPAddress` | `System.Net` |
| Endpoint (address+port) | `IPEndPoint` | `System.Net` |

No extra NuGet package is needed.  `System.Net.Sockets` is part of the .NET 10
BCL (its types live in `System.dll` / `System.Net.Sockets.dll`, both shipped
with the runtime).

## Constructor gotcha

`TcpListener` has three constructors:

- `TcpListener(int port)` — **Obsolete** (do not use)
- `TcpListener(IPAddress localAddr, int port)` — the current recommended form
- `TcpListener(IPEndPoint endpoint)` — also valid

Example:

```csharp
using System.Net;
using System.Net.Sockets;

var listener = new TcpListener(new IPEndPoint(IPAddress.Any, 25565));
// or:  new TcpListener(IPAddress.Any, 25565)

listener.Start();
TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);
NetworkStream stream = client.GetStream();
// ... read/write with stream ...
listener.Stop();
```

## Lifecycle rules

- `TcpListener` implements `IDisposable` (synchronous `Dispose()` via
  `Stop()`).
- `TcpClient` implements `IDisposable` (synchronous `Dispose()` via
  `Close()` or `using` block).
- `NetworkStream` implements `IAsyncDisposable` (asynchronous
  `DisposeAsync()` via `await using`).
- `NetworkStream` **does not own the underlying socket**.  Closing it does
  not close the TCP connection.  The owning `TcpClient` must be disposed
  to close the connection.
- Always dispose in this order: `NetworkStream` first (inner block), then
  `TcpClient` (outer block).

## Server accept loop pattern

```csharp
// Accept loop — handler owns the TcpClient, NOT the loop.
TcpClient client = await listener.AcceptTcpClientAsync(ct);
_ = Task.Run(() => HandleClientAsync(client));

// Handler disposes both stream and client in order.
private async Task HandleClientAsync(TcpClient client)
{
    using (client)                         // outer: dispose client last
    {
        await using NetworkStream stream = client.GetStream();  // inner: dispose stream first
        var pipe = new StreamDuplexPipe(stream);
        var session = new ConnectionSession(pipe, ...);
        await session.RunAsync(ct);
    }
}
```

---

# FrameCodec convention: prefix ≤ 3 bytes

The FrameLength codec allows each frame-length prefix to be encoded with at
most 3 bytes (even though VarInt logic itself allows longer; see
`FrameLength.MaxEncodedLength`).  The over-long 3-byte encoding is permitted
so that a frame that is 127 bytes or less can still use 3 bytes if the sender
chooses.  The decoder does not reject over-long prefixes as long as they
fit in 3 bytes.  This was a design decision made during FrameCodec
implementation — revisit if interoperability issues arise.

---

# PipeReader / PipeWriter endian

When the FrameCodec was first written, it used an endian-inverted VarInt
decode (`group << (7 * i)`) which caused all tests to fail byte-equality
until a reviewer caught the bug.  The span-based decode was then corrected.

---

# Test-file final newline

The `write` tool strips the trailing newline.  After every write, the file
must be normalized with PowerShell, adding back the final `\n`, or edited
via the `edit` tool (which preserves existing newlines).

PowerShell normalize pattern:

```powershell
$text = [System.IO.File]::ReadAllText($path)
$text = $text.Replace("`r`n", "`n")
if (-not $text.EndsWith("`n")) { $text += "`n" }
[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
```

---

# NullLogger from Microsoft.Extensions.Logging.Abstractions

`NullLogger<T>.Instance` is useful for tests.  It is available transitively
via the `Microsoft.Extensions.Hosting` package (which depends on
`Microsoft.Extensions.Logging.Abstractions`).  No separate package reference
is needed.

---

# PacketId collisions across states

Packet ID 0 means *Handshake* in `(Handshaking, Serverbound)` but
*StatusRequest* in `(Status, Serverbound)`.  Packet ID 1 means *PingRequest*
in `(Status, Serverbound)` but may be something else in other states.
**Do not merge packet IDs into a global enum.**  The registry is keyed by
`(state, direction, packetId)` so collisions are safe, but any attempt to
build a unified ID lookup will fail.

---

# CA1859: concrete codec types

The codec helpers (e.g., `ClientSendAsync` in tests) take `IPacketCodec`
parameter but the analyzer suggests narrowing to a concrete codec type.
Fix: make the helper generic over `PacketCodec<TPacket>`:

```csharp
private async Task ClientSendAsync<TPacket>(PacketCodec<TPacket> codec, TPacket packet)
    where TPacket : IPacket
```

This satisfies the analyzer without losing polymorphism.

---

# CA1848 / CA1873: LoggerMessage delegates

Direct `_logger.LogWarning(...)` calls are flagged.  All log calls must use
`LoggerMessage.Define<T>(...)` static delegates.  Pattern:

```csharp
private static readonly Action<ILogger, string, Exception?> _someMessage =
    LoggerMessage.Define<string>(LogLevel.Warning, new EventId(N, nameof(MyClass)), "Message template: {Arg}");

// Usage:
_someMessage(_logger, argValue, null);
```

For error-level messages with an exception:

```csharp
private static readonly Action<ILogger, Exception?> _someError =
    LoggerMessage.Define(LogLevel.Error, new EventId(N, nameof(MyClass)), "Error description.");

// Usage:
catch (Exception ex) { _someError(_logger, ex); }
```

---

# CA1707: Underscores in member names

Test method names must not contain underscores (`_`).  Use PascalCase
instead: `StatusRequestGetsStatusResponse` not
`StatusRequest_GetsStatusResponse`.

---

# CA1001: Disposable fields

A class holding disposable fields (e.g. `TcpListener`, `CancellationTokenSource`)
must implement `IDisposable` or `IAsyncDisposable` itself.  The dispose
method should delegate to `StopAsync()` or similar.

---

# Protocol version for status pings

Wiki: "The version that the client plans on using to connect to the server
(which is not important for the ping). If the client is pinging to determine
what version to use, by convention -1 should be set."

During the M2 status flow, the server **must not reject** based on protocol
version.  Protocol version validation is a login-state concern (M3+).

---

# Missing protocol data blocks

Current status:

- `docs/data/packets.json` — present, covers all states
- `docs/data/registries.json` — **missing** (blocks M3 Login + Configuration)
- `docs/data/blocks.json` — **missing** (blocks M4 gameplay)
- `docs/data/_raw/_server_list_ping.raw.wikitext.txt` — present (gitignored provenance)

Before implementing any packet codec for a state that isn't already
documented in `docs/PROTOCOL.md` with VERIFIED status, the data files
must be generated.  Vanilla data generators or Burger-based tooling are
the planned approach.
