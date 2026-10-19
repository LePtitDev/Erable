using System.Security.Cryptography;
using Erable.Server.Api;
using Erable.Server.Hosting;
using Erable.Server.Native;
using Erable.Server.Projects;
using Microsoft.Extensions.DependencyInjection;
using Velopack;

namespace Erable.Desktop;

/// <summary><c>Erable.Desktop [folder] [--self-test [--self-test-report file]]</c>.</summary>
internal static class Program
{
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);

    [STAThread]
    private static int Main(string[] args)
    {
        // First thing, before anything else: Velopack handles its install, update and uninstall hooks here and exits.
        VelopackApp.Build().Run();

        var selfTest = args.Contains(SelfTest.Flag);
        var reportIndex = Array.IndexOf(args, SelfTest.ReportFlag);
        var reportPath = reportIndex >= 0 && reportIndex + 1 < args.Length ? args[reportIndex + 1] : null;
        var folder = FolderArgument(args, reportIndex);
        var testMode = Environment.GetEnvironmentVariable("ERABLE_TEST_MODE") == "1";
        var profile = AppProfile.GetDirectory();

        // The smoke journey always opens a window of the default size and leaves the user's placement alone.
        var userProfile = new UserProfile(profile);
        var shell = new PhotinoShell { Placement = selfTest ? null : WindowPlacement.Load(userProfile) };
        if (testMode)
        {
            shell.PickFolderOverride = Environment.GetEnvironmentVariable("ERABLE_TEST_PICK_FOLDER") is { Length: > 0 } picked ? picked : null;
        }

        // F16.R2: a project already open in another instance brings that instance to the front, and this one stops
        // before showing anything.
        if (folder is not null
            && InstanceRegistry.ActivateOwnerAsync(profile, folder, shell.AllowForeground, CancellationToken.None).GetAwaiter().GetResult())
        {
            return 0;
        }

        // 256-bit session token, generated per launch and never logged (docs/architecture.md §9).
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var options = new ErableHostOptions
        {
            Mode = HostMode.Desktop,
            Address = ErableHost.DesktopAddress,
            Port = 0,
            SessionToken = token,
            DataDirectory = profile,
            TestMode = testMode,
            SelfTest = selfTest,
        };

        var app = ErableHost.Build(options, builder => builder.Services.AddSingleton<INativeBridge>(shell));
        var window = app.Services.GetRequiredService<WindowCoordinator>();
        shell.Closing = window.OnClosing;
        shell.FolderDropped = window.OnFolderDropped;
        Task<SelfTest.Result>? selfTestResult = null;
        string? selfTestFolder = null;
        try
        {
            app.StartAsync().GetAwaiter().GetResult();
            var baseAddress = ErableHost.GetListeningUri(app);

            // Tests and the smoke journey must not depend on the network or change the installed version.
            if (!selfTest && !testMode)
            {
                _ = Task.Run(() => AppUpdater.CheckAsync(profile));
            }

            // The token travels in the fragment: never sent to the server, so never in a request log.
            var fragment = "#session=" + token;
            if (folder is not null)
            {
                fragment += "&open=" + Uri.EscapeDataString(folder);
            }

            if (selfTest)
            {
                // The smoke journey creates its project in a folder of its own, deleted afterwards.
                selfTestFolder = Directory.CreateTempSubdirectory("erable-selftest-").FullName;
                fragment += "&selftest=1&selftestFolder=" + Uri.EscapeDataString(Path.Combine(selfTestFolder, "Fumée"));
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
            if (!selfTest)
            {
                shell.Placement?.Save(userProfile);
            }
        }
        finally
        {
            // Closing the window stops Kestrel, which closes the projects (WAL checkpoint, lock released) and releases
            // the process (F16.R3).
            using var timeout = new CancellationTokenSource(ShutdownTimeout);
            app.StopAsync(timeout.Token).GetAwaiter().GetResult();
            app.DisposeAsync().AsTask().GetAwaiter().GetResult();
            if (selfTestFolder is not null)
            {
                TryDelete(selfTestFolder);
            }
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

    /// <summary>The project folder given on the command line (file managers and « Open with » pass it), if any.</summary>
    private static string? FolderArgument(string[] args, int reportIndex)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--", StringComparison.Ordinal) || i == reportIndex + 1 && reportIndex >= 0)
            {
                continue;
            }

            return Path.GetFullPath(args[i]);
        }

        return null;
    }

    private static void TryDelete(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (IOException)
        {
            // A temporary folder: the OS cleans it up eventually.
        }
    }
}
