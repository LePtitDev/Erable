using System.Text.RegularExpressions;
using CsCheck;
using Erable.Core.Properties;
using Erable.Core.Search;

namespace Erable.Core.Tests.Search;

/// <summary>The pure parts of the search (M3-01): what reaches FTS5, folding, conditions.</summary>
public sealed partial class SearchCoreTests
{
    private static readonly ElementId Property = ElementId.Parse("44B107E9C2A63F58");

    [Theory]
    [InlineData("col gele", "\"col\" \"gele\"*")]
    [InlineData("  Ilsa   ", "\"Ilsa\"*")]
    [InlineData("Lanterne d'ambre", "\"Lanterne\" \"d\" \"ambre\"*")]
    [InlineData("gelé", "\"gelé\"*")]
    [InlineData("niveau 72", "\"niveau\" \"72\"*")]
    public void Build_Words_AreQuotedAndTheLastOneIsAPrefix(string text, string expected)
    {
        FullTextExpression.Build(text).ShouldBe(expected);
    }

    [Fact]
    public void Build_NameOnly_RestrictsToTheNameColumn()
    {
        FullTextExpression.Build("ilsa va", nameOnly: true).ShouldBe("{name} : (\"ilsa\" \"va\"*)");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"*()-:^")]
    public void Build_NoWord_IsNull(string? text)
    {
        FullTextExpression.Build(text).ShouldBeNull();
    }

    [Fact]
    public void Build_AnyText_OnlyHoldsQuotedWordsAndNoSyntax()
    {
        // Whatever is typed, the expression is made of quoted words of letters and digits: no operator, column or NEAR.
        Gen.String.Sample(text =>
        {
            var expression = FullTextExpression.Build(text);

            if (expression is not null)
            {
                QuotedWords().IsMatch(expression).ShouldBeTrue(expression);
            }
        }, iter: 2000);
    }

    [Fact]
    public void Build_LongInput_IsBounded()
    {
        var text = string.Join(' ', Enumerable.Range(0, 100).Select(i => new string('a', 200) + i));

        var words = FullTextExpression.Words(text);

        words.Count.ShouldBe(FullTextExpression.MaxWords);
        words.ShouldAllBe(w => w.Length <= FullTextExpression.MaxWordLength);
    }

    [Fact]
    public void Words_DecomposedAccent_StaysInOneWord()
    {
        FullTextExpression.Words("gelée").ShouldBe(["gelée"]);
    }

    [Theory]
    [InlineData("Col gelé", "col gele")]
    [InlineData("ÉRUDIT", "erudit")]
    [InlineData("Œuvre", "œuvre")]
    [InlineData(null, "")]
    public void Fold_IgnoresCaseAndAccents(string? text, string expected)
    {
        TextFolding.Fold(text).ShouldBe(expected);
    }

    [Fact]
    public void Fold_IsIdempotent()
    {
        Gen.String.Sample(text => TextFolding.Fold(TextFolding.Fold(text)).ShouldBe(TextFolding.Fold(text)));
    }

    [Theory]
    [InlineData(ScalarType.Int, ConditionOp.Greater, "50")]
    [InlineData(ScalarType.Int, ConditionOp.Equal, "-3")]
    [InlineData(ScalarType.Float, ConditionOp.LessOrEqual, "1.5")]
    [InlineData(ScalarType.Bool, ConditionOp.Equal, "true")]
    [InlineData(ScalarType.Bool, ConditionOp.NotEqual, "FALSE")]
    [InlineData(ScalarType.Date, ConditionOp.GreaterOrEqual, "2026-10-13")]
    [InlineData(ScalarType.String, ConditionOp.Contains, "mar")]
    [InlineData(ScalarType.Enum, ConditionOp.Equal, "marchande")]
    [InlineData(ScalarType.Ref, ConditionOp.Equal, "5B19C0E2A4D7F311")]
    [InlineData(ScalarType.Int, ConditionOp.IsSet, null)]
    [InlineData(ScalarType.Text, ConditionOp.IsNotSet, null)]
    public void CreateCondition_ValidCondition_IsAccepted(ScalarType type, ConditionOp op, string? raw)
    {
        PropertyCondition.Create(Property, type, op, raw).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(ScalarType.Int, ConditionOp.Greater, "beaucoup")]
    [InlineData(ScalarType.Int, ConditionOp.Equal, "1.5")]
    [InlineData(ScalarType.Float, ConditionOp.Equal, "NaN")]
    [InlineData(ScalarType.Bool, ConditionOp.Equal, "oui")]
    [InlineData(ScalarType.Bool, ConditionOp.Greater, "true")]
    [InlineData(ScalarType.Date, ConditionOp.Equal, "13/10/2026")]
    [InlineData(ScalarType.Enum, ConditionOp.Contains, "mar")]
    [InlineData(ScalarType.Ref, ConditionOp.Less, "5B19C0E2A4D7F311")]
    [InlineData(ScalarType.Ref, ConditionOp.Equal, "xyz")]
    [InlineData(ScalarType.String, ConditionOp.Equal, "")]
    public void CreateCondition_InvalidCondition_IsRefused(ScalarType type, ConditionOp op, string raw)
    {
        var result = PropertyCondition.Create(Property, type, op, raw);

        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe(ErrorCodes.SearchConditionInvalid);
    }

    [Fact]
    public void CreateCondition_Number_KeepsItsTypedColumn()
    {
        var integer = PropertyCondition.Create(Property, ScalarType.Int, ConditionOp.Greater, " 50 ").Value.Operand!;
        var real = PropertyCondition.Create(Property, ScalarType.Float, ConditionOp.Greater, "0.5").Value.Operand!;

        integer.ShouldBe(new ConditionOperand(50, null, null, null));
        real.ShouldBe(new ConditionOperand(null, 0.5, null, null));
    }

    [Theory]
    [InlineData(ModifiedWindow.Day, 24)]
    [InlineData(ModifiedWindow.Week, 168)]
    [InlineData(ModifiedWindow.Month, 720)]
    public void DurationOf_Window_IsInHours(ModifiedWindow window, int hours)
    {
        ModifiedWindows.DurationOf(window).ShouldBe(TimeSpan.FromHours(hours));
    }

    [GeneratedRegex("""^"[\p{L}\p{N}\p{M}]+"( "[\p{L}\p{N}\p{M}]+")*\*$""")]
    private static partial Regex QuotedWords();
}
