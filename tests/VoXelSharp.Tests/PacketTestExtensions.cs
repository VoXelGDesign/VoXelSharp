using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.Tests;

/// <summary>
/// Fluent helpers that condense the common encode → decode assertion pattern in packet tests:
/// <c>registry.DecodeAs&lt;TPacket&gt;(state, direction, codec.Encode(packet))</c>.
/// </summary>
internal static class PacketTestExtensions
{
    /// <summary>Encodes <paramref name="packet"/> with <paramref name="codec"/> into a full payload.</summary>
    public static byte[] Encode<TPacket>(this PacketCodec<TPacket> codec, TPacket packet)
        where TPacket : IPacket
    {
        var writer = new PacketWriter();
        codec.Encode(packet, writer);
        return writer.ToArray();
    }

    /// <summary>
    /// Decodes <paramref name="payload"/> and asserts the result is exactly <typeparamref name="TPacket"/>.
    /// </summary>
    public static TPacket DecodeAs<TPacket>(
        this PacketRegistry registry, ConnectionState state, PacketDirection direction, byte[] payload)
        where TPacket : IPacket
    {
        IPacket decoded = registry.Decode(state, direction, payload);
        return Assert.IsType<TPacket>(decoded);
    }

    /// <summary>Decodes <paramref name="payload"/> via the default registry and asserts the type.</summary>
    public static TPacket DecodeAs<TPacket>(ConnectionState state, PacketDirection direction, byte[] payload)
        where TPacket : IPacket
        => PacketRegistry.CreateDefault().DecodeAs<TPacket>(state, direction, payload);

    /// <summary>Decodes a codec-encoded payload via the default registry and asserts the type.</summary>
    public static TPacket DecodeAs<TPacket>(
        this PacketCodec<TPacket> codec, ConnectionState state, PacketDirection direction, byte[] payload)
        where TPacket : IPacket
        => PacketRegistry.CreateDefault().DecodeAs<TPacket>(state, direction, payload);

    /// <summary>Asserts that a payload fails to decode (malformed input closes the connection).</summary>
    public static void AssertInvalidData(this byte[] payload, ConnectionState state, PacketDirection direction)
        => Assert.Throws<InvalidDataException>(
            () => PacketRegistry.CreateDefault().Decode(state, direction, payload));
}
