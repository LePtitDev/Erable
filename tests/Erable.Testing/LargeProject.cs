using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Erable.Core;
using Erable.Core.Elements;
using Erable.Core.Projects;
using Erable.Storage;

namespace Erable.Testing;

/// <summary>What a large project holds (docs/testing.md §4.3, §11.1: 10 000 pages, 300 categories, 50 000 references, 2 GB).</summary>
/// <param name="AttachmentsBytes">Total size of the attachments (0 for none); the light CI option uses a few MB.</param>
/// <param name="AttachmentSize">Size of one attachment: the count is <c>AttachmentsBytes / AttachmentSize</c>.</param>
public sealed record LargeProjectOptions(
    int Pages = 10_000,
    int Categories = 300,
    int References = 50_000,
    long AttachmentsBytes = 2L * 1024 * 1024 * 1024,
    int Seed = 42,
    int AttachmentSize = 1024 * 1024)
{
    public static LargeProjectOptions Light { get; } = new(AttachmentsBytes: 16L * 1024 * 1024, AttachmentSize: 64 * 1024);
}

/// <summary>
/// Generator of large projects with a fixed seed (M3-06): the same options always give the same project, down to the
/// IDs, so that measurements compare from one run to the next. Pages, categories, properties with rules, values,
/// references, modification dates over the last 90 days, and attachments of pseudo-random content.
/// </summary>
public static class LargeProject
{
    private static readonly string[] Words =
    [
        "montagne", "village", "caravane", "givre", "sentier", "marchand", "garde", "ordre", "carte", "tempête", "refuge",
        "dragon", "quête", "lanterne", "col", "vallée", "rivière", "forge", "archives", "serment", "ombre", "lumière",
        "pierre", "hiver", "auberge", "pont", "tour", "frontière", "clan", "légende", "relevé", "passage", "falaise",
        "brume", "cloche", "guilde", "voyageur", "mémoire", "cendre", "étoile",
    ];

    private static readonly string[] Roles = ["marchande", "garde", "erudit"];

    private static readonly DateTimeOffset Now = new(2026, 10, 13, 8, 0, 0, TimeSpan.Zero);

    /// <summary>Writes the project into <paramref name="folder"/> and returns it open (the caller disposes it).</summary>
    public static OpenedProject Generate(string folder, LargeProjectOptions options, Action<string>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        var random = new Random(options.Seed);
        var builder = Project.Build().WithIdFactory(StableId);

        progress?.Invoke("structure");
        const int folders = 100;
        for (var f = 0; f < folders; f++)
        {
            builder.WithFolder($"Dossier {f:D3}");
        }

        builder.WithFolder("Propriétés de test", root: ElementKind.PropertyDefinition);
        builder.WithProperty("Niveau", under: "Propriétés de test", type: "int", rules: """[{"op":"between","value":[1,60]}]""", propertyKey: "niveau");
        builder.WithProperty("Hostile", under: "Propriétés de test", type: "bool", propertyKey: "hostile");
        builder.WithProperty("Âge", under: "Propriétés de test", type: "int", rules: """[{"op":"greater_or_equal","value":0}]""", propertyKey: "age");
        builder.WithProperty("Rôle", under: "Propriétés de test", type: "enum",
            options: [("marchande", "Marchande"), ("garde", "Garde"), ("erudit", "Érudit")], propertyKey: "role");
        builder.WithProperty("Surnom", under: "Propriétés de test", type: "string", propertyKey: "surnom");

        // Categories: a few roots, the others inherit from an earlier one (depth grows slowly, as in a real wiki).
        var categoryNames = new List<string>();
        for (var c = 0; c < options.Categories; c++)
        {
            var name = $"Catégorie {c:D3}";
            var parent = c < 12 ? null : categoryNames[random.Next(c)];
            var values = c % 5 == 0
                ? new Dictionary<string, string> { ["Hostile"] = c % 10 == 0 ? "true" : "false", ["Niveau"] = (5 + c % 40).ToString(System.Globalization.CultureInfo.InvariantCulture) }
                : new Dictionary<string, string>();
            builder.WithCategory(name, parent: parent, description: $"Catégorie de test {Phrase(random, 6)}.", values: values);
            categoryNames.Add(name);
        }

        progress?.Invoke("pages");
        var names = Enumerable.Range(0, options.Pages).Select(i => $"Page {i:D5} {Words[i % Words.Length]}").ToArray();
        var references = new int[options.Pages];
        for (var r = 0; r < options.References && options.Pages > 0; r++)
        {
            references[random.Next(options.Pages)]++;
        }

        for (var i = 0; i < options.Pages; i++)
        {
            var targets = Enumerable.Range(0, references[i]).Select(_ => names[random.Next(options.Pages)]).ToList();
            builder.WithPage(names[i], under: $"Dossier {i % folders:D3}", markdown: Body(random, names[i], targets));
            var updated = Now - TimeSpan.FromMinutes(random.Next(90 * 24 * 60));
            builder.Stamped(names[i], updated - TimeSpan.FromDays(30), "camille", updated, random.Next(3) == 0 ? "malo" : "camille");
            var categories = Enumerable.Range(0, 1 + random.Next(3)).Select(_ => categoryNames[random.Next(categoryNames.Count)]).Distinct().ToList();
            var values = new Dictionary<string, string>();
            if (random.Next(10) < 7)
            {
                values["Niveau"] = random.Next(1, 100).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            if (random.Next(10) < 3)
            {
                values["Rôle"] = JsonSerializer.Serialize(Roles[random.Next(Roles.Length)]);
            }

            if (random.Next(10) < 2)
            {
                values["Surnom"] = JsonSerializer.Serialize($"Le {Words[random.Next(Words.Length)]}");
            }

            builder.Classified(names[i], categories, values);
        }

        progress?.Invoke("writing");
        var project = ProjectSeeder.Create(builder, folder, "Grand projet", "fr", ElementId.Parse("1A29E00000000001"), Now);
        if (options.AttachmentsBytes > 0)
        {
            progress?.Invoke("attachments");
            AddAttachments(project, options, names, random);
        }

        return project;
    }

    private static void AddAttachments(OpenedProject project, LargeProjectOptions options, string[] pageNames, Random random)
    {
        var service = new ProjectService(project, new FixedClock(Now));
        var count = (int)Math.Max(1, options.AttachmentsBytes / options.AttachmentSize);
        var ids = pageNames.Select(StableId).ToArray();
        var buffer = new byte[options.AttachmentSize];
        for (var a = 0; a < count; a++)
        {
            // Pseudo-random bytes seeded per file: distinct hashes (no deduplication), same content on every run.
            new Random(options.Seed + a + 1).NextBytes(buffer);
            using var stream = new MemoryStream(buffer, writable: false);
            var page = ids[random.Next(ids.Length)];
            var added = service.AddAttachmentAsync(page, $"piece_{a:D5}.bin", stream, long.MaxValue, KnownUsers.Local)
                .GetAwaiter().GetResult();
            if (!added.IsSuccess)
            {
                throw new InvalidOperationException($"Attachment {a}: {added.Error.Code}.");
            }
        }
    }

    private static string Body(Random random, string title, List<string> targets)
    {
        var text = new StringBuilder();
        text.Append(CultureInfoText(Phrase(random, 18))).Append(".\n\n");
        var sections = 2 + random.Next(3);
        for (var s = 0; s < sections; s++)
        {
            text.Append("## ").Append(CultureInfoText(Phrase(random, 3))).Append("\n\n");
            var paragraphs = 1 + random.Next(2);
            for (var p = 0; p < paragraphs; p++)
            {
                text.Append(Phrase(random, 30 + random.Next(30))).Append(". ");
                // References are spread over the paragraphs.
                var take = Math.Min(targets.Count, 1 + random.Next(3));
                for (var t = 0; t < take; t++)
                {
                    text.Append("Voir [[@").Append(targets[0]).Append("]]. ");
                    targets.RemoveAt(0);
                }

                text.Append("\n\n");
            }
        }

        foreach (var target in targets)
        {
            text.Append("- [[@").Append(target).Append("]]\n");
        }

        return text.ToString().Replace("{title}", title, StringComparison.Ordinal);
    }

    private static string CultureInfoText(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static string Phrase(Random random, int words) =>
        string.Join(' ', Enumerable.Range(0, words).Select(_ => Words[random.Next(Words.Length)]));

    /// <summary>Derived from the name: the same project on every run.</summary>
    public static ElementId StableId(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("large/" + name));
        return new ElementId(BitConverter.ToUInt64(hash, 0) | 0x8000_0000_0000_0000UL);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
