# `docs/data/` Schema

Authoritative protocol data for Minecraft **26.3 / protocol 777**.

These files are the single source of wire facts for packet and registry
implementation. They are generated from the Minecraft Wiki Java Edition
"Java Edition protocol" documentation (CC BY-SA 3.0) by
`source/_generate_packets.py`. Facts that the wiki data is stale or missing for
are verified against the official 26.3 client bytecode (decompiled under
`vanilla/decompiled-26.3/`) and marked with a `_Source:` line — see
`docs/TROUBLESHOOTING.md` "Decompiling the 26.3 client as ground truth". The large
raw source pages fetched locally to regenerate this data are **not committed** —
see `_raw/` (gitignored), and `.gitignore` for the excluded paths.

## Metadata fields (appear on every generated file)

| Field      | Type   | Meaning                                             |
|------------|--------|-----------------------------------------------------|
| `_comment` | string | Provenance, generation method, licensing notice.    |
| `version`  | string | Minecraft release, e.g. `"26.3"`.                   |
| `protocol` | int    | Protocol version number, `777` for 26.3.            |

Files:

- `packets.json` — packet index + field layout (schema below).
- `registries.json` — synchronized registry contents (present; data-pack-compiled from the 26.3 server.jar).
- `blocks.json` — block state IDs (present; compiled from the official 26.3 data generator).
- `wire_types.md` — composite wire types (wiki captures + 26.3 client-bytecode verifications).
- `chunk_format.md` — chunk wire format for protocol 777 (wiki + 26.3 client verification).

---

## `packets.json`

Top-level shape:

```json
{
  "_comment": "...",
  "version": "26.3",
  "protocol": 777,
  "packets": [ /* Packet ... */ ]
}
```

### Packet entry

```json
{
  "state": "login",
  "direction": "clientbound",
  "packetId": 2,
  "packetIdHex": "0x2",
  "name": "Login Success",
  "officialName": "login_finished",
  "fields": [ /* Field ... */ ],
  "unverified": false
}
```

| Field          | Type                 | Meaning                                                                 |
|----------------|----------------------|-------------------------------------------------------------------------|
| `state`        | string               | One of `handshaking`, `status`, `login`, `configuration`, `play`.       |
| `direction`    | string               | `clientbound` (server→client) or `serverbound` (client→server).         |
| `packetId`     | int                  | Numeric packet ID within the state+direction. **Never global.**         |
| `packetIdHex`  | string               | Hex form of `packetId`, e.g. `"0x2"`.                                   |
| `name`         | string               | Human-readable packet name from the protocol documentation.             |
| `officialName` | string               | Stable mapping name from the vanilla data generators, e.g. `login_finished`. |
| `fields`       | Field[]              | Ordered field layout. Empty for no-field packets.                       |
| `unverified`   | boolean              | Present and `true` only when the source documents the packet ID/name but no field table. **Do not implement schemas for these without a verified source.** |

> `state` + `direction` + `packetId` uniquely identify a packet; packet IDs are
> scoped and must never be treated as a global namespace.

### Field entry

```json
{
  "name": "Threshold",
  "type": "VarInt",
  "notes": null
}
```

| Field   | Type   | Meaning                                                                                                            |
|---------|--------|--------------------------------------------------------------------------------------------------------------------|
| `name`  | string | Field name (leading whitespace trimmed).                                                                            |
| `type`  | string | Wire type as documented, e.g. `VarInt`, `String (255)`, `Prefixed Array`, `Prefixed Optional NBT`. Element/composite context appears in `elements` when present. `null` when unknown. |
| `notes` | string | Plain-text notes (constraints, semantics). `null` when absent.                                                     |

Field order is significant and matches the documentation.

### Nested element layout (arrays / compound / variant tables)

Some field tables use `rowspan`-spanning cells (arrays of records, or
action-discriminated variants such as Boss Bar). Those fields carry an
`elements` array that captures the rows grouped under the spanning name:

```json
{
  "name": "Entries",
  "type": "Prefixed Array",
  "elements": [
    { "name": "Entry ID", "type": "Identifier" },
    { "name": "Data", "type": "Prefixed Optional NBT" }
  ]
}
```

Element objects use the same `name`/`type`/`notes` shape. Consumers must treat
`elements` as documentation of the nested record/variant layout; the exact
wire encoding (e.g. count prefix) comes from the `type` field and the
documentation notes.

### Known gaps

- Play clientbound `Ping Response` (id 63, `pong_response`) and Play serverbound
  `Ping Request` (id 38, `ping_request`) are listed by the protocol index but
  have **no field table in the wiki**; both carry `"unverified": true`.
  Do not implement them without a verified source.

---

## Type vocabulary (informative)

Common wire types seen in `type` fields:

`Boolean`, `Byte`, `Unsigned Byte`, `Short`, `Unsigned Short`, `Int`, `Long`,
`Float`, `Double`, `VarInt`, `VarLong`, `String (N)`, `Identifier`, `UUID`,
`Position`, `Angle`, `Byte Array`, `Array`, `Prefixed Array`, `Prefixed
Optional`, `Optional`, `NBT`, `Text Component`, `Game Profile`, `BitSet`,
`Bit Set`, `Varies`, `Enum` (suffix), `LpVec3`.

String/count limits are embedded in the type text, e.g. `String (32767)`.

## Licensing

The wiki pages used are licensed under **CC BY-SA 3.0**. This technical data is
included for server implementation; attribution and license compatibility are
preserved per the wiki's reuse notice.
