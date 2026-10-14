using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using Xunit.Sdk;

namespace Erable.Testing;

/// <summary>
/// Runner for the specification shared with the TypeScript implementation (docs/testing.md §5).
/// Every <c>*.json</c> file under <c>spec/fixtures/&lt;folder&gt;</c> has the shape
/// <c>{ "description": "…", "cases": [ { "name": "…", … } ] }</c>; adding a case means adding a JSON object.
/// The same files are read by <c>src/web/src/test/fixtures.ts</c>.
/// </summary>
/// <example>
/// <code>
/// public static TheoryData&lt;FixtureCase&gt; Cases => SharedFixtures.Load("ids");
/// [Theory, MemberData(nameof(Cases))]
/// public void Parse_SharedCases(FixtureCase fixture) { var c = fixture.As&lt;IdCase&gt;(); … }
/// </code>
/// </example>
public static class SharedFixtures
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Directory(string folder) => RepositoryPaths.Combine("spec", "fixtures", folder);

    public static TheoryData<FixtureCase> Load(string folder)
    {
        var root = Directory(folder);
        var data = new TheoryData<FixtureCase>();
        foreach (var file in System.IO.Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var document = JsonNode.Parse(File.ReadAllText(file))
                ?? throw new InvalidDataException($"{relative}: empty fixture file.");
            var cases = document["cases"]?.AsArray()
                ?? throw new InvalidDataException($"{relative}: missing \"cases\" array.");
            foreach (var item in cases)
            {
                var name = item?["name"]?.GetValue<string>()
                    ?? throw new InvalidDataException($"{relative}: every case needs a \"name\".");
                data.Add(new FixtureCase(relative, name, item.ToJsonString()));
            }
        }

        if (data.Count == 0)
        {
            throw new InvalidDataException($"No fixture case found under {root}.");
        }

        return data;
    }
}

/// <summary>One shared case; serializable so that each case is a separate test in the runner.</summary>
public sealed class FixtureCase : IXunitSerializable
{
    public FixtureCase(string file, string name, string json)
    {
        File = file;
        Name = name;
        Json = json;
    }

    [Obsolete("For xUnit serialization only.")]
    public FixtureCase()
    {
        File = Name = Json = string.Empty;
    }

    public string File { get; private set; }

    public string Name { get; private set; }

    public string Json { get; private set; }

    public T As<T>() =>
        JsonSerializer.Deserialize<T>(Json, SharedFixtures.JsonOptions)
        ?? throw new InvalidDataException($"{File} › {Name}: cannot read the case as {typeof(T).Name}.");

    public void Deserialize(IXunitSerializationInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        File = info.GetValue<string>(nameof(File)) ?? string.Empty;
        Name = info.GetValue<string>(nameof(Name)) ?? string.Empty;
        Json = info.GetValue<string>(nameof(Json)) ?? string.Empty;
    }

    public void Serialize(IXunitSerializationInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        info.AddValue(nameof(File), File);
        info.AddValue(nameof(Name), Name);
        info.AddValue(nameof(Json), Json);
    }

    public override string ToString() => $"{File} › {Name}";
}
