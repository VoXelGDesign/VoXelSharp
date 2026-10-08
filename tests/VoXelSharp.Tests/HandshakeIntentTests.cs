using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.Tests;

/// <summary>
/// Locks the numeric wire values of <see cref="HandshakeIntent"/> as documented in
/// docs/data/packets.json (handshaking/serverbound/Handshake Intent notes).
/// </summary>
public class HandshakeIntentTests
{
    [Fact]
    public void StatusIsOne()
    {
        Assert.Equal(1, (int)HandshakeIntent.Status);
    }

    [Fact]
    public void LoginIsTwo()
    {
        Assert.Equal(2, (int)HandshakeIntent.Login);
    }

    [Fact]
    public void TransferIsThree()
    {
        Assert.Equal(3, (int)HandshakeIntent.Transfer);
    }
}
