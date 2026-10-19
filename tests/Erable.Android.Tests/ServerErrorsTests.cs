using Erable.Mobile.Shell;

namespace Erable.Android.Tests;

public sealed class ServerErrorsTests
{
    private const string Origin = "https://wiki.example.org";

    [Fact]
    public void Check_ErableServer_GivesItsHandshake()
    {
        var result = ServerErrors.Check(Origin, 200, """{"appVersion":"1.2.0","apiVersion":"v1","formatVersion":4,"mode":"server"}""");

        result.Value.AppVersion.ShouldBe("1.2.0");
    }

    [Fact]
    public void Check_NewerApiVersion_IsStillAccepted()
    {
        // The SPA comes from the server itself: the shell has no API version to agree on.
        ServerErrors.Check(Origin, 200, """{"appVersion":"3.0.0","apiVersion":"v3","mode":"server"}""").IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(404, """{"apiVersion":"v1","mode":"server"}""")]
    [InlineData(500, "")]
    [InlineData(200, "<!doctype html><title>Router</title>")]
    [InlineData(200, """{"status":"ok"}""")]
    public void Check_AnythingElse_IsNotErable(int status, string body)
    {
        ServerErrors.Check(Origin, status, body).Error.Code.ShouldBe(ServerErrors.NotErable);
    }

    [Fact]
    public void Check_DesktopBackend_IsNotAServer()
    {
        ServerErrors.Check(Origin, 200, """{"apiVersion":"v1","mode":"desktop"}""").Error.Code.ShouldBe(ServerErrors.NotServerMode);
    }
}
