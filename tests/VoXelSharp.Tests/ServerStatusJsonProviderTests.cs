using System.Text.Json;
using VoXelSharp.Server.Status;

namespace VoXelSharp.Tests;

public class ServerStatusJsonProviderTests
{
    [Fact]
    public void MatchesVerifiedSchema()
    {
        var provider = new ServerStatusJsonProvider(new ServerStatusData("A Minecraft Server", 20, 0));

        string json = provider.GetStatusJson();
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Assert.Equal("26.3", root.GetProperty("version").GetProperty("name").GetString());
        Assert.Equal(777, root.GetProperty("version").GetProperty("protocol").GetInt32());
        Assert.Equal(20, root.GetProperty("players").GetProperty("max").GetInt32());
        Assert.Equal(0, root.GetProperty("players").GetProperty("online").GetInt32());
        Assert.Equal("A Minecraft Server", root.GetProperty("description").GetProperty("text").GetString());
    }

    [Fact]
    public void ReportsProvidedPlayerCounts()
    {
        var provider = new ServerStatusJsonProvider(new ServerStatusData("Test", 10, 3));

        using JsonDocument document = JsonDocument.Parse(provider.GetStatusJson());
        JsonElement root = document.RootElement;

        Assert.Equal(10, root.GetProperty("players").GetProperty("max").GetInt32());
        Assert.Equal(3, root.GetProperty("players").GetProperty("online").GetInt32());
    }

    [Fact]
    public void EscapesSpecialCharactersInDescription()
    {
        var provider = new ServerStatusJsonProvider(new ServerStatusData("line1\nline2 \"quoted\" \\slash", 20, 0));

        string json = provider.GetStatusJson();
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal("line1\nline2 \"quoted\" \\slash", document.RootElement.GetProperty("description").GetProperty("text").GetString());
    }

    [Fact]
    public void IsStableAcrossCalls()
    {
        var provider = new ServerStatusJsonProvider(new ServerStatusData("Stable", 20, 0));

        Assert.Equal(provider.GetStatusJson(), provider.GetStatusJson());
    }
}
