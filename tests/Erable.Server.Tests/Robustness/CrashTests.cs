using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Erable.Core.Elements;
using Erable.Storage;
using Erable.Testing;

namespace Erable.Server.Tests.Robustness;

/// <summary>
/// A wiki must survive a power cut (docs/testing.md §6.1): the real server process is killed at random moments in
/// the middle of bursts of writes (pages, new pages, attachments), then the project is examined with the doctor and the
/// server is started again on it. Every write the server acknowledged must still be there, whole; the one that was
/// in flight is there whole or not at all; nothing is left half-written or referenced without its file.
/// </summary>
public sealed class CrashTests
{
    private const string IterationsVariable = "ERABLE_CRASH_ITERATIONS";
    private const string SeedVariable = "ERABLE_CRASH_SEED";
    private const int DefaultIterations = 8;
    private const int Writers = 6;
    private const int InitialPages = 12;

    private sealed class PageState(string id, int owner)
    {
        public string Id { get; } = id;

        /// <summary>The one writer that saves this page: two writers never race on a revision.</summary>
        public int Owner { get; } = owner;

        /// <summary>What the server acknowledged last; the page holds at least this.</summary>
        public string? Acked { get; set; }

        /// <summary>What was being saved when the process died; the page may hold it, whole.</summary>
        public string? Pending { get; set; }

        /// <summary>Revision to send next; -1 when it must be read again.</summary>
        public long Revision { get; set; } = -1;
    }

    private sealed record AttachmentState(string Id, string Sha256);

    private sealed class Model
    {
        public ConcurrentDictionary<string, PageState> Pages { get; } = new(StringComparer.Ordinal);

        public ConcurrentBag<AttachmentState> Attachments { get; } = [];

        public ConcurrentQueue<string> Unexpected { get; } = new();
    }

    [Fact]
    public async Task KillingTheServerDuringWriteBursts_KeepsEveryAcknowledgedWriteAndAHealthyProject()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var iterations = IntFrom(IterationsVariable) ?? DefaultIterations;
        var seed = IntFrom(SeedVariable) ?? Environment.TickCount;
        var random = new Random(seed);
        using var dir = TempDirectory.Create("erable-crash-");
        var data = dir.Combine("data");
        var model = new Model();
        string projectId;
        var killedAfter = new List<int>();

        await using (var first = await ServerProcess.StartAsync(data, cancellationToken))
        {
            projectId = await CreateProjectAsync(first.Http, cancellationToken);
            for (var i = 0; i < InitialPages; i++)
            {
                var page = await CreatePageAsync(first.Http, projectId, $"Initial {i.ToString(CultureInfo.InvariantCulture)}", "initial", i % Writers, cancellationToken);
                model.Pages[page.Id] = page;
            }
        }

        var folder = Directory.GetDirectories(Path.Combine(data, "projects")).Single();
        for (var iteration = 1; iteration <= iterations; iteration++)
        {
            var context = $"iteration {iteration.ToString(CultureInfo.InvariantCulture)}/{iterations.ToString(CultureInfo.InvariantCulture)}, seed {seed.ToString(CultureInfo.InvariantCulture)}";
            await using var server = await ServerProcess.StartAsync(data, cancellationToken);
            await VerifyAcknowledgedAsync(server.Http, projectId, model, context, cancellationToken);

            // The burst runs for a random time, then the process is killed: the cut lands anywhere in a save, an upload or between them.
            var killing = false;
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var writers = Enumerable.Range(0, Writers)
                .Select(w => Task.Run(() => WriteBurstAsync(
                    server.Http, projectId, model, w, new Random(HashCode.Combine(seed, iteration, w)), () => Volatile.Read(ref killing), stop.Token), cancellationToken))
                .ToArray();
            var delay = random.Next(40, 900);
            killedAfter.Add(delay);
            await Task.Delay(delay, cancellationToken);
            Volatile.Write(ref killing, true);
            server.Kill();
            await stop.CancelAsync();
            await Task.WhenAll(writers);

            model.Unexpected.ShouldBeEmpty($"{context}: the server answered with an error before it was killed.");
            AssertDoctorIsSatisfied(folder, context);
        }

        // One last start: everything acknowledged is still served.
        await using var last = await ServerProcess.StartAsync(data, cancellationToken);
        await VerifyAcknowledgedAsync(last.Http, projectId, model, "final restart", cancellationToken);
        killedAfter.Count.ShouldBe(iterations);
    }

    private static async Task WriteBurstAsync(
        HttpClient http, string projectId, Model model, int writer, Random random, Func<bool> killing, CancellationToken cancellationToken)
    {
        var root = $"/api/v1/projects/{projectId}";
        var sequence = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var mine = model.Pages.Values.Where(p => p.Owner == writer).ToList();
            try
            {
                var choice = random.Next(100);
                if (choice < 55 && mine.Count > 0)
                {
                    await SaveAsync(http, root, mine[random.Next(mine.Count)], random, writer, ++sequence, cancellationToken);
                }
                else if (choice < 75)
                {
                    var page = await CreatePageAsync(http, projectId, $"Page {Guid.NewGuid():N}", Body(random, "created"), writer, cancellationToken);
                    model.Pages[page.Id] = page;
                }
                else
                {
                    var target = model.Pages.Values.ElementAt(random.Next(model.Pages.Count));
                    await UploadAsync(http, root, model, target, random, cancellationToken);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
            {
                // The process was killed under this request: the usual end of a burst.
                return;
            }
            catch (UnexpectedResponseException ex) when (!killing())
            {
                model.Unexpected.Enqueue(ex.Message);
                return;
            }
            catch (UnexpectedResponseException)
            {
                return;
            }
        }
    }

    private static async Task SaveAsync(HttpClient http, string root, PageState page, Random random, int writer, int sequence, CancellationToken cancellationToken)
    {
        if (page.Revision < 0)
        {
            using var current = await http.GetAsync(new Uri($"{root}/pages/{page.Id}/content", UriKind.Relative), cancellationToken);
            Ensure(current, "read a page");
            var content = (await current.Content.ReadFromJsonAsync<JsonObject>(cancellationToken))!;
            page.Revision = content["revision"]!.GetValue<long>();
            // What an earlier killed save may have left: it is read, not guessed.
            page.Acked = content["markdown"]!.GetValue<string>();
            page.Pending = null;
        }

        var markdown = Body(random, $"w{writer.ToString(CultureInfo.InvariantCulture)}-{sequence.ToString(CultureInfo.InvariantCulture)}");
        page.Pending = markdown;
        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri($"{root}/pages/{page.Id}/content", UriKind.Relative))
        {
            Content = JsonContent.Create(new { markdown }),
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{page.Revision.ToString(CultureInfo.InvariantCulture)}\"");
        using var response = await http.SendAsync(request, cancellationToken);
        Ensure(response, "save a page");
        var saved = (await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken))!;
        page.Acked = markdown;
        page.Pending = null;
        page.Revision = saved["revision"]!.GetValue<long>();
    }

    private static async Task UploadAsync(HttpClient http, string root, Model model, PageState page, Random random, CancellationToken cancellationToken)
    {
        var bytes = new byte[random.Next(20_000, 400_000)];
        random.NextBytes(bytes);
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", $"data-{Guid.NewGuid():N}.bin");
        using var response = await http.PostAsync(new Uri($"{root}/pages/{page.Id}/attachments", UriKind.Relative), form, cancellationToken);
        Ensure(response, "upload an attachment");
        var item = (await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken))!;
        model.Attachments.Add(new AttachmentState(item["id"]!.GetValue<string>(), Convert.ToHexString(SHA256.HashData(bytes))));
    }

    private static async Task<string> CreateProjectAsync(HttpClient http, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync("/api/v1/projects", new { name = "Crash", contentLanguage = "fr" }, cancellationToken);
        Ensure(response, "create the project");
        return (await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken))!["id"]!.GetValue<string>();
    }

    private static async Task<PageState> CreatePageAsync(HttpClient http, string projectId, string name, string markdown, int owner, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/pages", new { parentId = Roots.Pages.Id.ToString(), name, markdown }, cancellationToken);
        Ensure(response, "create a page");
        var created = (await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken))!;
        return new PageState(created["id"]!.GetValue<string>(), owner) { Acked = markdown };
    }

    /// <summary>After a restart: every acknowledged write is served whole, and an in-flight one is there whole or absent.</summary>
    private static async Task VerifyAcknowledgedAsync(HttpClient http, string projectId, Model model, string context, CancellationToken cancellationToken)
    {
        var root = $"/api/v1/projects/{projectId}";
        foreach (var page in model.Pages.Values)
        {
            using var response = await http.GetAsync(new Uri($"{root}/pages/{page.Id}/content", UriKind.Relative), cancellationToken);
            response.IsSuccessStatusCode.ShouldBeTrue($"{context}: page {page.Id} is gone ({(int)response.StatusCode}).");
            var markdown = (await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken))!["markdown"]!.GetValue<string>();
            (markdown == page.Acked || markdown == page.Pending).ShouldBeTrue(
                $"{context}: page {page.Id} holds neither the last acknowledged text nor the one in flight (length {markdown.Length.ToString(CultureInfo.InvariantCulture)}).");
            page.Acked = markdown;
            page.Pending = null;
            page.Revision = -1;
        }

        foreach (var attachment in model.Attachments)
        {
            using var response = await http.GetAsync(new Uri($"{root}/attachments/{attachment.Id}", UriKind.Relative), cancellationToken);
            response.IsSuccessStatusCode.ShouldBeTrue($"{context}: attachment {attachment.Id} is gone ({(int)response.StatusCode}).");
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            Convert.ToHexString(SHA256.HashData(bytes)).ShouldBe(attachment.Sha256, $"{context}: attachment {attachment.Id} is not what was uploaded.");
        }
    }

    /// <summary>
    /// The dead process may leave an upload's temporary file or a file no attachment uses yet (it is written before it is
    /// recorded): warnings the doctor repairs. Anything else — an integrity error, a record without its file — is corruption.
    /// </summary>
    private static void AssertDoctorIsSatisfied(string folder, string context)
    {
        var report = ProjectDoctor.Diagnose(folder, fix: false);
        var corruption = report.Findings
            .Where(f => f.Severity != DoctorSeverity.Info && f.Code is not (DoctorCodes.BlobOrphan or DoctorCodes.BlobTemporary))
            .Select(f => $"{f.Code}: {f.Message}")
            .ToList();
        corruption.ShouldBeEmpty($"{context}: erable doctor found corruption.");

        ProjectDoctor.Diagnose(folder, fix: true).Healthy.ShouldBeTrue($"{context}: the doctor could not repair the leftovers.");
        ProjectDoctor.Diagnose(folder, fix: false).Healthy.ShouldBeTrue($"{context}: the project is not healthy after the repair.");
    }

    /// <summary>A text of a few kilobytes, different at each call, so that a half-written save cannot look like a whole one.</summary>
    private static string Body(Random random, string label)
    {
        var token = random.NextInt64().ToString("x", CultureInfo.InvariantCulture);
        return $"# {label}\n\n" + string.Concat(Enumerable.Repeat(token + " ", 400)) + $"\n\nfin {token}";
    }

    private static void Ensure(HttpResponseMessage response, string what)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new UnexpectedResponseException($"Could not {what}: {(int)response.StatusCode} {response.ReasonPhrase}");
        }
    }

    private static int? IntFrom(string variable) =>
        int.TryParse(Environment.GetEnvironmentVariable(variable), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    private sealed class UnexpectedResponseException(string message) : Exception(message);
}
