using System.Diagnostics;
using System.Text.Json;
using Erable.Core.Properties;

namespace Erable.Core.Tests.Properties;

public sealed class PropertyModelTests
{
    private static readonly EnumOption[] Roles = [new("marchande", "Marchande"), new("garde", "Garde")];

    // Types

    [Theory]
    [InlineData("int")]
    [InlineData("list<string>")]
    [InlineData("ref")]
    [InlineData("list<enum>")]
    public void Parse_DocumentedName_RoundTrips(string name)
    {
        PropertyType.Parse(name).Value.ToString().ShouldBe(name);
    }

    [Theory]
    [InlineData("integer")]
    [InlineData("list<list<int>>")]
    [InlineData("")]
    [InlineData(null)]
    public void Parse_UnknownType_IsRefused(string? name)
    {
        PropertyType.Parse(name).Error.Code.ShouldBe(ErrorCodes.PropertyTypeInvalid);
    }

    [Fact]
    public void Item_OfAList_IsTheScalarType()
    {
        PropertyType.ListOf(ScalarType.Int).Item.ShouldBe(PropertyType.Int);
    }

    // Keys

    [Theory]
    [InlineData("Niveau", "niveau")]
    [InlineData("Points de vie", "points_de_vie")]
    [InlineData("Âge", "age")]
    [InlineData("  Rôle / fonction  ", "role_fonction")]
    [InlineData("12 travaux", "p_12_travaux")]
    [InlineData("!!!", "p_")]
    public void FromName_DerivesAReadableKey(string name, string key)
    {
        PropertyKey.FromName(name).ShouldBe(key);
        PropertyKey.Normalize(key).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void FromName_LongName_IsTruncated()
    {
        PropertyKey.FromName(new string('a', 100)).Length.ShouldBe(PropertyKey.MaxLength);
    }

    [Theory]
    [InlineData("Niveau")]
    [InlineData("1abc")]
    [InlineData("a b")]
    [InlineData("")]
    public void Normalize_InvalidKey_IsRefused(string key)
    {
        PropertyKey.Normalize(key).Error.Code.ShouldBe(ErrorCodes.PropertyKeyInvalid);
    }

    [Fact]
    public void Unique_TakenKey_GetsASuffix()
    {
        PropertyKey.Unique("niveau", ["niveau", "niveau_2"]).ShouldBe("niveau_3");
        PropertyKey.Unique("age", ["niveau"]).ShouldBe("age");
        PropertyKey.Unique(new string('a', 64), [new string('a', 64)]).ShouldBe(new string('a', 62) + "_2");
    }

    // Values

    [Fact]
    public void ListValue_Equality_IsStructural()
    {
        var a = new ListValue([new IntValue(1), new IntValue(2)]);
        var b = new ListValue([new IntValue(1), new IntValue(2)]);

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
        a.ShouldNotBe(new ListValue([new IntValue(2), new IntValue(1)]));
    }

    [Fact]
    public void FromJson_MalformedText_IsRefused()
    {
        PropertyValues.FromJson("{", PropertyType.Int).Error.Code.ShouldBe(ErrorCodes.PropertyValueInvalid);
    }

    [Fact]
    public void FromJson_ListTooLong_IsRefused()
    {
        PropertyValues.FromJson("[" + string.Join(',', Enumerable.Repeat("1", 1001)) + "]", PropertyType.ListOf(ScalarType.Int))
            .IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Matches_ChecksScalarAndListShape()
    {
        PropertyValues.Matches(new IntValue(1), PropertyType.Int).ShouldBeTrue();
        PropertyValues.Matches(new IntValue(1), PropertyType.Float).ShouldBeFalse();
        PropertyValues.Matches(new TextValue("a"), PropertyType.Text).ShouldBeTrue();
        PropertyValues.Matches(new ListValue([new IntValue(1)]), PropertyType.ListOf(ScalarType.Int)).ShouldBeTrue();
        PropertyValues.Matches(new ListValue([new IntValue(1)]), PropertyType.Int).ShouldBeFalse();
        PropertyValues.Matches(new IntValue(1), PropertyType.ListOf(ScalarType.Int)).ShouldBeFalse();
    }

    // Rules

    [Fact]
    public void Available_EachType_MatchesTheTableOfTheArchitecture()
    {
        ValidationRules.Available(PropertyType.Bool).ShouldBeEmpty();
        ValidationRules.Available(PropertyType.String).Select(ValidationRules.OpName).ShouldBe(["min_length", "max_length", "match", "not_empty"]);
        ValidationRules.Available(PropertyType.Text).ShouldBe([RuleOp.MaxLength]);
        ValidationRules.Available(PropertyType.Int).Count.ShouldBe(6);
        ValidationRules.Available(PropertyType.Float).ShouldBe(ValidationRules.Available(PropertyType.Int));
        ValidationRules.Available(PropertyType.Enum).ShouldBe([RuleOp.In]);
        ValidationRules.Available(PropertyType.Date).ShouldBe([RuleOp.After, RuleOp.Before]);
        ValidationRules.Available(PropertyType.Ref).ShouldBe([RuleOp.Kind, RuleOp.InCategory]);
        ValidationRules.Available(PropertyType.ListOf(ScalarType.Bool)).ShouldBe([RuleOp.MinCount, RuleOp.MaxCount]);
    }

    [Fact]
    public void OpName_EveryOperator_ParsesBack()
    {
        foreach (var op in Enum.GetValues<RuleOp>())
        {
            ValidationRules.ParseOp(ValidationRules.OpName(op)).ShouldBe(op);
        }

        ValidationRules.ParseOp("even").ShouldBeNull();
    }

    [Fact]
    public void ListFromJson_NotAnArray_IsRefused()
    {
        using var json = JsonDocument.Parse("{}");

        ValidationRules.ListFromJson(json.RootElement, PropertyType.Int).Error.Code.ShouldBe(ErrorCodes.PropertyRuleInvalid);
    }

    [Fact]
    public void ListFromJson_OneInvalidRule_RefusesTheList()
    {
        using var json = JsonDocument.Parse("""[{"op":"less_than","value":3},{"op":"match","value":"a"}]""");

        ValidationRules.ListFromJson(json.RootElement, PropertyType.Int).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Evaluate_StaleRuleOfAnotherType_IsIgnored()
    {
        // After a type change from int to string, a numeric rule no longer applies.
        RuleEvaluator.Evaluate([new NumberRule(RuleOp.LessThan, 3)], new TextValue("abc")).ShouldBeEmpty();
    }

    [Fact]
    public void Evaluate_CountRuleOnAScalar_IsIgnored()
    {
        RuleEvaluator.Evaluate([new LengthRule(RuleOp.MinCount, 2)], new IntValue(1)).ShouldBeEmpty();
    }

    [Fact]
    public void SafeRegex_PathologicalPattern_DoesNotBlock()
    {
        var regex = SafeRegex.Parse("^[a-z]*[a-z]*[a-z]*[a-z]*!$").Value;
        var stopwatch = Stopwatch.StartNew();

        regex.IsMatch(new string('a', 10_000) + "?").ShouldBeFalse();

        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void SafeRegex_EndAnchor_IsTheTrueEndOfInputInDotNet()
    {
        var regex = SafeRegex.Parse("a$").Value;

        regex.DotNetPattern.ShouldEndWith(@"\z");
        regex.JavaScriptPattern.ShouldEndWith("$");
        regex.IsMatch("a\n").ShouldBeFalse();
        regex.ShouldBe(SafeRegex.Parse("a$").Value);
        regex.GetHashCode().ShouldBe(SafeRegex.Parse("a$").Value.GetHashCode());
        regex.ToString().ShouldBe("a$");
    }

    // Definitions

    private static PropertyDefinition Niveau() => new(
        new ElementId(0x44B1), "Niveau", "niveau", "Niveau du personnage.", PropertyType.Int, null,
        [new NumberRule(RuleOp.GreaterOrEqual, 1), new NumberRule(RuleOp.LessOrEqual, 60)], []);

    [Fact]
    public void Validate_NiveauOfIlsa_WarnsLessOrEqual()
    {
        Niveau().Validate(new IntValue(72)).Single().Rule.ShouldBe(new NumberRule(RuleOp.LessOrEqual, 60));
    }

    [Fact]
    public void Check_ValidDefinition_HasNoError()
    {
        Niveau().Check().ShouldBeNull();
        (Niveau() with { Type = PropertyType.Enum, Rules = [new InRule(["garde"])], Options = Roles, Default = new EnumValue("garde") })
            .Check().ShouldBeNull();
    }

    [Fact]
    public void Check_OptionsOnANonEnum_AreRefused()
    {
        (Niveau() with { Options = Roles }).Check()!.Code.ShouldBe(ErrorCodes.PropertyOptionInvalid);
    }

    [Fact]
    public void Check_DuplicateOptionKeys_AreRefused()
    {
        (Niveau() with { Type = PropertyType.Enum, Rules = [], Options = [new("a", "A"), new("a", "B")] }).Check()!.Code
            .ShouldBe(ErrorCodes.PropertyOptionInvalid);
    }

    [Fact]
    public void Check_EmptyOptionLabel_IsRefused()
    {
        (Niveau() with { Type = PropertyType.Enum, Rules = [], Options = [new("a", " ")] }).Check()!.Code.ShouldBe(ErrorCodes.PropertyOptionInvalid);
    }

    [Fact]
    public void Check_RuleOfAnotherType_IsRefused()
    {
        (Niveau() with { Type = PropertyType.Bool }).Check()!.Code.ShouldBe(ErrorCodes.PropertyRuleInvalid);
    }

    [Fact]
    public void Check_InRuleWithUnknownOption_IsRefused()
    {
        (Niveau() with { Type = PropertyType.Enum, Rules = [new InRule(["voleur"])], Options = Roles }).Check()!.Code
            .ShouldBe(ErrorCodes.PropertyRuleInvalid);
    }

    [Fact]
    public void Check_DefaultOfAnotherType_IsRefused()
    {
        (Niveau() with { Default = new TextValue("10") }).Check()!.Code.ShouldBe(ErrorCodes.PropertyValueInvalid);
        (Niveau() with { Type = PropertyType.Enum, Rules = [], Options = Roles, Default = new EnumValue("voleur") }).Check()!.Code
            .ShouldBe(ErrorCodes.PropertyValueInvalid);
    }

    [Fact]
    public void Check_InvalidKeyOrLongDescription_IsRefused()
    {
        (Niveau() with { Key = "Niveau" }).Check()!.Code.ShouldBe(ErrorCodes.PropertyKeyInvalid);
        (Niveau() with { Description = new string('a', 2001) }).Check()!.Code.ShouldBe(ErrorCodes.PropertyDescriptionTooLong);
    }

    [Fact]
    public void OptionsContain_ListOfKeys_ChecksEveryItem()
    {
        var role = Niveau() with { Type = PropertyType.ListOf(ScalarType.Enum), Rules = [], Options = Roles };

        role.OptionsContain(new ListValue([new EnumValue("garde"), new EnumValue("marchande")])).ShouldBeTrue();
        role.OptionsContain(new ListValue([new EnumValue("garde"), new EnumValue("x")])).ShouldBeFalse();
    }

    // F08.R6 ─ conversions

    private static Result<PropertyValue?> Convert(PropertyValue value, PropertyType from, PropertyType to, IReadOnlyList<EnumOption>? toOptions = null) =>
        ValueConversion.Convert(value, from, Roles, to, toOptions ?? []);

    [Fact]
    public void Convert_EnumToInt_IsNotConvertible()
    {
        // E2E-08-10: Rôle (« Marchande ») cannot become an int.
        Convert(new EnumValue("marchande"), PropertyType.Enum, PropertyType.Int).Error.Code.ShouldBe(ErrorCodes.PropertyValueNotConvertible);
    }

    [Fact]
    public void Convert_EnumToString_UsesTheLabel()
    {
        Convert(new EnumValue("marchande"), PropertyType.Enum, PropertyType.String).Value.ShouldBe(new TextValue("Marchande"));
    }

    [Fact]
    public void Convert_StringToEnum_MatchesLabelOrKeyIgnoringCase()
    {
        Convert(new TextValue("garde"), PropertyType.String, PropertyType.Enum, Roles).Value.ShouldBe(new EnumValue("garde"));
        Convert(new TextValue("MARCHANDE"), PropertyType.String, PropertyType.Enum, Roles).Value.ShouldBe(new EnumValue("marchande"));
        Convert(new TextValue("voleur"), PropertyType.String, PropertyType.Enum, Roles).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Convert_EnumToEnumWithRenamedOptions_KeepsTheKeyOrMatchesTheLabel()
    {
        Convert(new EnumValue("garde"), PropertyType.Enum, PropertyType.Enum, Roles).Value.ShouldBe(new EnumValue("garde"));
        Convert(new EnumValue("garde"), PropertyType.Enum, PropertyType.Enum, [new("g", "Garde")]).Value.ShouldBe(new EnumValue("g"));
    }

    [Theory]
    [InlineData("int", "40", "float", "40")]
    [InlineData("float", "2", "int", "2")]
    [InlineData("string", "\" 42 \"", "int", "42")]
    [InlineData("string", "\"1.5\"", "float", "1.5")]
    [InlineData("string", "\"true\"", "bool", "true")]
    [InlineData("string", "\"2026-10-13\"", "date", "\"2026-10-13\"")]
    [InlineData("string", "\"3f2a9c14e07b5d21\"", "ref", "\"3F2A9C14E07B5D21\"")]
    [InlineData("int", "40", "string", "\"40\"")]
    [InlineData("bool", "false", "text", "\"false\"")]
    [InlineData("float", "1.5", "string", "\"1.5\"")]
    [InlineData("date", "\"2026-10-13\"", "string", "\"2026-10-13\"")]
    [InlineData("ref", "\"3F2A9C14E07B5D21\"", "string", "\"3F2A9C14E07B5D21\"")]
    [InlineData("text", "\"court\"", "string", "\"court\"")]
    [InlineData("int", "40", "list<int>", "[40]")]
    [InlineData("list<int>", "[40]", "int", "40")]
    [InlineData("list<int>", "[1,2]", "list<float>", "[1,2]")]
    public void Convert_ConvertibleValue_IsConverted(string from, string json, string to, string expected)
    {
        var fromType = PropertyType.Parse(from).Value;
        var toType = PropertyType.Parse(to).Value;
        var value = PropertyValues.FromJson(json, fromType).Value;

        var converted = Convert(value, fromType, toType);

        PropertyValues.ToJsonString(converted.Value!).ShouldBe(expected);
    }

    [Theory]
    [InlineData("float", "1.5", "int")]
    [InlineData("string", "\"abc\"", "int")]
    [InlineData("string", "\"abc\"", "float")]
    [InlineData("string", "\"oui\"", "bool")]
    [InlineData("string", "\"13/10/2026\"", "date")]
    [InlineData("string", "\"Région\"", "ref")]
    [InlineData("text", "\"deux\\nlignes\"", "string")]
    [InlineData("int", "1", "bool")]
    [InlineData("list<int>", "[1,2]", "int")]
    [InlineData("list<string>", "[\"a\"]", "list<int>")]
    [InlineData("list<string>", "[\"a\"]", "int")]
    [InlineData("date", "\"2026-10-13\"", "int")]
    public void Convert_IncompatibleValue_IsRefused(string from, string json, string to)
    {
        var fromType = PropertyType.Parse(from).Value;
        var value = PropertyValues.FromJson(json, fromType).Value;

        Convert(value, fromType, PropertyType.Parse(to).Value).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Convert_EmptyListToScalar_RemovesTheValue()
    {
        var converted = Convert(new ListValue([]), PropertyType.ListOf(ScalarType.Int), PropertyType.Int);

        converted.IsSuccess.ShouldBeTrue();
        converted.Value.ShouldBeNull();
    }

    [Fact]
    public void TextOf_ListAndUnknownOption_AreReadable()
    {
        ValueConversion.TextOf(new EnumValue("x"), Roles).ShouldBe("x");
        ValueConversion.TextOf(new ListValue([new IntValue(1), new IntValue(2)]), Roles).ShouldBe("1, 2");
    }
}
