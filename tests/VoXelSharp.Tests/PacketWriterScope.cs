using VoXelSharp.Protocol;

namespace VoXelSharp.Tests;

/// <summary>
/// Holds a <see cref="PacketWriter"/> for the duration of a test and exposes the bytes written.
/// </summary>
internal sealed class PacketWriterScope : IDisposable
{
    public PacketWriter Writer { get; } = new();

    /// <summary>A snapshot of the bytes written so far.</summary>
    public byte[] Bytes => Writer.ToArray();

    public void Dispose()
    {
    }
}
