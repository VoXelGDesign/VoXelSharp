using System.IO.Pipelines;
using System.Net.Sockets;

namespace VoXelSharp.Server.Connections;

/// <summary>
/// An <see cref="IDuplexPipe"/> wrapping a <see cref="NetworkStream"/>.
/// For outbound architecture, a bounded <c>Channel&lt;T&gt;</c> will replace
/// direct writes once gameplay traffic requires it (M4+).
/// </summary>
public sealed class StreamDuplexPipe : IDuplexPipe
{
    public StreamDuplexPipe(NetworkStream stream)
    {
        Input = PipeReader.Create(stream);
        Output = PipeWriter.Create(stream);
    }

    public PipeReader Input { get; }
    public PipeWriter Output { get; }
}
