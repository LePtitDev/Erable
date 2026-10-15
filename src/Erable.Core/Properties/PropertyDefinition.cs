namespace Erable.Core.Properties;

/// <summary>Definition of a property (docs/architecture.md §5.4, F08.R1).</summary>
/// <param name="Id">Element ID of the definition (under the Propriétés root).</param>
/// <param name="Name">Display name: the element name.</param>
/// <param name="Key">Readable unique identifier, renamable (<see cref="PropertyKey"/>).</param>
/// <param name="Description">Plain text.</param>
/// <param name="Type">Value type.</param>
/// <param name="Default">Value when neither the page nor its categories define one.</param>
/// <param name="Rules">Validation rules, in order; they warn, never block (§5.4).</param>
/// <param name="Options">Options of an <c>enum</c> (or <c>list&lt;enum&gt;</c>), in display order; empty otherwise.</param>
public sealed record PropertyDefinition(
    ElementId Id,
    string Name,
    string Key,
    string Description,
    PropertyType Type,
    PropertyValue? Default,
    IReadOnlyList<ValidationRule> Rules,
    IReadOnlyList<EnumOption> Options)
{
    public const int MaxDescriptionLength = 2_000;
    public const int MaxOptions = 200;
    public const int MaxRules = 50;

    /// <summary>Rules for a value of this property.</summary>
    public IReadOnlyList<RuleViolation> Validate(PropertyValue value, IRuleContext? context = null) =>
        RuleEvaluator.Evaluate(Rules, value, context);

    /// <summary>
    /// Checks the consistency of a definition before it is saved: options only for enums (unique keys, non-empty labels),
    /// rules available for the type, a default of the right type.
    /// </summary>
    public DomainError? Check()
    {
        if (Description.Length > MaxDescriptionLength)
        {
            return new DomainError(ErrorCodes.PropertyDescriptionTooLong, $"The description exceeds {MaxDescriptionLength} characters.");
        }

        if (!PropertyKey.Normalize(Key).IsSuccess)
        {
            return PropertyKey.Normalize(Key).Error;
        }

        if (Type.Scalar != ScalarType.Enum && Options.Count > 0)
        {
            return new DomainError(ErrorCodes.PropertyOptionInvalid, "Only enum properties have options.");
        }

        if (Options.Count > MaxOptions || Options.Select(o => o.Key).Distinct(StringComparer.Ordinal).Count() != Options.Count
            || Options.Any(o => !PropertyKey.Normalize(o.Key).IsSuccess || o.Label.Trim().Length == 0 || o.Label.Length > 200))
        {
            return new DomainError(ErrorCodes.PropertyOptionInvalid, "Options need unique valid keys and non-empty labels.");
        }

        if (Rules.Count > MaxRules || Rules.Any(r => !ValidationRules.Available(Type).Contains(r.Op)))
        {
            return new DomainError(ErrorCodes.PropertyRuleInvalid, $"A rule is not available for {Type}.");
        }

        if (Rules.OfType<InRule>().Any(r => r.Keys.Any(k => Options.All(o => o.Key != k))))
        {
            return new DomainError(ErrorCodes.PropertyRuleInvalid, "in: unknown option.");
        }

        if (Default is not null && (!PropertyValues.Matches(Default, Type) || !OptionsContain(Default)))
        {
            return new DomainError(ErrorCodes.PropertyValueInvalid, $"The default value is not a valid {Type}.");
        }

        return null;
    }

    /// <summary>True when every enum key of the value is an option.</summary>
    public bool OptionsContain(PropertyValue value) => value switch
    {
        EnumValue e => Options.Any(o => o.Key == e.Key),
        ListValue l => l.Items.All(OptionsContain),
        _ => true,
    };
}
