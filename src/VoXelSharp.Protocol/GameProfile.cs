namespace VoXelSharp.Protocol;

/// <summary>
/// A Minecraft player profile (Game Profile), sent in Login Success.
/// Wire format per the Minecraft Wiki "Data types" article (Game Profile section):
///   UUID (2 longs: most significant bits first),
///   Username (String 16),
///   Properties (Prefixed Array (16) of {Name: String 64, Value: String 32767}),
///   Signature (Prefixed Optional String 1024).
/// </summary>
public sealed record GameProfile(
    long PlayerUuidMsb,
    long PlayerUuidLsb,
    string Username,
    ProfileProperty[] Properties,
    string? Signature)
{
    /// <summary>Maximum username length in UTF-16 code units.</summary>
    public const int MaxUsernameLength = 16;

    /// <summary>Maximum number of property entries.</summary>
    public const int MaxPropertiesCount = 16;

    /// <summary>Maximum property name length.</summary>
    public const int MaxPropertyNameLength = 64;

    /// <summary>Maximum property value length.</summary>
    public const int MaxPropertyValueLength = 32767;

    /// <summary>Maximum signature length.</summary>
    public const int MaxSignatureLength = 1024;
}

/// <summary>A single key-value property in a <see cref="GameProfile"/>.</summary>
public sealed record ProfileProperty(string Name, string Value);

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

        // Prefixed Array (16) of {Name: String 64, Value: String 32767}
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
            properties[i] = new ProfileProperty(propName, propValue);
        }

        // Prefixed Optional String (1024)
        string? signature = null;
        bool hasSignature = reader.ReadBoolean();
        if (hasSignature)
        {
            signature = reader.ReadString(GameProfile.MaxSignatureLength);
        }

        return new GameProfile(msb, lsb, username, properties, signature);
    }

    /// <summary>Writes <paramref name="profile"/> to <paramref name="writer"/>.</summary>
    public static void Encode(GameProfile profile, PacketWriter writer)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLong(profile.PlayerUuidMsb);
        writer.WriteLong(profile.PlayerUuidLsb);
        writer.WriteString(profile.Username, GameProfile.MaxUsernameLength);

        // Prefixed Array (16) of {Name: String 64, Value: String 32767}
        writer.WriteVarInt(profile.Properties.Length);
        for (int i = 0; i < profile.Properties.Length; i++)
        {
            writer.WriteString(profile.Properties[i].Name, GameProfile.MaxPropertyNameLength);
            writer.WriteString(profile.Properties[i].Value, GameProfile.MaxPropertyValueLength);
        }

        // Prefixed Optional String (1024)
        writer.WriteBoolean(profile.Signature is not null);
        if (profile.Signature is not null)
        {
            writer.WriteString(profile.Signature, GameProfile.MaxSignatureLength);
        }
    }
}
