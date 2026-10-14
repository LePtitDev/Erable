// Throwaway spike host (M0-08), PhotinoX variant: same as ../host, to compare the API and behaviour.
// Usage: Spike.HostX <web dist folder>
using System.Diagnostics;
using System.Security.Cryptography;
using Erable.Server.Hosting;
using Photino.NET;

var stopwatch = Stopwatch.StartNew();
var options = new ErableHostOptions
{
    Mode = HostMode.Desktop,
    SessionToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
    WebRootPath = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine("..", "web", "dist")),
};
var app = ErableHost.Build(options);
await app.StartAsync();
var url = ErableHost.GetListeningUri(app);
Console.WriteLine($"SPIKE url={url} backendReadyMs={stopwatch.ElapsedMilliseconds}");

// Photino requires an STA thread on Windows.
var ui = new Thread(() =>
{
    var window = new PhotinoWindow(null)
        .SetTitle("Erable spike — PhotinoX")
        .SetSize(1280, 900)
        .Center()
        .SetDevToolsEnabled(true)
        .Load(url);
    new PhotinoApplication().Run(window);
});
if (OperatingSystem.IsWindows())
{
    ui.SetApartmentState(ApartmentState.STA);
}

ui.Start();
ui.Join();

await app.StopAsync();
