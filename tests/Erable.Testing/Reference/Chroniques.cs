using System.Security.Cryptography;
using System.Text;
using Erable.Core;
using Erable.Storage;

namespace Erable.Testing.Reference;

/// <summary>
/// The reference project « Chroniques d'Aube » (docs/features.md §2), written to <c>spec/projects/chroniques/</c> by
/// <see cref="Write"/>. The IDs of docs/features.md §2.1 are frozen; the others are derived from the element names,
/// so that regenerating the project after a format migration keeps every ID. Categories, properties and links join
/// with M2 (in M1 they are bare elements of the tree).
/// </summary>
public static class Chroniques
{
    public const string Name = "Chroniques d'Aube";
    public const string FolderName = "chroniques";

    public static readonly ElementId ProjectId = ElementId.Parse("CA0B0E5EC0A5D1E1");

    public static readonly ElementId Ilsa = ElementId.Parse("3F2A9C14E07B5D21");
    public static readonly ElementId Orsen = ElementId.Parse("7A1C4E90B3F2D615");
    public static readonly ElementId Dragon = ElementId.Parse("C47D2B9E0F6A1388");
    public static readonly ElementId Hautbois = ElementId.Parse("5B19C0E2A4D7F311");
    public static readonly ElementId ColGeleLieu = ElementId.Parse("8C3E1F7A90B2D446");
    public static readonly ElementId OrdreDuGivre = ElementId.Parse("9E04B6A1C8D2F570");
    public static readonly ElementId ColGeleQuete = ElementId.Parse("2D8F5A7C1B9E0463");

    public static readonly ElementId Entite = ElementId.Parse("E1A0000000000001");
    public static readonly ElementId Personnage = ElementId.Parse("E1A0000000000002");
    public static readonly ElementId Pnj = ElementId.Parse("0D7EA95162C3B804");
    public static readonly ElementId Niveau = ElementId.Parse("44B107E9C2A63F58");

    private static readonly string[] StructureKeys =
    [
        "Entité", "Personnage", "Catégorie PNJ", "Marchand", "Garde", "Érudit", "Joueur", "Créature", "Catégorie Région",
        "Quête", "Faction", "Propriétés/Personnages", "Niveau", "Rôle", "Âge", "Points de vie", "Hostile", "Monde",
        "Propriété Région", "Réputation", "Allié de", "Situé à", "Mentor de",
    ];

    /// <summary>Test clock: 13 October 2026, 10:00 Europe/Paris (docs/features.md §1.4).</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 13, 8, 0, 0, TimeSpan.Zero);

    public static string AssetsDirectory => RepositoryPaths.Combine("spec", "projects", "assets");

    public static string CommittedDirectory => RepositoryPaths.Combine("spec", "projects", FolderName);

    /// <summary>ID of an element of the reference project, by name (frozen or derived).</summary>
    public static ElementId IdOf(string name) => Describe().IdOf(name);

    /// <summary>Writes the project into <paramref name="folder"/> and returns it open.</summary>
    public static OpenedProject Write(string folder) =>
        ProjectSeeder.Create(Describe(), folder, Name, "fr", ProjectId, Date(2026, 3, 10, 9, 0));

    public static ProjectBuilder Describe()
    {
        var p = Project.Build().WithIdFactory(StableId);

        p.WithFolder("Personnages").Stamped("Personnages", Date(2026, 3, 10, 9, 0), "camille");
        p.WithFolder("PNJ", under: "Personnages").Stamped("PNJ", Date(2026, 3, 10, 9, 1), "camille");
        p.WithPage("Ilsa Varenne", under: "PNJ", id: Ilsa.ToString(), markdown: IlsaMarkdown)
            .Stamped("Ilsa Varenne", Date(2026, 3, 12, 10, 30), "camille", Now.AddHours(-2), "camille");
        p.WithPage("Dialogues", under: "Ilsa Varenne", markdown: """
            Répliques d’Ilsa selon la réputation du joueur auprès de l’Ordre.

            - **Méfiante** : « Les cartes ne se donnent pas, voyageur. »
            - **Amicale** : « Le Col ne pardonne pas. Prenez celle-ci, elle est à jour. »
            """).Stamped("Dialogues", Date(2026, 3, 20, 14, 0), "camille", Date(2026, 9, 2, 16, 0), "malo");
        p.WithPage("Inventaire", under: "Ilsa Varenne", markdown: """
            | Objet | Prix |
            |---|---|
            | Carte du Col gelé | 40 pièces |
            | Corde de montagne | 12 pièces |
            | Lanterne | 8 pièces |
            """).Stamped("Inventaire", Date(2026, 3, 20, 14, 5), "camille", Date(2026, 8, 18, 11, 0), "camille");
        p.WithPage("Capitaine Orsen", under: "PNJ", id: Orsen.ToString(), markdown: """
            Commandant de la garnison de Hautbois, ancien camarade d’Ilsa au sein de l’Ordre.

            ## Présentation

            Orsen doit la vie à [[@Ilsa Varenne]], qui l’a guidé à travers le Col lors de la Longue Nuit. Il tient
            la garnison d’une main ferme et se méfie des marchands.
            """).Stamped("Capitaine Orsen", Date(2026, 3, 14, 11, 0), "malo", Date(2026, 10, 10, 9, 15), "malo");
        p.WithFolder("Créatures", under: "Personnages").Stamped("Créatures", Date(2026, 3, 10, 9, 2), "camille");
        p.WithPage("Dragon des Cimes", under: "Créatures", id: Dragon.ToString(), markdown: """
            Gardien des sommets au-delà du Col gelé. On dit qu’il ne descend que les nuits sans lune.
            """).Stamped("Dragon des Cimes", Date(2026, 4, 2, 15, 0), "camille", Date(2026, 9, 30, 17, 40), "camille");

        p.WithFolder("Lieux").Stamped("Lieux", Date(2026, 3, 10, 9, 3), "camille");
        p.WithPage("Hautbois", under: "Lieux", id: Hautbois.ToString(), markdown: """
            Bourg marchand au pied du [[@Col gelé]], dernier refuge avant la montagne.

            ![[@carte_hautbois.webp|Carte de Hautbois]]

            ## Lieux notables

            - Le comptoir d’échange de [[@Ilsa Varenne]].
            - La garnison du bourg.
            """).Stamped("Hautbois", Date(2026, 3, 10, 10, 0), "malo", Date(2026, 10, 12, 15, 0), "malo");
        p.WithAttachment("carte_hautbois.webp", "Hautbois", Asset("carte_hautbois.webp"))
            .Stamped("carte_hautbois.webp", Date(2026, 3, 10, 10, 5), "malo");
        p.WithAttachment("bande_annonce.mp4", "Hautbois", Asset("bande_annonce.mp4"))
            .Stamped("bande_annonce.mp4", Date(2026, 6, 1, 8, 0), "malo");
        p.WithAttachment("plans.zip", "Hautbois", Asset("plans.zip"))
            .Stamped("plans.zip", Date(2026, 6, 1, 8, 1), "malo");
        p.WithAttachment("blason.svg", "Hautbois", Asset("blason.svg"))
            .Stamped("blason.svg", Date(2026, 6, 1, 8, 2), "malo");
        p.WithPage("Col gelé", under: "Lieux", id: ColGeleLieu.ToString(), markdown: """
            Passage de haute montagne entre Hautbois et les terres du nord, fermé une bonne partie de l’hiver.
            """).Stamped("Col gelé", Date(2026, 3, 10, 10, 30), "malo", Date(2026, 10, 1, 13, 0), "malo");

        p.WithFolder("Factions").Stamped("Factions", Date(2026, 3, 10, 9, 4), "camille");
        p.WithPage("Ordre du Givre", under: "Factions", id: OrdreDuGivre.ToString(), markdown: """
            Ordre de cartographes et de gardiens du Col, installé à Hautbois depuis deux siècles.
            """).Stamped("Ordre du Givre", Date(2026, 3, 11, 9, 0), "camille", Date(2026, 9, 20, 10, 0), "camille");

        p.WithFolder("Quêtes").Stamped("Quêtes", Date(2026, 3, 10, 9, 5), "camille");
        p.WithPage("Le Col gelé", under: "Quêtes", id: ColGeleQuete.ToString(), markdown: """
            Le joueur doit franchir le col gelé avant la tempête.

            ## Déroulement

            1. Acheter une carte du col gelé à [[@Ilsa Varenne]].
            2. Traverser le col gelé de nuit, en évitant le [[@Dragon des Cimes]].
            """).Stamped("Le Col gelé", Date(2026, 5, 5, 9, 0), "camille", Date(2026, 10, 12, 9, 0), "camille");

        p.WithAttachment("portrait_ilsa.png", "Ilsa Varenne", Asset("portrait_ilsa.png"))
            .Stamped("portrait_ilsa.png", Date(2026, 3, 12, 11, 0), "malo");

        // Structure (docs/features.md §2.1, §2.2). Categories are flat in the tree: their inheritance is an attribute of
        // the category. Keys disambiguate the names used twice (« Région », « Personnages », « PNJ »).
        // Properties come first: category defaults refer to them.
        p.WithFolder("Personnages", under: "Propriétés", key: "Propriétés/Personnages");
        p.WithProperty("Niveau", under: "Propriétés/Personnages", id: Niveau.ToString(), type: "int",
            rules: """[{"op":"greater_or_equal","value":1},{"op":"less_or_equal","value":60}]""",
            description: "Niveau de puissance du personnage, de 1 à 60.");
        p.WithProperty("Rôle", under: "Propriétés/Personnages", type: "enum",
            options: [("marchande", "Marchande"), ("garde", "Garde"), ("erudit", "Érudit"), ("voyageur", "Voyageur")],
            description: "Fonction du personnage dans le monde.");
        p.WithProperty("Âge", under: "Propriétés/Personnages", type: "int", rules: """[{"op":"greater_or_equal","value":0}]""");
        p.WithProperty("Points de vie", under: "Propriétés/Personnages", type: "int", rules: """[{"op":"greater_than","value":0}]""");
        p.WithProperty("Hostile", under: "Propriétés/Personnages", type: "bool", description: "Attaque le joueur à vue.");
        p.WithFolder("Monde", under: "Propriétés");
        p.WithProperty("Région", under: "Monde", key: "Propriété Région", type: "ref",
            rules: """[{"op":"in_category","value":"@Catégorie Région"}]""", description: "Lieu où l'on trouve l'élément.");
        p.WithProperty("Réputation", under: "Monde", type: "int", rules: """[{"op":"between","value":[-100,100]}]""");

        p.WithCategory("Entité", id: Entite.ToString(), description: "Tout ce qui vit dans le monde.");
        p.WithCategory("Personnage", id: Personnage.ToString(), parent: "Entité", description: "Être doué de parole.");
        p.WithCategory("PNJ", id: Pnj.ToString(), key: "Catégorie PNJ", parent: "Personnage",
            description: "Personnage non joueur : contrôlé par le meneur de jeu.",
            values: new Dictionary<string, string> { ["Hostile"] = "false", ["Niveau"] = "10" });
        p.WithCategory("Marchand", parent: "Catégorie PNJ", description: "PNJ qui vend ou achète des objets.");
        p.WithCategory("Garde", parent: "Catégorie PNJ", description: "PNJ chargé de la sécurité d'un lieu.");
        p.WithCategory("Érudit", parent: "Catégorie PNJ", description: "PNJ savant, source d'informations.");
        p.WithCategory("Joueur", parent: "Personnage", description: "Personnage incarné par un joueur.",
            values: new Dictionary<string, string> { ["Niveau"] = "1" });
        p.WithCategory("Créature", parent: "Entité", description: "Être sauvage ou monstrueux.",
            values: new Dictionary<string, string> { ["Niveau"] = "20", ["Hostile"] = "true" });
        p.WithCategory("Région", key: "Catégorie Région", description: "Lieu du monde.");
        p.WithCategory("Quête", description: "Mission proposée au joueur.");
        p.WithCategory("Faction", description: "Groupe organisé.");

        p.Classified("Ilsa Varenne", ["Catégorie PNJ", "Marchand"], new Dictionary<string, string>
        {
            ["Niveau"] = "72",
            ["Rôle"] = "\"marchande\"",
            ["Propriété Région"] = "\"@Hautbois\"",
        });
        p.Classified("Capitaine Orsen", ["Catégorie PNJ", "Garde"], new Dictionary<string, string>
        {
            ["Niveau"] = "40",
            ["Rôle"] = "\"garde\"",
            ["Propriété Région"] = "\"@Hautbois\"",
        });
        p.Classified("Dragon des Cimes", ["Créature"], new Dictionary<string, string> { ["Niveau"] = "99" });
        p.Classified("Hautbois", ["Catégorie Région"]);
        p.Classified("Col gelé", ["Catégorie Région"]);
        p.Classified("Le Col gelé", ["Quête"]);
        p.Classified("Ordre du Givre", ["Faction"]);
        p.WithLinkType("Allié de", description: "Deux personnages qui se soutiennent.", inverse: "Allié de");
        p.WithLinkType("Situé à", description: "Un élément qui se trouve en un lieu.", inverse: "Lieu de");
        p.WithLinkType("Mentor de", description: "Un personnage qui forme un autre.", inverse: "Élève de");
        p.WithLink("Allié de", "Ilsa Varenne", "Capitaine Orsen");
        p.WithLink("Situé à", "Ilsa Varenne", "Hautbois");
        foreach (var key in StructureKeys)
        {
            p.Stamped(key, Date(2026, 3, 10, 9, 30), "camille");
        }

        return p;
    }

    /// <summary>Lead text, sections and embeds of the mockup « Page — lecture ».</summary>
    private const string IlsaMarkdown = """
        Ilsa Varenne tient le comptoir d’échange de [[@Hautbois]], au pied du Col gelé. Ancienne cartographe de l’[[@Ordre du Givre]], elle connaît chaque sentier de la région et vend ses cartes à qui sait poser les bonnes questions.

        ## Présentation

        Méfiante envers les soldats mais loyale envers ceux qui l’ont aidée, Ilsa partage un passé commun avec le [[@Capitaine Orsen]], qu’elle a guidé à travers le Col lors de la Longue Nuit.

        ![[@portrait_ilsa.png|Portrait par Malo L.]]

        ![[@carte_hautbois.webp]]

        ## Histoire

        Née à Hautbois, Ilsa rejoint l’Ordre à seize ans comme apprentie. Elle quitte l’Ordre après l’incendie des archives, convaincue que ses supérieurs l’ont provoqué.

        ### Les années de cartographe

        Ses relevés du Col gelé restent les seuls fiables. Le joueur peut les acheter dès la quête [[@Le Col gelé]].

        ## Rôle dans le jeu

        - Marchande de cartes et d’équipement de montagne.
        - Donneuse de quêtes secondaires dans la région de Hautbois.
        - Peut devenir hostile si le joueur trahit l’Ordre.

        > **Note de conception** — Le prix des cartes dépend de la propriété Réputation du joueur auprès de l’Ordre.
        """;

    /// <summary>Derived from the name: stable across regenerations, never in the reserved range.</summary>
    public static ElementId StableId(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("chroniques/" + name));
        var value = BitConverter.ToUInt64(hash, 0) | 0x8000_0000_0000_0000UL;
        return new ElementId(value);
    }

    private static byte[] Asset(string fileName) => File.ReadAllBytes(Path.Combine(AssetsDirectory, fileName));

    private static DateTimeOffset Date(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, TimeSpan.Zero);
}
