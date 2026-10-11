namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// A chat message signature: a fixed 256-byte payload (26.3 MessageSignature), only present on
/// the wire when the optional presence flag is set.
/// </summary>
public sealed record ChatMessageSignature(byte[] Bytes)
{
    /// <summary>Fixed signature size in bytes (26.3 MessageSignature.BYTES).</summary>
    public const int Size = 256;
}

/// <summary>
/// LastSeenMessages.Update (26.3): Offset (VarInt), Acknowledged (Fixed BitSet of 20 bits,
/// encoded as ceil(20/8) = 3 little-endian bytes, no length prefix), Checksum (Byte).
/// </summary>
public sealed record LastSeenMessageUpdate(int Offset, byte[] Acknowledged, byte Checksum)
{
    /// <summary>Reads a Fixed BitSet (20) as 3 bytes.</summary>
    public static byte[] ReadAcknowledged(ref PacketReader reader)
    {
        int byteCount = 3; // ceil(20 / 8)
        return reader.ReadBytes(byteCount);
    }

    /// <summary>Writes the Fixed BitSet (20) as exactly 3 little-endian bytes (no length prefix).</summary>
    public static void WriteAcknowledged(PacketWriter writer, byte[] acknowledged)
    {
        // The wire form always carries ceil(20/8) = 3 bytes, padding with zero bits.
        writer.WriteByte(acknowledged.Length > 0 ? acknowledged[0] : (byte)0);
        writer.WriteByte(acknowledged.Length > 1 ? acknowledged[1] : (byte)0);
        writer.WriteByte(acknowledged.Length > 2 ? acknowledged[2] : (byte)0);
    }
}

/// <summary>
/// A signable command argument's name + signature (26.3 ArgumentSignatures.Entry):
/// Name (String 16 UTF-8), Signature (fixed 256 bytes).
/// </summary>
public sealed record CommandArgumentSignature(string Name, ChatMessageSignature Signature);

/// <summary>
/// Serverbound Chat (play, id 9, "chat"). Fields verified from the 26.3 client
/// (ServerboundChatPacket.STREAM_CODEC):
/// Message (String 256 UTF-8), Timestamp (Instant = Long epoch millis), Salt (Long),
/// Signature (Optional: Boolean + 256 bytes), Last Seen Messages Update.
/// </summary>
public sealed class ServerboundChatPacket : IPacket
{
    public ServerboundChatPacket(
        string message,
        long timestampEpochMillis,
        long salt,
        ChatMessageSignature? signature,
        LastSeenMessageUpdate lastSeenMessages)
    {
        Message = message;
        TimestampEpochMillis = timestampEpochMillis;
        Salt = salt;
        Signature = signature;
        LastSeenMessages = lastSeenMessages;
    }

    /// <summary>The chat text (max 256 UTF-8 code units).</summary>
    public string Message { get; }

    /// <summary>Message timestamp in epoch milliseconds.</summary>
    public long TimestampEpochMillis { get; }

    /// <summary>Signature salt.</summary>
    public long Salt { get; }

    /// <summary>The message signature when the optional flag is set.</summary>
    public ChatMessageSignature? Signature { get; }

    /// <summary>Last-seen messages update.</summary>
    public LastSeenMessageUpdate LastSeenMessages { get; }
}

/// <summary>Codec for <see cref="ServerboundChatPacket"/>.</summary>
public sealed class ServerboundChatCodec : PacketCodec<ServerboundChatPacket>
{
    /// <summary>Singleton access.</summary>
    public static ServerboundChatCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 9;

    /// <inheritdoc />
    protected override void EncodeBody(ServerboundChatPacket packet, PacketWriter writer)
    {
        writer.WriteString(packet.Message, 256);
        writer.WriteLong(packet.TimestampEpochMillis);
        writer.WriteLong(packet.Salt);

        writer.WriteBoolean(packet.Signature is not null);
        if (packet.Signature is not null)
        {
            writer.WriteBytes(packet.Signature.Bytes);
        }

        ChatIO.WriteLastSeenUpdate(packet.LastSeenMessages, writer);
    }

    /// <inheritdoc />
    protected override ServerboundChatPacket DecodeBody(ref PacketReader reader)
    {
        string message = reader.ReadString(256);
        long timestamp = reader.ReadLong();
        long salt = reader.ReadLong();

        ChatMessageSignature? signature = null;
        if (reader.ReadBoolean())
        {
            byte[] signatureBytes = reader.ReadBytes(ChatMessageSignature.Size);
            signature = new ChatMessageSignature(signatureBytes);
        }

        LastSeenMessageUpdate lastSeen = ChatIO.ReadLastSeenUpdate(ref reader);
        return new ServerboundChatPacket(message, timestamp, salt, signature, lastSeen);
    }
}

/// <summary>Reads a LastSeenMessages.Update (offset VarInt + Fixed BitSet 20 + checksum Byte).</summary>
internal static class ChatIO
{
    public static LastSeenMessageUpdate ReadLastSeenUpdate(ref PacketReader reader)
    {
        int offset = reader.ReadVarInt();
        byte[] acknowledged = LastSeenMessageUpdate.ReadAcknowledged(ref reader);
        byte checksum = reader.ReadByte();
        return new LastSeenMessageUpdate(offset, acknowledged, checksum);
    }

    /// <summary>Writes a LastSeenMessages.Update.</summary>
    public static void WriteLastSeenUpdate(LastSeenMessageUpdate update, PacketWriter writer)
    {
        writer.WriteVarInt(update.Offset);
        LastSeenMessageUpdate.WriteAcknowledged(writer, update.Acknowledged);
        writer.WriteByte(update.Checksum);
    }
}

/// <summary>
/// Serverbound Chat Command (play, id 7, "chat_command"). Fields: Command (String UTF-8, max 32767).
/// Sent when the player sends a client-side known command without argument signing in some flows;
/// the 26.3 client uses chat_command_signed for commands with configurable behaviour.
/// </summary>
public sealed class ServerboundChatCommandPacket : IPacket
{
    /// <summary>Maximum command length (26.3 readUtf default = Short.MAX_VALUE).</summary>
    public const int MaxCommandLength = 32767;

    public ServerboundChatCommandPacket(string command)
    {
        Command = command;
    }

    /// <summary>The command text (excluding the leading "/").</summary>
    public string Command { get; }
}

/// <summary>Codec for <see cref="ServerboundChatCommandPacket"/>.</summary>
public sealed class ServerboundChatCommandCodec : PacketCodec<ServerboundChatCommandPacket>
{
    /// <summary>Singleton access.</summary>
    public static ServerboundChatCommandCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 7;

    /// <inheritdoc />
    protected override void EncodeBody(ServerboundChatCommandPacket packet, PacketWriter writer)
        => writer.WriteString(packet.Command, ServerboundChatCommandPacket.MaxCommandLength);

    /// <inheritdoc />
    protected override ServerboundChatCommandPacket DecodeBody(ref PacketReader reader)
        => new(reader.ReadString(ServerboundChatCommandPacket.MaxCommandLength));
}

/// <summary>
/// Serverbound Chat Command Signed (play, id 8, "chat_command_signed"). Fields verified from the
/// 26.3 client (ServerboundChatCommandSignedPacket.STREAM_CODEC):
/// Command (String 32767), Timestamp (Long epoch millis), Salt (Long),
/// Argument Signatures (Prefixed Array ≤8 of {Name String 16, Signature 256 bytes}),
/// Last Seen Messages Update.
/// </summary>
public sealed class ServerboundChatCommandSignedPacket : IPacket
{
    /// <summary>Maximum number of command arguments the client may sign (26.3 MAX_ARGUMENT_COUNT).</summary>
    public const int MaxCommandArgumentCount = 8;

    public ServerboundChatCommandSignedPacket(
        string command,
        long timestampEpochMillis,
        long salt,
        CommandArgumentSignature[] argumentSignatures,
        LastSeenMessageUpdate lastSeenMessages)
    {
        Command = command;
        TimestampEpochMillis = timestampEpochMillis;
        Salt = salt;
        ArgumentSignatures = argumentSignatures;
        LastSeenMessages = lastSeenMessages;
    }

    /// <summary>The command text (excluding the leading "/").</summary>
    public string Command { get; }

    /// <summary>Command timestamp in epoch milliseconds.</summary>
    public long TimestampEpochMillis { get; }

    /// <summary>Signature salt.</summary>
    public long Salt { get; }

    /// <summary>Signatures for typed command arguments (≤ <see cref="MaxCommandArgumentCount"/>).</summary>
    public CommandArgumentSignature[] ArgumentSignatures { get; }

    /// <summary>Last-seen messages update.</summary>
    public LastSeenMessageUpdate LastSeenMessages { get; }
}

/// <summary>Codec for <see cref="ServerboundChatCommandSignedPacket"/>.</summary>
public sealed class ServerboundChatCommandSignedCodec : PacketCodec<ServerboundChatCommandSignedPacket>
{
    /// <summary>Singleton access.</summary>
    public static ServerboundChatCommandSignedCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 8;

    /// <inheritdoc />
    protected override void EncodeBody(ServerboundChatCommandSignedPacket packet, PacketWriter writer)
    {
        writer.WriteString(packet.Command, ServerboundChatCommandPacket.MaxCommandLength);
        writer.WriteLong(packet.TimestampEpochMillis);
        writer.WriteLong(packet.Salt);

        writer.WriteVarInt(packet.ArgumentSignatures.Length);
        foreach (CommandArgumentSignature argument in packet.ArgumentSignatures)
        {
            writer.WriteString(argument.Name, 16);
            writer.WriteBytes(argument.Signature.Bytes);
        }

        ChatIO.WriteLastSeenUpdate(packet.LastSeenMessages, writer);
    }

    /// <inheritdoc />
    protected override ServerboundChatCommandSignedPacket DecodeBody(ref PacketReader reader)
    {
        string command = reader.ReadString(ServerboundChatCommandPacket.MaxCommandLength);
        long timestamp = reader.ReadLong();
        long salt = reader.ReadLong();

        int argumentCount = reader.ReadVarInt();
        if (argumentCount < 0 || argumentCount > ServerboundChatCommandSignedPacket.MaxCommandArgumentCount)
        {
            throw new InvalidDataException(
                $"Command argument signature count {argumentCount} is out of range (0..{ServerboundChatCommandSignedPacket.MaxCommandArgumentCount}).");
        }

        var arguments = new CommandArgumentSignature[argumentCount];
        for (int i = 0; i < argumentCount; i++)
        {
            string name = reader.ReadString(16);
            byte[] signatureBytes = reader.ReadBytes(ChatMessageSignature.Size);
            arguments[i] = new CommandArgumentSignature(name, new ChatMessageSignature(signatureBytes));
        }

        LastSeenMessageUpdate lastSeen = ChatIO.ReadLastSeenUpdate(ref reader);
        return new ServerboundChatCommandSignedPacket(command, timestamp, salt, arguments, lastSeen);
    }
}

/// <summary>
/// Serverbound Chat Ack (play, id 6, "chat_ack"). Fields: Offset (VarInt). The client acks
/// chat messages it has received; verified from the 26.3 client (ServerboundChatAckPacket).
/// </summary>
public sealed class ServerboundChatAckPacket : IPacket
{
    public ServerboundChatAckPacket(int offset)
    {
        Offset = offset;
    }

    /// <summary>The acknowledged message offset.</summary>
    public int Offset { get; }
}

/// <summary>Codec for <see cref="ServerboundChatAckPacket"/>.</summary>
public sealed class ServerboundChatAckCodec : PacketCodec<ServerboundChatAckPacket>
{
    /// <summary>Singleton access.</summary>
    public static ServerboundChatAckCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 6;

    /// <inheritdoc />
    protected override void EncodeBody(ServerboundChatAckPacket packet, PacketWriter writer)
        => writer.WriteVarInt(packet.Offset);

    /// <inheritdoc />
    protected override ServerboundChatAckPacket DecodeBody(ref PacketReader reader)
        => new(reader.ReadVarInt());
}

/// <summary>
/// Serverbound Chat Session Update (play, id 10, "chat_session_update"). The client announces its
/// remote chat session at the start of the session. Fields verified from the 26.3 client
/// (RemoteChatSession.Data.STREAM_CODEC + ProfilePublicKey.Data.STREAM_CODEC):
/// Session ID (UUID = 2 longs), Expires At (Long epoch millis),
/// Public Key (Prefixed Byte Array ≤512), Key Signature (Prefixed Byte Array ≤4096).
/// The server neither validates nor uses the key material in this milestone.
/// </summary>
public sealed class ServerboundChatSessionUpdatePacket : IPacket
{
    public const int MaxPublicKeyLength = 512;
    public const int MaxKeySignatureLength = 4096;

    public ServerboundChatSessionUpdatePacket(
        long sessionIdMsb,
        long sessionIdLsb,
        long expiresAtEpochMillis,
        byte[] publicKey,
        byte[] keySignature)
    {
        SessionIdMsb = sessionIdMsb;
        SessionIdLsb = sessionIdLsb;
        ExpiresAtEpochMillis = expiresAtEpochMillis;
        PublicKey = publicKey;
        KeySignature = keySignature;
    }

    /// <summary>Most significant bits of the session UUID.</summary>
    public long SessionIdMsb { get; }

    /// <summary>Least significant bits of the session UUID.</summary>
    public long SessionIdLsb { get; }

    /// <summary>Public key expiry in epoch milliseconds.</summary>
    public long ExpiresAtEpochMillis { get; }

    /// <summary>The public key bytes (prefix length ++ key).</summary>
    public byte[] PublicKey { get; }

    /// <summary>The key signature bytes (prefix length ++ signature).</summary>
    public byte[] KeySignature { get; }
}

/// <summary>Codec for <see cref="ServerboundChatSessionUpdatePacket"/>.</summary>
public sealed class ServerboundChatSessionUpdateCodec : PacketCodec<ServerboundChatSessionUpdatePacket>
{
    /// <summary>Singleton access.</summary>
    public static ServerboundChatSessionUpdateCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 10;

    /// <inheritdoc />
    protected override void EncodeBody(ServerboundChatSessionUpdatePacket packet, PacketWriter writer)
    {
        writer.WriteLong(packet.SessionIdMsb);
        writer.WriteLong(packet.SessionIdLsb);
        writer.WriteLong(packet.ExpiresAtEpochMillis);
        writer.WritePrefixedByteArray(packet.PublicKey);
        writer.WritePrefixedByteArray(packet.KeySignature);
    }

    /// <inheritdoc />
    protected override ServerboundChatSessionUpdatePacket DecodeBody(ref PacketReader reader)
    {
        long sessionIdMsb = reader.ReadLong();
        long sessionIdLsb = reader.ReadLong();
        long expiresAt = reader.ReadLong();

        byte[] publicKey = reader.ReadPrefixedByteArray();
        if (publicKey.Length > ServerboundChatSessionUpdatePacket.MaxPublicKeyLength)
        {
            throw new InvalidDataException(
                $"Chat session public key length {publicKey.Length} exceeds {ServerboundChatSessionUpdatePacket.MaxPublicKeyLength}.");
        }

        byte[] keySignature = reader.ReadPrefixedByteArray();
        if (keySignature.Length > ServerboundChatSessionUpdatePacket.MaxKeySignatureLength)
        {
            throw new InvalidDataException(
                $"Chat session key signature length {keySignature.Length} exceeds {ServerboundChatSessionUpdatePacket.MaxKeySignatureLength}.");
        }

        return new ServerboundChatSessionUpdatePacket(sessionIdMsb, sessionIdLsb, expiresAt, publicKey, keySignature);
    }
}
