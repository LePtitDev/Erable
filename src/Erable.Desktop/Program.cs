using System.Security.Cryptography;
using Erable.Server.Api;
using Erable.Server.Hosting;
using Erable.Server.Native;
using Microsoft.Extensions.DependencyInjection;

namespace Erable.Desktop;

internal static class Program
{
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);

    [STAThread]
    private static int Main(string[] args)
    {
        var selfTest = args.Contains(SelfTest.Flag);
        var reportIndex = Array.IndexOf(args, SelfTest.ReportFlag);
        var reportPath = reportIndex >= 0 && reportIndex + 1 < args.Length ? args[reportIndex + 1] : null;

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
            SelfTest = selfTest,
        };

        var app = ErableHost.Build(options, builder => builder.Services.AddSingleton<INativeBridge>(shell));
        Task<SelfTest.Result>? selfTestResult = null;
        try
        {
            app.StartAsync().GetAwaiter().GetResult();
            var baseAddress = ErableHost.GetListeningUri(app);

            // The token travels in the fragment: never sent to the server, so never in a request log.
            var fragment = "#session=" + token;
            if (selfTest)
            {
                fragment += "&selftest=1";
                var channel = app.Services.GetRequiredService<SelfTestChannel>();
                selfTestResult = Task.Run(async () =>
                {
                    // Runs concurrently with the window, so that it does not weigh on the startup measurement.
                    var shellSteps = await SelfTest.CheckLoopbackProtectionAsync(baseAddress).ConfigureAwait(false);
                    SelfTestReport? report = null;
                    try
                    {
                        report = await channel.Report.WaitAsync(SelfTest.Timeout).ConfigureAwait(false);
                    }
                    catch (TimeoutException)
                    {
                        // Reported as a failed step.
                    }

                    var combined = SelfTest.Combine(shellSteps, report, SelfTest.SinceProcessStart());
                    shell.Close();
                    return combined;
                });
            }

            shell.Run(new Uri(baseAddress, "/" + fragment));
        }
        finally
        {
            // Closing the window stops Kestrel and releases the process (F16.R3).
            using var timeout = new CancellationTokenSource(ShutdownTimeout);
            app.StopAsync(timeout.Token).GetAwaiter().GetResult();
            app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        if (selfTestResult is null)
        {
            return 0;
        }

        var result = selfTestResult.IsCompleted
            ? selfTestResult.GetAwaiter().GetResult()
            : new SelfTest.Result(false, [new SelfTest.Step("window", false, 0, "window closed before the report")]);
        SelfTest.Write(result, reportPath);
        return result.Success ? 0 : 1;
    }
}
