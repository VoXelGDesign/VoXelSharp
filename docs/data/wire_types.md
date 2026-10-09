# Wire Types Reference

Protocol facts for composite wire types used by packet fields. These complement
`packets.json` (which records packet IDs, direction, and field names). Facts here were
captured from the Minecraft Wiki (CC BY-SA 3.0) on 2026-10-09:

- Java Edition protocol/Data types
  <https://minecraft.wiki/w/Java_Edition_protocol/Data_types>
- Minecraft Wiki:Projects/wiki.vg merge/NBT
  <https://minecraft.wiki/w/Minecraft_Wiki:Projects/wiki.vg_merge/NBT>
- Java Edition protocol/Packets (state: 26.3 / protocol 777)
  <https://minecraft.wiki/w/Java_Edition_protocol/Packets>

All multi-byte integers are big-endian except VarInt/VarLong.

## Identifier

- Encoded as a String with a maximum length of 32767 (UTF-16 code units).
- Namespaced location `minecraft:thing`; if the namespace is omitted it defaults to
  `minecraft`. Namespace charset `[a-z0-9._-]`, value charset `[a-z0-9._-/]`.

## Prefixed Array

- Length: VarInt (always present, even when zero), followed by that many elements of type X.

## Prefixed Optional X

- Boolean: true (0x01) if a value of type X is present; then the value if present.
- Represented as "Prefixed Optional" in packet field types.

## NBT (Network NBT, Java Edition)

Used since protocol 764 (1.20.2). The root `TAG_Compound` carries **no name length prefix**
(it is "unnamed"). Nested named tags inside a `TAG_Compound` follow the classic layout.

Tag type IDs:

| ID | Name               | Payload                                                        |
|----|--------------------|----------------------------------------------------------------|
| 0  | TAG_End            | none (terminates a compound)                                   |
| 1  | TAG_Byte           | 1 signed byte                                                  |
| 2  | TAG_Short          | 2 signed bytes (big-endian)                                    |
| 3  | TAG_Int            | 4 signed bytes (big-endian)                                    |
| 4  | TAG_Long           | 8 signed bytes (big-endian)                                    |
| 5  | TAG_Float          | 4 bytes IEEE-754 (big-endian)                                  |
| 6  | TAG_Double         | 8 bytes IEEE-754 (big-endian)                                  |
| 7  | TAG_Byte_Array     | Int length, then signed bytes                                  |
| 8  | TAG_String         | Unsigned Short byte-length, then modified UTF-8                |
| 9  | TAG_List           | Tag type (1 byte) + Int length + elements (no names)           |
| 10 | TAG_Compound       | Named child tags terminated by TAG_End                         |
| 11 | TAG_Int_Array      | Int length, then Int elements                                  |
| 12 | TAG_Long_Array     | Int length, then Long elements                                 |

Layout inside a `TAG_Compound`: for each named tag write `type ID byte`, then the name as an
unsigned-short byte-length prefix followed by the name bytes (modified UTF-8), then the payload.
A `TAG_Compound` ends with a `TAG_End` (0x00) byte. In a `TAG_List` the elements are nameless.

Network NBT root (`Prefixed Optional NBT` in Registry Data): `0x0A` (Compound type byte),
then the compound payload (child tags + TAG_End). There is no name length prefix for the root.

Note: TAG_String uses modified UTF-8; our protocol String type uses standard UTF-8. For tag
names and TAG_String values the length prefix is an unsigned short (2 bytes), distinct from
the protocol String type which uses a VarInt byte length.

## Update Tags — Tag sub-structure

`Tagged Registries` is a Prefixed Array of:
- `Registry`: Identifier (e.g. `minecraft:block`)
- `Tags`: Prefixed Array of Tag

Each **Tag** is:
- `Tag Name`: Identifier (name without the `#`-prefix, e.g. `minecraft:climbable`)
- `Entries`: Prefixed Array of VarInt (numeric IDs of the registry type)

## Registry Data — Entries

`Registry Data` fields:
- `Registry ID`: Identifier (e.g. `minecraft:dimension_type`)
- `Entries`: Prefixed Array of:
  - `Entry ID`: Identifier (e.g. `minecraft:overworld`)
  - `Data`: Prefixed Optional NBT (Network NBT compound). If omitted, sourced from known packs.

## Position

An 8-byte integer packing `x`, `z`, `y`: x as a 26-bit signed integer, z as a 26-bit signed
integer, y as a 12-bit signed integer (big-endian on the wire).

- Valid ranges: x `-33,554,432 .. 33,554,431`; z `-33,554,432 .. 33,554,431`; y `-2048 .. 2047`.
- Encoded `((x & 0x3FFFFFF) << 38) | ((z & 0x3FFFFFF) << 12) | (y & 0xFFF)`.
- Decoded `x = val >> 38; z = val << 26 >> 38; y = val << 52 >> 52` (arithmetic shifts).

Source: Java Edition protocol/Data types (wiki, 2026-10-09).

## Teleport Flags

Flags for Synchronize Player Position and related packets; Int bit mask:

| Bit  | Meaning            |
|------|--------------------|
| 0x01 | Relative X         |
| 0x02 | Relative Y         |
| 0x04 | Relative Z         |
| 0x08 | Relative Yaw       |
| 0x10 | Relative Pitch     |
| 0x20 | Relative Velocity X |
| 0x40 | Relative Velocity Y |
| 0x80 | Relative Velocity Z |
| 0x100 | Rotate velocity according to the change in rotation |

Source: Java Edition protocol/Data types (wiki, 2026-10-09).

## BitSet

A bit set prefixed by its length in longs.

- Length: VarInt — number of longs in the following array. May be 0 (no bits set).
- Data: Array of Long.
- The `i`th bit (0-based) is set when `(Data[i / 64] & (1 << (i % 64))) != 0`.

Distinct from `Fixed BitSet (n)` (an `n`-bit set encoded as `ceil(n / 8)` bytes; not
length-prefixed). Used by Light Data masks and elsewhere.

Source: Java Edition protocol/Data types (wiki, 2026-10-09).
