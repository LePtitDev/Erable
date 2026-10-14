using System.Security.Cryptography;
using Erable.Server.Hosting;
using Erable.Server.Native;
using Microsoft.Extensions.DependencyInjection;

namespace Erable.Desktop;

internal static class Program
{
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);

    [STAThread]
    private static int Main()
    {
        var shell = new PhotinoShell();

        // 256-bit session token, generated per launch and never logged (docs/architecture.md §9).
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var options = new ErableHostOptions
        {
            Mode = HostMode.Desktop,
            Address = ErableHost.DesktopAddress,
            Port = 0,
            SessionToken = token,
            DataDirectory = AppProfile.GetDirectory(),
            TestMode = Environment.GetEnvironmentVariable("ERABLE_TEST_MODE") == "1",
        };

        var app = ErableHost.Build(options, builder => builder.Services.AddSingleton<INativeBridge>(shell));
        try
        {
            app.StartAsync().GetAwaiter().GetResult();

            // The token travels in the fragment: never sent to the server, so never in a request log.
            var url = new Uri(ErableHost.GetListeningUri(app), "/#session=" + token);
            shell.Run(url);
        }
        finally
        {
            // Closing the window stops Kestrel and releases the process (F16.R3).
            using var timeout = new CancellationTokenSource(ShutdownTimeout);
            app.StopAsync(timeout.Token).GetAwaiter().GetResult();
            app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        return 0;
    }
}
