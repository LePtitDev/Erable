using CsCheck;
using Erable.Core.Properties;

namespace Erable.Core.Tests.Properties;

/// <summary>docs/testing.md §4.1 (Règles de validation).</summary>
public sealed class RulePropertyTests
{
    private static readonly Gen<double> GenNumber = Gen.Int[-1000, 1000].Select(i => i / 4.0);

    [Fact]
    public void Between_IsGreaterOrEqualAndLessOrEqual()
    {
        Gen.Select(GenNumber, GenNumber, GenNumber).Sample((a, b, x) =>
        {
            var (min, max) = a <= b ? (a, b) : (b, a);
            var value = new FloatValue(x);

            var between = RuleEvaluator.Evaluate([new BetweenRule(min, max)], value).Count == 0;
            var both = RuleEvaluator.Evaluate([new NumberRule(RuleOp.GreaterOrEqual, min), new NumberRule(RuleOp.LessOrEqual, max)], value).Count == 0;

            between.ShouldBe(both);
        });
    }

    private static readonly Gen<(PropertyType Type, PropertyValue Value)> GenValue = Gen.OneOf(
        Gen.Bool.Select(b => (PropertyType.Bool, (PropertyValue)new BoolValue(b))),
        Gen.Long[-PropertyValues.MaxSafeInteger, PropertyValues.MaxSafeInteger].Select(i => (PropertyType.Int, (PropertyValue)new IntValue(i))),
        Gen.Double.Where(double.IsFinite).Select(d => (PropertyType.Float, (PropertyValue)new FloatValue(d))),
        Gen.String[Gen.Char.AlphaNumeric, 0, 50].Select(s => (PropertyType.String, (PropertyValue)new TextValue(s))),
        Gen.Date.Select(d => (PropertyType.Date, (PropertyValue)new DateValue(DateOnly.FromDateTime(d)))),
        Gen.ULong[ElementId.FirstRandomValue, ulong.MaxValue].Select(u => (PropertyType.Ref, (PropertyValue)new RefValue(new ElementId(u)))),
        Gen.Int[-100, 100].Array[0, 5].Select(a => (PropertyType.ListOf(ScalarType.Int), (PropertyValue)new ListValue([.. a.Select(i => (PropertyValue)new IntValue(i))]))));

    [Fact]
    public void ValidValue_JsonRoundTrip_IsIdentical()
    {
        GenValue.Sample(sample =>
        {
            var json = PropertyValues.ToJsonString(sample.Value);

            PropertyValues.FromJson(json, sample.Type).Value.ShouldBe(sample.Value);
        });
    }

    [Fact]
    public void ValidValue_StaysValidAfterJsonRoundTrip()
    {
        ValidationRule[] rules = [new NumberRule(RuleOp.GreaterThan, 0), new NumberRule(RuleOp.LessOrEqual, 60)];
        Gen.Int[1, 60].Sample(i =>
        {
            var value = PropertyValues.FromJson(PropertyValues.ToJsonString(new IntValue(i)), PropertyType.Int).Value;

            RuleEvaluator.Evaluate(rules, value).ShouldBeEmpty();
        });
    }
}
