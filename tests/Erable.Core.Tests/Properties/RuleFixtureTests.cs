using System.Text.Json;
using System.Text.Json.Nodes;
using Erable.Core.Elements;
using Erable.Core.Properties;
using Erable.Testing;

namespace Erable.Core.Tests.Properties;

/// <summary>spec/fixtures/rules, shared with src/web/src/shared/properties/rules.fixtures.test.ts (docs/testing.md §5).</summary>
public sealed class RuleFixtureTests
{
    public static TheoryData<FixtureCase> EvaluationCases => Only("evaluation.json");

    public static TheoryData<FixtureCase> DefinitionCases => Only("definitions.json");

    public static TheoryData<FixtureCase> ValueCases => Only("values.json");

    public static TheoryData<FixtureCase> RegexCases => Only("regex.json");

    [Theory]
    [MemberData(nameof(EvaluationCases))]
    public void Evaluate_SharedCases_ReportsExpectedViolations(FixtureCase fixture)
    {
        using var document = JsonDocument.Parse(fixture.Json);
        var c = document.RootElement;
        var type = PropertyType.Parse(c.GetProperty("type").GetString()).Value;
        var options = Options(c);
        var rules = ValidationRules.ListFromJson(c.GetProperty("rules"), type, options).Value;
        var value = PropertyValues.FromJson(c.GetProperty("value"), type, options).Value;
        var context = c.TryGetProperty("context", out var ctx) ? new FixtureContext(ctx) : null;

        var actual = RuleEvaluator.Evaluate(rules, value, context)
            .Select(v => v.Index is { } index ? $"{ValidationRules.OpName(v.Rule.Op)}#{index}" : ValidationRules.OpName(v.Rule.Op));

        var expected = c.GetProperty("violations").EnumerateArray()
            .Select(v => v.TryGetProperty("index", out var i) ? $"{v.GetProperty("op").GetString()}#{i.GetInt32()}" : v.GetProperty("op").GetString());
        actual.ShouldBe(expected);
    }

    [Theory]
    [MemberData(nameof(DefinitionCases))]
    public void ReadRule_SharedCases_AcceptsOrRefuses(FixtureCase fixture)
    {
        using var document = JsonDocument.Parse(fixture.Json);
        var c = document.RootElement;
        var type = PropertyType.Parse(c.GetProperty("type").GetString()).Value;

        var rule = ValidationRules.FromJson(c.GetProperty("rule"), type, Options(c));

        rule.IsSuccess.ShouldBe(c.GetProperty("valid").GetBoolean());
        if (rule.IsSuccess)
        {
            JsonNode.DeepEquals(ValidationRules.ToJson(rule.Value), JsonNode.Parse(c.GetProperty("canonical").GetRawText())).ShouldBeTrue(
                ValidationRules.ToJson(rule.Value).ToJsonString());
        }
        else
        {
            rule.Error.Code.ShouldBe(ErrorCodes.PropertyRuleInvalid);
        }
    }

    [Theory]
    [MemberData(nameof(ValueCases))]
    public void ReadValue_SharedCases_AcceptsOrRefuses(FixtureCase fixture)
    {
        using var document = JsonDocument.Parse(fixture.Json);
        var c = document.RootElement;
        var type = PropertyType.Parse(c.GetProperty("type").GetString()).Value;

        var value = PropertyValues.FromJson(c.GetProperty("json"), type, Options(c));

        value.IsSuccess.ShouldBe(c.GetProperty("valid").GetBoolean());
        if (value.IsSuccess)
        {
            var written = PropertyValues.ToJson(value.Value);
            JsonNode.DeepEquals(written, JsonNode.Parse(c.GetProperty("canonical").GetRawText())).ShouldBeTrue(written.ToJsonString());
        }
    }

    [Theory]
    [MemberData(nameof(RegexCases))]
    public void SafeRegex_SharedCases_SameMeaningAsJavaScript(FixtureCase fixture)
    {
        using var document = JsonDocument.Parse(fixture.Json);
        var c = document.RootElement;

        var regex = SafeRegex.Parse(c.GetProperty("pattern").GetString()!);

        regex.IsSuccess.ShouldBe(c.GetProperty("valid").GetBoolean());
        if (regex.IsSuccess)
        {
            regex.Value.JavaScriptPattern.ShouldBe(c.GetProperty("javascript").GetString());
            foreach (var match in c.GetProperty("matches").EnumerateArray())
            {
                regex.Value.IsMatch(match[0].GetString()!).ShouldBe(match[1].GetBoolean(), $"value {match[0].GetRawText()}");
            }
        }
    }

    private static IReadOnlyList<EnumOption>? Options(JsonElement c) =>
        c.TryGetProperty("options", out var options)
            ? [.. options.EnumerateArray().Select(o => new EnumOption(o.GetProperty("key").GetString()!, o.GetProperty("label").GetString()!))]
            : null;

    private static TheoryData<FixtureCase> Only(string file)
    {
        var data = new TheoryData<FixtureCase>();
        foreach (var row in SharedFixtures.Load("rules").Where(r => r.Data.File == file))
        {
            data.Add(row.Data);
        }

        return data;
    }

    private sealed class FixtureContext(JsonElement json) : IRuleContext
    {
        private readonly Dictionary<ElementId, ElementKind> _kinds = json.TryGetProperty("kinds", out var kinds)
            ? kinds.EnumerateObject().ToDictionary(
                k => ElementId.Parse(k.Name),
                k => k.Value.GetString() == "page" ? ElementKind.Page : ElementKind.Category)
            : [];

        private readonly HashSet<(ElementId, ElementId)> _memberships = json.TryGetProperty("inCategory", out var memberships)
            ? [.. memberships.EnumerateArray().Select(m => (ElementId.Parse(m[0].GetString()!), ElementId.Parse(m[1].GetString()!)))]
            : [];

        public ElementKind? KindOf(ElementId id) => _kinds.TryGetValue(id, out var kind) ? kind : null;

        public bool IsInCategory(ElementId pageId, ElementId categoryId) => _memberships.Contains((pageId, categoryId));
    }
}
