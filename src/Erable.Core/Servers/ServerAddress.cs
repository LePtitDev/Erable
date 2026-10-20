using System.Text.Json;

namespace Erable.Core.Servers;

/// <summary>
/// The address of an Erable server as its clients take it from a person: the desktop in connected mode and the
/// Android application (F19.R1). Only the origin is kept: the SPA and the API live at the root of a server.
/// </summary>
public static class ServerAddress
{
    public const string InvalidUrl = "server.invalidUrl";

    /// <summary>The origin of a typed address (<c>wiki.example.org/</c> → <c>https://wiki.example.org</c>).</summary>
    public static Result<string> Normalize(string? address)
    {
        var text = address?.Trim() ?? string.Empty;
        if (text.Length > 0 && !text.Contains("://", StringComparison.Ordinal))
        {
            text = "https://" + text;
        }

        return Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https"
            && uri.UserInfo.Length == 0
            && uri.Host.Length > 0
            ? uri.GetLeftPart(UriPartial.Authority)
            : new DomainError(InvalidUrl, "Give the address of the server, e.g. https://wiki.example.org.");
    }

    /// <summary>Whether a normalized origin is reached without TLS (a server of the local network, F19.R3).</summary>
    public static bool IsCleartext(string origin) => origin.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// What <c>GET /api/info</c> tells a client before anything else (F16.R4, F19.R1). Read by hand: a newer server may add
/// or change fields, and a missing <c>apiVersion</c> means it is not an Erable server.
/// </summary>
public sealed record ServerHandshake(string AppVersion, string ApiVersion, int FormatVersion, string? Mode)
{
    /// <summary>A hosted server (not the loopback backend of a desktop application).</summary>
    public bool IsServerMode => Mode == "server";

    /// <summary>The handshake of an answer, or null when it does not come from an Erable server.</summary>
    public static ServerHandshake? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return Parse(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static ServerHandshake? Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("apiVersion", out var api)
            || api.ValueKind != JsonValueKind.String
            || api.GetString() is not { Length: > 0 } apiVersion)
        {
            return null;
        }

        return new ServerHandshake(
            StringOf(root, "appVersion") ?? "?",
            apiVersion,
            // TryGetInt32 throws on anything but a number: check the kind first.
            root.TryGetProperty("formatVersion", out var format)
                && format.ValueKind == JsonValueKind.Number
                && format.TryGetInt32(out var number) ? number : 0,
            StringOf(root, "mode"));
    }

    private static string? StringOf(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
