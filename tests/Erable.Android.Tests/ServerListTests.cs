using Erable.Mobile.Shell;

namespace Erable.Android.Tests;

public sealed class ServerListTests
{
    private static readonly DateTimeOffset Monday = new(2026, 10, 19, 8, 0, 0, TimeSpan.Zero);

    private static SavedServer Server(string origin, string version = "1.0.0") => new(origin, version, Monday);

    [Fact]
    public void Write_ThenRead_GivesTheSameServers()
    {
        var list = ServerList.Empty()
            .Add(Server("https://wiki.example.org"))
            .Add(Server("http://192.168.1.20:5080", "0.9.0"));

        var read = ServerList.Read(list.Write());

        read.Servers.ShouldBe([Server("http://192.168.1.20:5080", "0.9.0"), Server("https://wiki.example.org")]);
    }

    [Fact]
    public void Add_KnownOrigin_MovesItFirstWithItsNewHandshake()
    {
        var list = ServerList.Empty()
            .Add(Server("https://a.example.org"))
            .Add(Server("https://b.example.org"))
            .Add(Server("https://a.example.org", "2.0.0"));

        list.Servers.Select(s => (s.Origin, s.AppVersion)).ShouldBe([("https://a.example.org", "2.0.0"), ("https://b.example.org", "1.0.0")]);
    }

    [Fact]
    public void Remove_ForgetsOnlyThatOrigin()
    {
        var list = ServerList.Empty().Add(Server("https://a.example.org")).Add(Server("https://b.example.org"));

        list.Remove("https://a.example.org").Servers.Select(s => s.Origin).ShouldBe(["https://b.example.org"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{"version":1,"servers":"nope"}""")]
    public void Read_UnreadableValue_IsAnEmptyList(string? json)
    {
        ServerList.Read(json).Servers.ShouldBeEmpty();
    }

    [Fact]
    public void Read_DropsEntriesThatAreNotOriginsOrRepeated()
    {
        const string json = """
            {"version":1,"servers":[
              {"origin":"https://a.example.org","appVersion":"1","checkedAt":"2026-10-19T08:00:00+00:00"},
              {"origin":"https://a.example.org","appVersion":"2","checkedAt":"2026-10-19T08:00:00+00:00"},
              {"origin":"https://b.example.org/path","appVersion":"1","checkedAt":"2026-10-19T08:00:00+00:00"},
              {"origin":"javascript:alert(1)","appVersion":"1","checkedAt":"2026-10-19T08:00:00+00:00"},
              null
            ]}
            """;

        ServerList.Read(json).Servers.Select(s => (s.Origin, s.AppVersion)).ShouldBe([("https://a.example.org", "1")]);
    }

    [Fact]
    public void SavedServer_ShowsItsHostAndWhetherItIsCleartext()
    {
        Server("http://192.168.1.20:5080").Host.ShouldBe("192.168.1.20:5080");
        Server("http://192.168.1.20:5080").IsCleartext.ShouldBeTrue();
        Server("https://wiki.example.org").IsCleartext.ShouldBeFalse();
    }
}
