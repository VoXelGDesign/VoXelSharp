using VoXelSharp.Protocol;

namespace VoXelSharp.Tests;

public class PacketDirectionTests
{
    [Fact]
    public void DirectionsAreUnique()
    {
        PacketDirection[] directions = Enum.GetValues<PacketDirection>();

        Assert.Equal(directions.Length, directions.Distinct().Count());
    }

    [Fact]
    public void DirectionsContainExactlyTheDocumentedSet()
    {
        PacketDirection[] expected =
        {
            PacketDirection.Clientbound,
            PacketDirection.Serverbound,
        };

        PacketDirection[] actual = Enum.GetValues<PacketDirection>();

        Assert.Equal(expected, actual);
    }
}
