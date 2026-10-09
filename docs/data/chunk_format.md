# Chunk Wire Format (protocol 777 / 26.3)

Protocol facts for chunk serialization, complementing `packets.json` (packet-level fields) and
`blocks.json` (global block state IDs). Facts captured from the Minecraft Wiki (CC BY-SA 3.0)
on 2026-10-09:

- Java Edition protocol/Chunk format
  <https://minecraft.wiki/w/Java_Edition_protocol/Chunk_format>
- Java Edition protocol/Packets (state: 26.3 / protocol 777), "Chunk Data and Update Light"
  and "Light Data" sections
  <https://minecraft.wiki/w/Java_Edition_protocol/Packets>
- Java Edition protocol/Data types (Light Data)
  <https://minecraft.wiki/w/Java_Edition_protocol/Data_types>

Cross-checked against the Mojang data-generator packet report
(`vanilla/generated/reports/packets.json`, generated 2026-10-09): the 26.3 clientbound packet is
`minecraft:level_chunk_with_light` with protocol_id 46 (packets.json field "officialName":
`level_chunk_with_light`, packetId 46).

## Chunk Data and Update Light (clientbound, Play, ID 46)

The chunk must be within the view area previously specified with Set Center Chunk.

| Field          | Type                          | Notes                                                        |
|----------------|-------------------------------|--------------------------------------------------------------|
| Chunk X        | Int                           | Block coordinate / 16, rounded down.                          |
| Chunk Z        | Int                           | Block coordinate / 16, rounded down.                          |
| Heightmaps     | Prefixed Array of Heightmap   | See Heightmap structure below.                                |
| Data           | Prefixed Array of Byte        | VarInt byte length, then the chunk data structure below.      |
| Block Entities | Prefixed Array (see below)    | Packed XZ: Unsigned Byte; Y: Short; Type: VarInt; Data: NBT.  |
| Light          | Light Data                    | See Light Data section.                                       |

Block Entity entry: `Packed XZ` encodes `((blockX & 15) << 4) | (blockZ & 15)`;
decode `x = packed_xz >> 4, z = packed_xz & 15`. `Y` is relative to the world; `Type` is a
VarInt block entity type; `Data` is NBT without X/Y/Z.

Note: it is not necessary to send all block entities in this packet; they may also be sent
later with Block Entity Data.

## Data structure (chunk column)

The `Data` byte payload contains an **array of Chunk Sections that is NOT length-prefixed**;
the element count is derived from the dimension's height (from the `minecraft:dimension_type`
registry entry). Sections are sent **bottom-to-top**. The vanilla overworld is 384 blocks tall
= 24 sections (16 high each).

## Chunk Section structure

| Field         | Type             | Notes                                                          |
|---------------|------------------|----------------------------------------------------------------|
| Block count   | Short            | Non-air blocks (`air`, `cave_air`, `void_air` excluded).       |
| Fluid count   | Short            | Fluids (waterlogged blocks, water/lava with any block state).  |
| Block states  | Paletted Container | 4096 entries (16x16x16), ordered x fastest, then z, then y.  |
| Biomes        | Paletted Container | 64 entries (4x4x4).                                          |

## Paletted Container structure

| Field          | Type       | Notes                                                    |
|----------------|------------|----------------------------------------------------------|
| Bits Per Entry | Unsigned Byte | Determines palette format.                            |
| Palette        | Varies     | See below.                                               |
| Data Array     | Array of Long | See Data Array format below.                           |

### Palette formats (selected by Bits Per Entry)

| BPE (blocks) | BPE (biomes) | Format       | Palette field                              |
|--------------|--------------|--------------|--------------------------------------------|
| 0            | 0            | Single valued | VarInt global ID (data array is empty).    |
| 4-8          | 1-3          | Indirect     | Prefixed Array of VarInt (local palette of global IDs). |
| 15           | 7            | Direct       | No fields (global IDs stored inline).      |

- Single valued: Data Array is empty; all entries equal the palette's single global ID.
- Indirect: entries in the Data Array are indices into the local palette.
- Direct: entries in the Data Array are global palette IDs directly.
- Values not listed should not be used (results are undefined). The client computes the Direct
  BPE at runtime from the global palette sizes, so Direct is only used when the section exceeds
  the indirect maximum.
- A section holds at most 4096 unique block state IDs and 64 unique biome IDs.

## Data Array format

- The array **length is not sent** (as of 1.21.5); it is derived from Bits Per Entry and the
  entry count:
  - `entries_per_long = 64 / bits_per_entry`
  - `number_of_longs = ceil(entries / entries_per_long)`
- Entries are tightly packed with the **first entry in the least significant bits** of a long.
  An entry **never spans two longs**; the remaining bits of each long are padding (MSB side).
- Longs are transmitted **big-endian**.
- Bit index math: `long_index = entry_index / entries_per_long`;
  `bit_index = (entry_index % entries_per_long) * bits_per_entry`.
- Entry order: x fastest, then z, then y.

## Heightmap structure (as of 1.21.5 / 26.3)

| Field | Type                     | Notes                                                            |
|-------|--------------------------|------------------------------------------------------------------|
| Type  | VarInt Enum              | 1 = WORLD_SURFACE, 4 = MOTION_BLOCKING, 5 = MOTION_BLOCKING_NO_LEAVES. |
| Data  | Prefixed Array of Long   | Packed as a Data Array (above), 256 entries, x fastest.          |

- Bits Per Entry = `ceil(log2(world_height + 1))`; the minimum world height is added as an
  offset to obtain the actual Y coordinate.
- No heightmap is strictly required; missing heightmaps default to minimum heights on the client.

## Light Data

Used by both Chunk Data and Update Light and Update Light.

| Field                     | Type                                                    |
|---------------------------|---------------------------------------------------------|
| Sky Light Mask            | BitSet (1 bit per section, world height + 2)            |
| Block Light Mask          | BitSet                                                  |
| Empty Sky Light Mask      | BitSet                                                  |
| Empty Block Light Mask    | BitSet                                                  |
| Sky Light arrays          | Prefixed Array of Prefixed Array (2048) of Byte          |
| Block Light arrays        | Prefixed Array of Prefixed Array (2048) of Byte          |

Each inner light array is always 2048 bytes (half byte per light value). The least significant
bit corresponds to the section 16 blocks below the world minimum; the most significant bit
covers 16 blocks above the maximum.
