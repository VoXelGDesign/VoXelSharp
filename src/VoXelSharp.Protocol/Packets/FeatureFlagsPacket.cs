namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Feature Flags (configuration/clientbound, packet ID 13, official name "update_enabled_features").
/// Tells the client which features (by Identifier) are enabled.
/// Field order per docs/data/packets.json: Feature Flags (Prefixed Array of Identifier).
/// </summary>
public sealed class FeatureFlagsPacket : IPacket
{
    /// <summary>Maximum length for a feature flag Identifier (String 32767).</summary>
    public const int MaxIdentifierLength = 32767;

    public FeatureFlagsPacket(string[] features)
    {
        ArgumentNullException.ThrowIfNull(features);
        Features = features;
    }

    /// <summary>The list of enabled feature identifiers (e.g. "minecraft:...").</summary>
    public string[] Features { get; }
}

/// <summary>Codec for <see cref="FeatureFlagsPacket"/>.</summary>
public sealed class FeatureFlagsCodec : PacketCodec<FeatureFlagsPacket>
{
    /// <summary>Singleton access.</summary>
    public static FeatureFlagsCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Configuration;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 13;

    /// <inheritdoc />
    protected override void EncodeBody(FeatureFlagsPacket packet, PacketWriter writer)
    {
        writer.WriteVarInt(packet.Features.Length);
        foreach (string feature in packet.Features)
        {
            writer.WriteString(feature, FeatureFlagsPacket.MaxIdentifierLength);
        }
    }

    /// <inheritdoc />
    protected override FeatureFlagsPacket DecodeBody(ref PacketReader reader)
    {
        int count = reader.ReadVarInt();
        if (count < 0)
        {
            throw new InvalidDataException($"Negative feature flag count ({count}).");
        }

        var features = new string[count];
        for (int i = 0; i < count; i++)
        {
            features[i] = reader.ReadString(FeatureFlagsPacket.MaxIdentifierLength);
        }

        return new FeatureFlagsPacket(features);
    }
}
