using VoXelSharp.Protocol;

namespace VoXelSharp.Tests;

public class BlockPositionTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 2, 3)]
    [InlineData(-1, -2, -3)]
    [InlineData(18357644, 831, -20882616)]
    [InlineData(33554431, 2047, 33554431)]
    [InlineData(-33554432, -2048, -33554432)]
    public void PackAndUnpackRoundTrips(int x, int y, int z)
    {
        var position = new BlockPosition(x, y, z);

        BlockPosition decoded = BlockPosition.FromPacked(position.ToPacked());

        Assert.Equal(x, decoded.X);
        Assert.Equal(y, decoded.Y);
        Assert.Equal(z, decoded.Z);
    }

    [Fact]
    public void KnownPackingMatchesWikiExample()
    {
        // Wiki example (docs/data/wire_types.md / Data types "Position"):
        // x=18357644, z=-20882616, y=831 encoded as 64 bits:
        // 01000110 00000111 01100011 00101100 00010101 10110100 10000011 00111111
        var position = new BlockPosition(18357644, 831, -20882616);

        long packed = position.ToPacked();

        Assert.Equal(0x4607632C15B4833F, unchecked((long)0x4607632C15B4833FUL));
        Assert.Equal(0x4607632C15B4833FUL, (ulong)packed);
    }

    [Fact]
    public void WriterRoundTripsPosition()
    {
        var writer = new PacketWriter();
        writer.WritePosition(new BlockPosition(100, 200, 300));

        var reader = new PacketReader(writer.ToArray());
        BlockPosition decoded = reader.ReadPosition();

        Assert.Equal(100, decoded.X);
        Assert.Equal(200, decoded.Y);
        Assert.Equal(300, decoded.Z);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void WriterRejectsOutOfRangeCoordinates()
    {
        var writer = new PacketWriter();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => writer.WritePosition(new BlockPosition(33_554_432, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => writer.WritePosition(new BlockPosition(0, -2_049, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => writer.WritePosition(new BlockPosition(0, 0, 33_554_432)));
    }

    [Fact]
    public void WriterAcceptsBoundaryCoordinates()
    {
        var writer = new PacketWriter();

        // Widest valid values: x=33,554,431; y=2,047; z=-33,554,432.
        writer.WritePosition(new BlockPosition(33_554_431, 2_047, -33_554_432));

        var reader = new PacketReader(writer.ToArray());
        BlockPosition decoded = reader.ReadPosition();

        Assert.Equal(33_554_431, decoded.X);
        Assert.Equal(2_047, decoded.Y);
        Assert.Equal(-33_554_432, decoded.Z);
    }

    [Fact]
    public void ReaderTruncatedPositionThrows()
    {
        // Only 5 of the 8 required bytes.
        bool caught = false;
        try
        {
            var reader = new PacketReader(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00 });
            reader.ReadPosition();
        }
        catch (InvalidDataException)
        {
            caught = true;
        }

        Assert.True(caught);
    }
}
