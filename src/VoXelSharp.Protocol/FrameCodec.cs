namespace VoXelSharp.Protocol;

/// <summary>
/// Encodes and decodes Minecraft packet frames (docs/PROTOCOL.md Packet Data / wiki "Packet format").
/// A frame is: Length (VarInt, at most 3 bytes) followed by Packet ID + Data.
/// The frame size limit is <see cref="FrameLength.MaxFrameLength"/>. The length prefix must not be
/// longer than 3 bytes even when the value would fit in fewer; unnecessarily long encodings of at
/// most 3 bytes are allowed (e.g. 0x81 0x00 for length 1).
/// This class handles the uncompressed format only; compression is negotiated later (Login).
/// </summary>
public static class FrameCodec
{
    /// <summary>
    /// Attempts to decode a complete frame from the front of <paramref name="buffer"/>.
    /// </summary>
    /// <param name="buffer">Buffered, not-yet-consumed bytes from the stream.</param>
    /// <param name="payload">The packet payload (Packet ID + Data), without the length prefix, when a frame completes.</param>
    /// <param name="bytesConsumed">The number of buffer bytes belonging to the frame (prefix + payload).</param>
    /// <returns>
    /// <see langword="true"/> when a complete frame was decoded; <see langword="false"/> when more
    /// bytes are required. On <see langword="false"/> nothing is consumed.
    /// </returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when the length prefix is wider than <see cref="FrameLength.MaxEncodedLength"/> bytes
    /// or the declared length exceeds <see cref="FrameLength.MaxFrameLength"/>.
    /// </exception>
    public static bool TryReadFrame(ReadOnlySpan<byte> buffer, out byte[] payload, out int bytesConsumed)
    {
        payload = [];
        bytesConsumed = 0;

        if (buffer.IsEmpty)
        {
            return false;
        }

        // Scan for the end of the length prefix. The prefix is a VarInt of at most 3 bytes,
        // so a terminator byte must appear within the first 3 bytes.
        int window = Math.Min(buffer.Length, FrameLength.MaxEncodedLength);
        int prefixBytes = 0;
        for (int i = 0; i < window; i++)
        {
            if ((buffer[i] & 0x80) == 0)
            {
                prefixBytes = i + 1;
                break;
            }
        }

        if (prefixBytes == 0)
        {
            if (buffer.Length >= FrameLength.MaxEncodedLength)
            {
                throw new InvalidDataException(
                    $"Frame length prefix is wider than {FrameLength.MaxEncodedLength} bytes.");
            }

            return false; // prefix bytes received so far are incomplete
        }

        long length = 0;
        for (int i = 0; i < prefixBytes; i++)
        {
            // VarInt groups are little-endian: the least significant 7-bit group is written first.
            length |= (long)(buffer[i] & 0x7F) << (7 * i);
        }

        // Unreachable with prefixBytes <= 3 (max value is MaxFrameLength), kept as defense in depth.
        if (length > FrameLength.MaxFrameLength)
        {
            throw new InvalidDataException(
                $"Frame length {length} exceeds the limit of {FrameLength.MaxFrameLength} bytes.");
        }

        int total = prefixBytes + (int)length;
        if (buffer.Length < total)
        {
            return false; // frame body is incomplete
        }

        payload = buffer.Slice(prefixBytes, (int)length).ToArray();
        bytesConsumed = total;
        return true;
    }

    /// <summary>
    /// Writes a frame: the length prefix followed by the payload.
    /// </summary>
    /// <param name="payload">The packet payload (Packet ID + Data).</param>
    /// <param name="destination">The destination buffer, which must hold <paramref name="payload"/>.Length + up to 3 prefix bytes.</param>
    /// <returns>The total number of bytes written (prefix + payload).</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="payload"/> exceeds <see cref="FrameLength.MaxFrameLength"/> bytes.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is too small.</exception>
    public static int EncodeFrame(ReadOnlySpan<byte> payload, Span<byte> destination)
    {
        int prefixBytes = FrameLength.Write(payload.Length, destination);
        payload.CopyTo(destination[prefixBytes..]);
        return prefixBytes + payload.Length;
    }
}
