using Erable.Mobile.Shell;

namespace Erable.Android.Tests;

public sealed class NavigationPolicyTests
{
    private const string Server = "https://wiki.example.org";

    [Theory]
    [InlineData("https://wiki.example.org/")]
    [InlineData("https://wiki.example.org/p/3F2A9C14E07B5D21/e/5B19C0E2A4D7F311#histoire")]
    [InlineData("https://WIKI.example.org:443/api/v1/projects")]
    public void Decide_PageOfTheServer_Stays(string url)
    {
        NavigationPolicy.Decide(Server, url).ShouldBe(NavigationDecision.Stay);
    }

    [Theory]
    [InlineData("https://fr.wikipedia.org/wiki/Erable")]
    [InlineData("http://wiki.example.org/")] // another scheme is another origin
    [InlineData("https://wiki.example.org:8443/")]
    [InlineData("https://evil.example.org.attacker.net/")]
    [InlineData("mailto:camille@example.org")]
    [InlineData("tel:+33100000000")]
    public void Decide_OtherSiteOrApplication_GoesOutside(string url)
    {
        NavigationPolicy.Decide(Server, url).ShouldBe(NavigationDecision.External);
    }

    [Theory]
    [InlineData("file:///sdcard/Download/secret.txt")]
    [InlineData("content://com.android.providers.downloads/1")]
    [InlineData("javascript:alert(1)")]
    [InlineData("intent://scan/#Intent;scheme=zxing;end")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("not a url")]
    public void Decide_LocalOrScriptLink_IsBlocked(string url)
    {
        NavigationPolicy.Decide(Server, url).ShouldBe(NavigationDecision.Block);
    }

    [Fact]
    public void Decide_LocalNetworkServer_KeepsItsPort()
    {
        NavigationPolicy.Decide("http://192.168.1.20:5080", "http://192.168.1.20:5080/p/x").ShouldBe(NavigationDecision.Stay);
        NavigationPolicy.Decide("http://192.168.1.20:5080", "http://192.168.1.20/").ShouldBe(NavigationDecision.External);
    }
}
