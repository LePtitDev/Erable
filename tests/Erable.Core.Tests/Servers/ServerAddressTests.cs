using Erable.Core.Servers;

namespace Erable.Core.Tests.Servers;

public sealed class ServerAddressTests
{
    [Theory]
    [InlineData("https://wiki.example.org", "https://wiki.example.org")]
    [InlineData("  https://wiki.example.org/  ", "https://wiki.example.org")]
    [InlineData("wiki.example.org", "https://wiki.example.org")]
    [InlineData("WIKI.Example.org", "https://wiki.example.org")]
    [InlineData("http://192.168.1.20:5080/some/path?x=1#y", "http://192.168.1.20:5080")]
    public void Normalize_KeepsTheOriginOnly(string typed, string expected)
    {
        ServerAddress.Normalize(typed).Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("ftp://wiki.example.org")]
    [InlineData("file:///sdcard/wiki")]
    [InlineData("https://user:secret@wiki.example.org")]
    [InlineData("javascript:alert(1)")]
    public void Normalize_NotAnAddressOfAServer_IsRefused(string? typed)
    {
        ServerAddress.Normalize(typed).Error.Code.ShouldBe(ServerAddress.InvalidUrl);
    }

    [Theory]
    [InlineData("http://192.168.1.20:5080", true)]
    [InlineData("https://wiki.example.org", false)]
    public void IsCleartext_TellsPlainHttp(string origin, bool expected)
    {
        ServerAddress.IsCleartext(origin).ShouldBe(expected);
    }

    [Fact]
    public void Parse_AnswerOfAServer_ReadsItsVersionsAndMode()
    {
        var handshake = ServerHandshake.Parse("""{"appVersion":"1.2.0","apiVersion":"v1","formatVersion":4,"mode":"server","folderProjects":false}""");

        handshake.ShouldBe(new ServerHandshake("1.2.0", "v1", 4, "server"));
        handshake!.IsServerMode.ShouldBeTrue();
    }

    [Fact]
    public void Parse_UnknownFieldsAndTypes_AreIgnored()
    {
        var handshake = ServerHandshake.Parse("""{"apiVersion":"v2","formatVersion":"five","mode":"cluster","new":[1]}""");

        handshake.ShouldBe(new ServerHandshake("?", "v2", 0, "cluster"));
        handshake!.IsServerMode.ShouldBeFalse();
    }

    [Theory]
    [InlineData("<html>not erable</html>")]
    [InlineData("[]")]
    [InlineData("""{"appVersion":"1.0"}""")]
    [InlineData("""{"apiVersion":""}""")]
    [InlineData("""{"apiVersion":1}""")]
    public void Parse_NotAnErableAnswer_IsNull(string json)
    {
        ServerHandshake.Parse(json).ShouldBeNull();
    }
}
