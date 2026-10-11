using System.Text;
using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;
using VoXelSharp.World;
using Xunit;

namespace VoXelSharp.Tests;

/// <summary>
/// Tests for M6 world persistence: <see cref="FlatWorld.ExportChanges"/> /
/// <see cref="FlatWorld.ApplyChanges"/> and the <see cref="WorldFileStore"/> binary file format.
/// </summary>
public class WorldFileStoreTests
{
    [Fact]
    public void FreshWorldExportsNoChanges()
    {
        var world = new FlatWorld();

        // Reading a chunk only seeds the column; nothing differs from the generated terrain.
        world.GetChunk(0, 0);

        Assert.Empty(world.ExportChanges());
    }

    [Fact]
    public void MutationExportsExactlyTheChangedBlocks()
    {
        var world = new FlatWorld();

        world.SetBlock(0, -60, 0, BlockStateIds.Stone);
        world.SetBlock(7, -60, 3, BlockStateIds.Dirt);

        var changes = world.ExportChanges();
        Assert.Single(changes);
        Assert.Equal(0, changes[0].ChunkX);
        Assert.Equal(0, changes[0].ChunkZ);
        Assert.Equal(2, changes[0].Changes.Length);
        Assert.Contains(changes[0].Changes, change =>
            change.X == 0 && change.Y == -60 && change.Z == 0 && change.GlobalBlockStateId == BlockStateIds.Stone);
        Assert.Contains(changes[0].Changes, change =>
            change.X == 7 && change.Y == -60 && change.Z == 3 && change.GlobalBlockStateId == BlockStateIds.Dirt);
    }

    [Fact]
    public void MutationExportUsesWorldCoordinatesForNegativeChunks()
    {
        var world = new FlatWorld();

        // x=-1, z=-1 sit in chunk column (-1,-1) at local (15,15); y=-61 is the grass surface.
        world.SetBlock(-1, -61, -1, BlockStateIds.Stone);

        IReadOnlyList<WorldColumnChanges> changes = world.ExportChanges();
        Assert.Single(changes);
        Assert.Equal(-1, changes[0].ChunkX);
        Assert.Equal(-1, changes[0].ChunkZ);
        Assert.Single(changes[0].Changes);
        Assert.Equal(-1, changes[0].Changes[0].X);
        Assert.Equal(-61, changes[0].Changes[0].Y);
        Assert.Equal(-1, changes[0].Changes[0].Z);
    }

    [Fact]
    public void ApplyChangesRebuildsTouchedColumns()
    {
        var source = new FlatWorld();
        source.SetBlock(2, -60, 4, BlockStateIds.Stone);
        source.SetBlock(0, -59, 0, BlockStateIds.Dirt);

        var restored = new FlatWorld();
        restored.ApplyChanges(source.ExportChanges());

        Assert.Equal(BlockStateIds.Stone, restored.GetBlock(2, -60, 4));
        Assert.Equal(BlockStateIds.Dirt, restored.GetBlock(0, -59, 0));
        // Untouched terrain is regenerated, not lost.
        Assert.Equal(BlockStateIds.GrassBlock, restored.GetBlock(0, -61, 0));
        Assert.Equal(BlockStateIds.Bedrock, restored.GetBlock(5, -64, 5));
        Assert.Equal(BlockStateIds.Air, restored.GetBlock(1, 100, 1));
    }

    [Fact]
    public void ApplyChangesThenExportIsIdempotent()
    {
        var source = new FlatWorld();
        source.SetBlock(3, -60, 3, BlockStateIds.Stone);

        var restored = new FlatWorld();
        restored.ApplyChanges(source.ExportChanges());

        AssertChangesEqual(source.ExportChanges(), restored.ExportChanges());
    }

    [Fact]
    public void SaveThenLoadRoundTripsTheChanges()
    {
        using var temp = new TempWorldDirectory();

        var world = new FlatWorld();
        world.SetBlock(0, -60, 0, BlockStateIds.Stone);
        world.SetBlock(10, -61, -5, BlockStateIds.Dirt);

        WorldFileStore.Save(temp.DirectoryPath, world.ExportChanges());
        Assert.True(File.Exists(Path.Combine(temp.DirectoryPath, WorldFileStore.FileName)));

        var loaded = WorldFileStore.Load(temp.DirectoryPath);
        AssertChangesEqual(world.ExportChanges(), loaded);
    }

    [Fact]
    public void SaveLeavesNoTempFileBehind()
    {
        using var temp = new TempWorldDirectory();

        var world = new FlatWorld();
        world.SetBlock(0, -60, 0, BlockStateIds.Stone);
        WorldFileStore.Save(temp.DirectoryPath, world.ExportChanges());

        // The exact save file must load without error and no .tmp sibling remains.
        var loaded = WorldFileStore.Load(temp.DirectoryPath);
        AssertChangesEqual(world.ExportChanges(), loaded);
        Assert.False(File.Exists(Path.Combine(temp.DirectoryPath, WorldFileStore.FileName + ".tmp")));
    }

    [Fact]
    public void LoadWithoutSaveFileReturnsEmpty()
    {
        using var temp = new TempWorldDirectory();

        Assert.Empty(WorldFileStore.Load(temp.DirectoryPath));
    }

    [Fact]
    public void SaveOverwritesThePreviousWorld()
    {
        using var temp = new TempWorldDirectory();

        var first = new FlatWorld();
        first.SetBlock(0, -60, 0, BlockStateIds.Stone);
        WorldFileStore.Save(temp.DirectoryPath, first.ExportChanges());

        var second = new FlatWorld();
        second.SetBlock(4, -59, 4, BlockStateIds.Dirt);
        WorldFileStore.Save(temp.DirectoryPath, second.ExportChanges());

        var loaded = WorldFileStore.Load(temp.DirectoryPath);
        AssertChangesEqual(second.ExportChanges(), loaded);
    }

    [Fact]
    public void LoadRejectsBadMagic()
    {
        using var temp = new TempWorldDirectory();

        File.WriteAllBytes(
            Path.Combine(temp.DirectoryPath, WorldFileStore.FileName),
            Encoding.UTF8.GetBytes("THIS IS NOT A WORLD FILE!"));
        Assert.Throws<InvalidDataException>(() => WorldFileStore.Load(temp.DirectoryPath));
    }

    [Fact]
    public void LoadRejectsTruncatedFile()
    {
        using var temp = new TempWorldDirectory();

        var world = new FlatWorld();
        world.SetBlock(0, -60, 0, BlockStateIds.Stone);
        WorldFileStore.Save(temp.DirectoryPath, world.ExportChanges());

        byte[] payload = File.ReadAllBytes(Path.Combine(temp.DirectoryPath, WorldFileStore.FileName));
        int trimmedLength = payload.Length - 3;
        File.WriteAllBytes(
            Path.Combine(temp.DirectoryPath, WorldFileStore.FileName),
            payload[..trimmedLength].ToArray());

        Assert.Throws<InvalidDataException>(() => WorldFileStore.Load(temp.DirectoryPath));
    }

    [Fact]
    public void LoadRejectsOutOfWorldCoordinate()
    {
        using var temp = new TempWorldDirectory();

        var writer = new PacketWriter();
        WriteMagicAndVersion(writer);
        writer.WriteInt(1); // one column
        writer.WriteInt(0); // chunk x
        writer.WriteInt(0); // chunk z
        writer.WriteInt(1); // one change
        writer.WriteInt(0); // x
        writer.WriteInt(FlatWorldGenerator.MinY + FlatWorldGenerator.WorldHeight); // y out of bounds
        writer.WriteInt(0); // z
        writer.WriteInt(BlockStateIds.Stone);
        File.WriteAllBytes(Path.Combine(temp.DirectoryPath, WorldFileStore.FileName), writer.ToArray());

        Assert.Throws<InvalidDataException>(() => WorldFileStore.Load(temp.DirectoryPath));
    }

    [Fact]
    public void LoadRejectsNegativeBlockStateId()
    {
        using var temp = new TempWorldDirectory();

        var writer = new PacketWriter();
        WriteMagicAndVersion(writer);
        writer.WriteInt(1); // one column
        writer.WriteInt(0); // chunk x
        writer.WriteInt(0); // chunk z
        writer.WriteInt(1); // one change
        writer.WriteInt(0); // x
        writer.WriteInt(-60); // y
        writer.WriteInt(0); // z
        writer.WriteInt(-1); // negative state id
        File.WriteAllBytes(Path.Combine(temp.DirectoryPath, WorldFileStore.FileName), writer.ToArray());

        Assert.Throws<InvalidDataException>(() => WorldFileStore.Load(temp.DirectoryPath));
    }

    private static void AssertChangesEqual(
        IReadOnlyList<WorldColumnChanges> expected, IReadOnlyList<WorldColumnChanges> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].ChunkX, actual[i].ChunkX);
            Assert.Equal(expected[i].ChunkZ, actual[i].ChunkZ);
            // WorldBlockChange is a record of scalars, so xUnit compares the arrays element-wise.
            Assert.Equal(expected[i].Changes, actual[i].Changes);
        }
    }

    private static void WriteMagicAndVersion(PacketWriter writer)
    {
        writer.WriteBytes([0x56, 0x58, 0x57, 0x44]); // "VXWD"
        writer.WriteInt(WorldFileStore.FormatVersion);
    }

    /// <summary>
    /// A uniquely named temporary directory that is deleted (recursively) when the test ends.
    /// </summary>
    private sealed class TempWorldDirectory : IDisposable
    {
        public readonly string DirectoryPath;

        public TempWorldDirectory() => DirectoryPath = Directory.CreateTempSubdirectory("voxelsharp-tests").FullName;

        public void Dispose() => Directory.Delete(DirectoryPath, recursive: true);
    }
}
