using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;

namespace VoXelSharp.Protocol;

/// <summary>
/// Minecraft protocol compression wrapper.
/// Wire facts per docs/PROTOCOL.md: compression uses the Minecraft (zlib) protocol compression
/// mechanism, and the implementation must enforce a decompressed payload safety limit of 8 MiB.
/// Compression negotiation and threshold behavior are intentionally out of scope here and must be
/// verified against the 26.3 protocol specification before Login compression is implemented.
/// </summary>
public static class Compression
{
    /// <summary>Maximum size of a decompressed payload, in bytes (8 MiB).</summary>
    public const int MaxDecompressedLength = 8 * 1024 * 1024;

    /// <summary>
    /// Compresses <paramref name="source"/> using the zlib format.
    /// </summary>
    /// <returns>The zlib-compressed bytes.</returns>
    public static byte[] Compress(ReadOnlySpan<byte> source)
    {
        if (source.IsEmpty)
        {
            // .NET's ZLibStream writes nothing for an empty source, which is not a valid zlib
            // stream. Emit the canonical empty zlib stream (verified against the reference zlib
            // implementation, which always produces this 8-byte encoding).
            return new byte[] { 0x78, 0x9C, 0x03, 0x00, 0x00, 0x00, 0x00, 0x01 };
        }

        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionMode.Compress, leaveOpen: true))
        {
            zlib.Write(source);
        }

        return output.ToArray();
    }

    /// <summary>
    /// Attempts to decompress <paramref name="compressed"/>.
    /// </summary>
    /// <param name="compressed">The zlib-compressed bytes.</param>
    /// <param name="result">The decompressed bytes when this returns <see langword="true"/>; otherwise <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> when the input is valid zlib data whose decompressed size does not
    /// exceed <see cref="MaxDecompressedLength"/>; <see langword="false"/> when the input is
    /// truncated, malformed, or decompresses past the safety limit.
    /// </returns>
    public static bool TryDecompress(ReadOnlySpan<byte> compressed, [NotNullWhen(true)] out byte[]? result)
    {
        try
        {
            result = Decompress(compressed);
            return true;
        }
        catch (InvalidDataException)
        {
            result = null;
            return false;
        }
    }

    /// <summary>
    /// Decompresses <paramref name="compressed"/>.
    /// </summary>
    /// <returns>The decompressed bytes.</returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when the input is truncated, malformed, or decompresses past
    /// <see cref="MaxDecompressedLength"/> bytes (8 MiB).
    /// </exception>
    public static byte[] Decompress(ReadOnlySpan<byte> compressed)
    {
        if (compressed.Length < 8)
        {
            // Too short to contain a zlib header, a deflate payload, and the 4-byte ADLER-32
            // trailer. RFC 1950's smallest complete stream (the reference implementation's empty
            // encoding) is 8 bytes: 2 header + 2 deflate block + 4 trailer. Fail closed rather
            // than guessing at a sub-minimal stream.
            throw new InvalidDataException("Compressed payload is truncated or malformed.");
        }

        using var input = new MemoryStream(compressed.ToArray(), writable: false);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();

        byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            int read;
            while ((read = zlib.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (output.Length + read > MaxDecompressedLength)
                {
                    throw new InvalidDataException(
                        $"Decompressed payload exceeds the limit of {MaxDecompressedLength} bytes (8 MiB).");
                }

                output.Write(buffer, 0, read);
            }
        }
        catch (IOException exception)
        {
            // ZLibStream surfaces truncated streams as EndOfStreamException/IOException;
            // normalize to the protocol-level failure type.
            throw new InvalidDataException("Compressed payload is truncated or malformed.", exception);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        byte[] result = output.ToArray();

        // ZLibStream tolerates streams truncated mid-deflate; verify the ADLER-32 trailer
        // (RFC 1950) so an incomplete stream is rejected just as the reference implementation
        // rejects it.
        uint expected = (uint)((compressed[^4] << 24) | (compressed[^3] << 16) | (compressed[^2] << 8) | compressed[^1]);
        if (Adler32(result) != expected)
        {
            throw new InvalidDataException("Compressed payload is truncated or malformed.");
        }

        return result;
    }

    private static uint Adler32(ReadOnlySpan<byte> data)
    {
        const uint mod = 65521;
        uint a = 1;
        uint b = 0;
        foreach (byte value in data)
        {
            a = (a + value) % mod;
            b = (b + a) % mod;
        }

        return (b << 16) | a;
    }
}
