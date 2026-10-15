using Erable.Server.Hosting;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Erable.Server.Api;

/// <summary>One step of the scripted desktop smoke journey (docs/testing.md §10).</summary>
/// <param name="Name">Stable step identifier, e.g. <c>home-visible</c>.</param>
/// <param name="Ok">Whether the step succeeded.</param>
/// <param name="DurationMs">Time spent in the step.</param>
/// <param name="Detail">Diagnostic message, if any.</param>
public sealed record SelfTestStep(string Name, bool Ok, double DurationMs, string? Detail);

/// <summary>Result of the SPA part of <c>--self-test</c>.</summary>
/// <param name="Steps">Steps in execution order.</param>
/// <param name="ReadyAtMs">SPA clock (<c>performance.now()</c>) when the home screen was displayed.</param>
/// <param name="ReportedAtMs">SPA clock when the report was sent: the journey after the home screen is not startup time.</param>
public sealed record SelfTestReport(IReadOnlyList<SelfTestStep> Steps, double ReadyAtMs, double? ReportedAtMs = null);

/// <summary>
/// Receives the SPA's self-test report in the desktop shell. Registered only when the host runs in self-test mode.
/// </summary>
public sealed class SelfTestChannel
{
    private readonly TaskCompletionSource<SelfTestReport> _report = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<SelfTestReport> Report => _report.Task;

    internal bool TryComplete(SelfTestReport report) => _report.TrySetResult(report);
}

public static class SelfTestEndpoints
{
    /// <summary>
    /// <c>--self-test</c> only (404 otherwise). Like every API call in desktop mode it requires the session token,
    /// so it grants nothing beyond a normal start: the report only decides the process exit code.
    /// </summary>
    public static IEndpointRouteBuilder MapSelfTestEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/self-test/report", Results<NoContent, ProblemHttpResult> (
                SelfTestReport report, ErableHostOptions options, IServiceProvider services) =>
            {
                var channel = options.SelfTest ? services.GetService<SelfTestChannel>() : null;
                if (channel is null)
                {
                    return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
                }

                return channel.TryComplete(report)
                    ? TypedResults.NoContent()
                    : TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Report already received.");
            })
            .WithName("ReportSelfTest");
        return endpoints;
    }
}
