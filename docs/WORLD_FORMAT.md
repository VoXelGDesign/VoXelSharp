# World save format (`world.bin`)

M6 persistence. The server persists the mutable world to a single binary file named `world.bin`
inside the configured world directory (`data/world` by default; Docker volume
`./data/world:/app/data/world`).

Only the blocks that differ from the deterministic flat-world seed terrain are stored (see
`FlatWorld.ExportChanges` / `FlatWorld.ApplyChanges`). Pristine chunks are omitted and regenerate
for free on load, so the file stays tiny regardless of how much terrain players have explored.

## Format

All integers are big-endian (`PacketWriter`/`PacketReader`). File layout, format version 1:

| Field         | Type   | Notes                                   |
|---------------|--------|-----------------------------------------|
| magic         | 4 bytes| ASCII `VXWD`                            |
| version       | int32  | 1                                       |
| column count  | int32  | number of touched chunk columns         |
| per column:   |        |                                         |
| chunk x       | int32  | chunk column X                          |
| chunk z       | int32  | chunk column Z                          |
| change count  | int32  | changed blocks in this column           |
| per change:   |        |                                         |
| world x       | int32  |                                          |
| world y       | int32  | must be within `[MinY, MinY + 384)`     |
| world z       | int32  |                                          |
| block state   | int32  | global block state ID (>= 0)            |

## Validation

`WorldFileStore.Load` throws `InvalidDataException` on any corruption:

- bad magic
- unsupported version
- negative counts
- coordinates outside the world bounds
- negative block state IDs
- truncated data or trailing bytes

`MinecraftServer` catches these on startup, logs a warning, and boots with a fresh world rather
than failing.

## Atomic writes

Saves write to `world.bin.tmp` first, then move it over `world.bin`, so a crash mid-write never
leaves a truncated world file in place.

## Lifecycle

- `Start()`: the save is loaded and applied before the listener accepts connections.
- `StopAsync()`: after the tick loop stops, the world diff is flushed back to disk (graceful
  shutdown; the compose `stop_grace_period: 30s` covers this).