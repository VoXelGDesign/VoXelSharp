namespace VoXelSharp.Protocol;

/// <summary>
/// A Minecraft player profile (Game Profile), sent in Login Success.
/// Wire format per the Minecraft Wiki "Data types" article (Game Profile section):
///   UUID (2 longs: most significant bits first),
///   Username (String 16),
///   Properties (Prefixed Array (16) of {Name: String 64, Value: String 32767,
///     Signature: Prefixed Optional String 1024}).
/// The signature is a per-property field; there is no profile-level signature.
/// Source: docs/data/_raw/_packets.raw.html (wiki capture, 2026-10-09).
/// </summary>
public sealed record GameProfile(
    long PlayerUuidMsb,
    long PlayerUuidLsb,
    string Username,
    ProfileProperty[] Properties)
{
    /// <summary>Maximum username length in UTF-16 code units.</summary>
    public const int MaxUsernameLength = 16;

    /// <summary>Maximum number of property entries.</summary>
    public const int MaxPropertiesCount = 16;

    /// <summary>Maximum property name length.</summary>
    public const int MaxPropertyNameLength = 64;

    /// <summary>Maximum property value length.</summary>
    public const int MaxPropertyValueLength = 32767;

    /// <summary>Maximum per-property signature length.</summary>
    public const int MaxSignatureLength = 1024;
}

/// <summary>
/// A single key-value property in a <see cref="GameProfile"/> or the Player Info Update
/// "Add Player" action. The signature is optional and encoded as a Prefixed Optional String.
/// </summary>
public sealed record ProfileProperty(string Name, string Value, string? Signature);

/// <summary>
/// Reads and writes a <see cref="GameProfile"/> from/to a packet reader/writer.
/// The profile is not a packet itself; it is embedded in the Login Success packet body.
/// </summary>
public static class GameProfileIO
{
    /// <summary>Reads a GameProfile from <paramref name="reader"/>.</summary>
    /// <exception cref="InvalidDataException">Thrown on truncated/invalid data.</exception>
    public static GameProfile Decode(ref PacketReader reader)
    {
        long msb = reader.ReadLong();
        long lsb = reader.ReadLong();
        string username = reader.ReadString(GameProfile.MaxUsernameLength);

        ProfileProperty[] properties = ReadProperties(ref reader);

        return new GameProfile(msb, lsb, username, properties);
    }

    /// <summary>Reads a Prefixed Array (16) of profile properties, each with an optional signature.</summary>
    public static ProfileProperty[] ReadProperties(ref PacketReader reader)
    {
        // Prefixed Array (16) of {Name: String 64, Value: String 32767, Signature: Prefixed Optional String 1024}
        int propertyCount = reader.ReadVarInt();
        if (propertyCount < 0 || propertyCount > GameProfile.MaxPropertiesCount)
        {
            throw new InvalidDataException(
                $"Property count {propertyCount} is out of range (0..{GameProfile.MaxPropertiesCount}).");
        }

        var properties = new ProfileProperty[propertyCount];
        for (int i = 0; i < propertyCount; i++)
        {
            string propName = reader.ReadString(GameProfile.MaxPropertyNameLength);
            string propValue = reader.ReadString(GameProfile.MaxPropertyValueLength);

            string? signature = null;
            if (reader.ReadBoolean())
            {
                signature = reader.ReadString(GameProfile.MaxSignatureLength);
            }

            properties[i] = new ProfileProperty(propName, propValue, signature);
        }

        return properties;
    }

    /// <summary>Writes <paramref name="profile"/> to <paramref name="writer"/>.</summary>
    public static void Encode(GameProfile profile, PacketWriter writer)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLong(profile.PlayerUuidMsb);
        writer.WriteLong(profile.PlayerUuidLsb);
        writer.WriteString(profile.Username, GameProfile.MaxUsernameLength);
        WriteProperties(profile.Properties, writer);
    }

    /// <summary>Writes a Prefixed Array (16) of profile properties, each with an optional signature.</summary>
    public static void WriteProperties(ProfileProperty[] properties, PacketWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (properties.Length > GameProfile.MaxPropertiesCount)
        {
            throw new ArgumentException(
                $"Property count {properties.Length} exceeds the maximum of {GameProfile.MaxPropertiesCount}.",
                nameof(properties));
        }

        writer.WriteVarInt(properties.Length);
        for (int i = 0; i < properties.Length; i++)
        {
            ProfileProperty property = properties[i];
            writer.WriteString(property.Name, GameProfile.MaxPropertyNameLength);
            writer.WriteString(property.Value, GameProfile.MaxPropertyValueLength);

            writer.WriteBoolean(property.Signature is not null);
            if (property.Signature is not null)
            {
                writer.WriteString(property.Signature, GameProfile.MaxSignatureLength);
            }
        }
    }
}
