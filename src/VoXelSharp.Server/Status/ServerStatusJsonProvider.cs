using System.Text.Json.Nodes;

namespace VoXelSharp.Server.Status;

/// <summary>
/// Provides the JSON payload for a Status Response packet (wiki "Server List Ping").
/// </summary>
public interface IStatusJsonProvider
{
    /// <summary>Returns the server-list JSON to send in response to a Status Request.</summary>
    string GetStatusJson();
}

/// <summary>
/// Static server-list data used to build the status JSON.
/// </summary>
/// <param name="Description">MOTD text (sent as a text component).</param>
/// <param name="MaxPlayers">Maximum player count shown in the client.</param>
/// <param name="OnlinePlayers">Current online player count shown in the client.</param>
public sealed record ServerStatusData(string Description, int MaxPlayers, int OnlinePlayers);

/// <summary>
/// Builds the Status Response JSON from <see cref="ServerStatusData"/>.
/// JSON layout per the Minecraft Wiki "Server List Ping" page (captured under
/// docs/data/_raw/_server_list_ping.raw.wikitext.txt) as documented in docs/PROTOCOL.md:
/// version.name (mandatory) / version.protocol / players.max / players.online /
/// description.text. favicon, enforcesSecureChat and players.sample are optional and
/// omitted here.
/// </summary>
public sealed class ServerStatusJsonProvider : IStatusJsonProvider
{
    /// <summary>The target Minecraft version shown as the server version name.</summary>
    public const string VersionName = "26.3";

    /// <summary>The target protocol version (Minecraft 26.3).</summary>
    public const int Protocol = 777;

    private readonly ServerStatusData _data;
    private readonly Lazy<string> _statusJson;

    public ServerStatusJsonProvider(ServerStatusData data)
    {
        _data = data;
        _statusJson = new Lazy<string>(() => Build(_data));
    }

    /// <inheritdoc />
    public string GetStatusJson() => _statusJson.Value;

    private static string Build(ServerStatusData data)
    {
        var root = new JsonObject
        {
            ["version"] = new JsonObject
            {
                ["name"] = VersionName,
                ["protocol"] = Protocol,
            },
            ["players"] = new JsonObject
            {
                ["max"] = data.MaxPlayers,
                ["online"] = data.OnlinePlayers,
            },
            ["description"] = new JsonObject
            {
                ["text"] = data.Description,
            },
        };

        return root.ToJsonString();
    }
}
