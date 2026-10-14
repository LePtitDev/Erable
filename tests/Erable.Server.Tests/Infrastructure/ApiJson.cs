using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Erable.Core;
using Erable.Server.Api;

namespace Erable.Server.Tests.Infrastructure;

/// <summary>JSON helpers matching the API settings (camelCase, enums as camelCase strings, IDs as strings).</summary>
public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var body = await response.Content.ReadFromJsonAsync<T>(Options, TestContext.Current.CancellationToken);
        return body ?? throw new InvalidOperationException("Empty body.");
    }

    /// <summary>The <c>code</c> extension of a ProblemDetails response.</summary>
    public static async Task<string?> ProblemCodeAsync(this HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var node = await response.Content.ReadFromJsonAsync<JsonObject>(TestContext.Current.CancellationToken);
        return node?["code"]?.GetValue<string>();
    }

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(url, body, Options, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> PatchJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PatchAsJsonAsync(url, body, Options, TestContext.Current.CancellationToken);

    /// <summary>Creates and opens a project in a fresh folder under the backend's data directory.</summary>
    public static async Task<ProjectSummary> CreateProjectAsync(this TestBackend backend, string name = "Atlas", string language = "fr")
    {
        ArgumentNullException.ThrowIfNull(backend);
        var folder = Path.Combine(backend.Options.DataDirectory!, "projects", Guid.NewGuid().ToString("N"));
        using var response = await backend.Client.PostJsonAsync("/api/v1/projects", new CreateProjectRequest(folder, name, language));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<ProjectSummary>();
    }

    public static async Task<ElementDetails> CreatePageAsync(
        this TestBackend backend, ElementId projectId, ElementId parentId, string name, string? markdown = null)
    {
        ArgumentNullException.ThrowIfNull(backend);
        using var response = await backend.Client.PostJsonAsync(
            $"/api/v1/projects/{projectId}/pages", new CreatePageRequest(parentId, name, null, markdown));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<ElementDetails>();
    }

    public static async Task<ElementDetails> CreateFolderAsync(this TestBackend backend, ElementId projectId, ElementId parentId, string name)
    {
        ArgumentNullException.ThrowIfNull(backend);
        using var response = await backend.Client.PostJsonAsync(
            $"/api/v1/projects/{projectId}/folders", new CreateFolderRequest(parentId, name));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<ElementDetails>();
    }
}
