using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.World;

/// <summary>
/// Saves and loads the mutable world diff to a single binary file (M6 persistence).
///
/// Only the blocks that differ from the <see cref="FlatWorldGenerator"/> seed terrain are
/// stored (see <see cref="FlatWorld.ExportChanges"/>), so the file is tiny and regenerates the
/// world exactly: on load every touched column is rebuilt from the seed and the stored changes
/// are re-applied.
///
/// File format (big-endian, packed with <see cref="PacketWriter"/>):
/// <pre>
/// magic                  "VXWD"  (4 bytes)
/// version                int32   = 1
/// column count           int32
///   per column:
///     chunk x            int32
///     chunk z            int32
///     change count       int32
///       per change:
///         world x        int32
///         world y        int32
///         world z        int32
///         global state   int32
/// </pre>
///
/// Writes are atomic: the file is fully written to a <c>.tmp</c> sibling first, then moved over
/// the real path, so a crash mid-write never leaves a truncated world file. Load validates the
/// whole structure and throws <see cref="InvalidDataException"/> on any corruption (bad magic,
/// unsupported version, negative counts, out-of-world coordinates, trailing bytes).
/// </summary>
public static class WorldFileStore
{
    /// <summary>Name of the world save file inside the world directory.</summary>
    public const string FileName = "world.bin";

    /// <summary>Current file format version.</summary>
    public const int FormatVersion = 1;

    private static readonly byte[] _magic = { (byte)'V', (byte)'X', (byte)'W', (byte)'D' };

    /// <summary>
    /// Saves <paramref name="changes"/> to <c>directory/world.bin</c>, creating the directory
    /// when missing. Overwrites any previous save.
    /// </summary>
    /// <param name="directory">Directory that holds the world save file.</param>
    /// <param name="changes">World changes to persist (may be empty).</param>
    /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
    public static void Save(string directory, IReadOnlyList<WorldColumnChanges> changes)
    {
        Directory.CreateDirectory(directory);

        byte[] payload = Encode(changes);
        string path = Path.Combine(directory, FileName);
        string tempPath = path + ".tmp";
        File.WriteAllBytes(tempPath, payload);
        File.Move(tempPath, path, overwrite: true);
    }

    /// <summary>
    /// Loads the world changes from <c>directory/world.bin</c>.
    /// </summary>
    /// <returns>The stored changes; an empty list when no save file exists.</returns>
    /// <exception cref="InvalidDataException">Thrown when the file exists but is corrupt.</exception>
    public static IReadOnlyList<WorldColumnChanges> Load(string directory)
    {
        string path = Path.Combine(directory, FileName);
        if (!File.Exists(path))
        {
            return [];
        }

        byte[] data = File.ReadAllBytes(path);
        var reader = new PacketReader(data);
        return Decode(reader);
    }

    private static byte[] Encode(IReadOnlyList<WorldColumnChanges> changes)
    {
        var writer = new PacketWriter();
        writer.WriteBytes(_magic);
        writer.WriteInt(FormatVersion);
        writer.WriteInt(changes.Count);

        foreach (WorldColumnChanges column in changes)
        {
            writer.WriteInt(column.ChunkX);
            writer.WriteInt(column.ChunkZ);
            writer.WriteInt(column.Changes.Length);

            foreach (WorldBlockChange change in column.Changes)
            {
                writer.WriteInt(change.X);
                writer.WriteInt(change.Y);
                writer.WriteInt(change.Z);
                writer.WriteInt(change.GlobalBlockStateId);
            }
        }

        return writer.ToArray();
    }

    private static List<WorldColumnChanges> Decode(PacketReader reader)
    {
        for (int i = 0; i < _magic.Length; i++)
        {
            if (reader.ReadByte() != _magic[i])
            {
                throw new InvalidDataException("Not a VoXelSharp world save file (bad magic).");
            }
        }

        int version = reader.ReadInt();
        if (version != FormatVersion)
        {
            throw new InvalidDataException($"Unsupported world save version {version} (expected {FormatVersion}).");
        }

        int columnCount = reader.ReadInt();
        if (columnCount < 0)
        {
            throw new InvalidDataException($"Negative column count ({columnCount}).");
        }

        if (columnCount > reader.Remaining / 12)
        {
            throw new InvalidDataException($"Column count {columnCount} exceeds the remaining file size.");
        }

        var columns = new List<WorldColumnChanges>();
        for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
        {
            int chunkX = reader.ReadInt();
            int chunkZ = reader.ReadInt();
            int changeCount = reader.ReadInt();
            if (changeCount < 0)
            {
                throw new InvalidDataException($"Negative change count ({changeCount}).");
            }

            if (changeCount > reader.Remaining / 16)
            {
                throw new InvalidDataException($"Change count {changeCount} exceeds the remaining file size.");
            }

            var changes = new WorldBlockChange[changeCount];
            for (int changeIndex = 0; changeIndex < changeCount; changeIndex++)
            {
                int x = reader.ReadInt();
                int y = reader.ReadInt();
                int z = reader.ReadInt();
                int stateId = reader.ReadInt();
                ValidateCoordinate(x, y, z, stateId);
                changes[changeIndex] = new WorldBlockChange(x, y, z, stateId);
            }

            if (changes.Length > 0)
            {
                columns.Add(new WorldColumnChanges(chunkX, chunkZ, changes));
            }
        }

        if (!reader.IsEmpty)
        {
            throw new InvalidDataException($"{reader.Remaining} trailing byte(s) after world changes.");
        }

        return columns;
    }

    private static void ValidateCoordinate(int x, int y, int z, int stateId)
    {
        if (y < FlatWorldGenerator.MinY || y >= FlatWorldGenerator.MinY + FlatWorldGenerator.WorldHeight)
        {
            throw new InvalidDataException($"Block y={y} outside the world bounds.");
        }

        if (stateId < 0)
        {
            throw new InvalidDataException($"Negative block state ID ({stateId}).");
        }
    }
}
