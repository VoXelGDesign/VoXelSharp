using VoXelSharp.Protocol;

namespace VoXelSharp.Tests;

public class ConnectionStateTests
{
    [Fact]
    public void StatesAreUnique()
    {
        ConnectionState[] states = Enum.GetValues<ConnectionState>();

        Assert.Equal(states.Length, states.Distinct().Count());
    }

    [Fact]
    public void StatesContainExactlyTheDocumentedSet()
    {
        ConnectionState[] expected =
        {
            ConnectionState.Handshaking,
            ConnectionState.Status,
            ConnectionState.Login,
            ConnectionState.Configuration,
            ConnectionState.Play,
        };

        ConnectionState[] actual = Enum.GetValues<ConnectionState>();

        Assert.Equal(expected, actual);
    }
}
