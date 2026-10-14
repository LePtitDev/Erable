using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Erable.Storage;

namespace Erable.Server.Cli;

/// <summary><c>erable doctor &lt;folder&gt; [--json] [--fix]</c>: integrity diagnosis of a closed project (docs/testing.md §6).</summary>
public static class DoctorCommand
{
    /// <summary>No anomaly left (infos aside).</summary>
    public const int Healthy = 0;

    /// <summary>At least one warning or error remains.</summary>
    public const int Anomalies = 1;

    /// <summary>The project could not be examined (missing manifest, locked by a running instance).</summary>
    public const int Unreadable = 2;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static int Run(string folder, bool json, bool fix, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var report = ProjectDoctor.Diagnose(folder, fix);
        if (json)
        {
            output.WriteLine(JsonSerializer.Serialize(new { report.Folder, report.FormatVersion, report.Healthy, report.Findings }, Json));
        }
        else
        {
            WriteText(report, output);
        }

        if (report.Findings.Any(f => f.Code is DoctorCodes.ManifestInvalid or DoctorCodes.ProjectLocked))
        {
            return Unreadable;
        }

        return report.Healthy ? Healthy : Anomalies;
    }

    private static void WriteText(DoctorReport report, TextWriter output)
    {
        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{report.Folder} (format {report.FormatVersion?.ToString(CultureInfo.InvariantCulture) ?? "?"})"));
        foreach (var finding in report.Findings)
        {
            var subject = string.Join(' ', new[] { finding.ElementId?.ToString(), finding.Blob?[..12] }.Where(s => s is not null));
            var state = finding.Fixed ? " [fixed]" : finding.Fixable ? " [--fix]" : string.Empty;
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  {finding.Severity.ToString().ToLowerInvariant(),-7} {finding.Code,-24} {subject,-30} {finding.Message}{state}"));
        }

        output.WriteLine(report.Healthy ? "No anomaly." : $"{report.Findings.Count(f => !f.Fixed && f.Severity != DoctorSeverity.Info)} anomalies.");
    }
}
