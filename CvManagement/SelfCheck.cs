using CvManagement.Data.Entities.Attributes;
using CvManagement.Data.Entities.Positions;
using CvManagement.Data.Entities.Profiles;
using CvManagement.Services.Positions;

namespace CvManagement;

// Self-check for the EAV access-rule evaluator (spec §7.2). Run with:
//   dotnet run --project CvManagement -- --selfcheck
// Pure in-memory, so no PostgreSQL or test project is needed.
public static class SelfCheck
{
    // A rule and the value it is evaluated against must share one attribute id, which is
    // how the evaluator pairs them up.
    private static readonly Guid AttributeId = Guid.NewGuid();

    public static Task<int> RunAsync()
    {
        var cases = new (string Name, bool Expected, bool Actual)[]
        {
            ("no rules passes", true, AccessRuleEvaluator.CanAccess([], [])),

            // A rule whose attribute the candidate never filled in must not pass.
            ("missing attribute fails its rule", false,
                AccessRuleEvaluator.CanAccess(
                    [Numeric(PositionAccessOperator.GreaterThan, "5")],
                    [Value(AttributeDataType.String, StringValue: "something else", UseOtherAttribute: true)])),

            ("numeric greater-than passes", true,
                CanAccess(Numeric(PositionAccessOperator.GreaterThan, "5"), NumericValue(9))),

            ("numeric greater-than rejects equal", false,
                CanAccess(Numeric(PositionAccessOperator.GreaterThan, "5"), NumericValue(5))),

            ("invariant culture parses 5.5", true,
                CanAccess(Numeric(PositionAccessOperator.GreaterThan, "5.5"), NumericValue(6))),

            ("boolean NotEquals on an unparseable filter fails closed", false,
                CanAccess(Boolean(PositionAccessOperator.NotEquals, "yes"), BoolValue(false))),

            ("boolean NotEquals false matches true", true,
                CanAccess(Boolean(PositionAccessOperator.NotEquals, "false"), BoolValue(true))),

            ("string comparison is case-insensitive", true,
                CanAccess(Str(PositionAccessOperator.Equals, "Berlin"), StringValue("BERLIN"))),

            ("one-of-many uses the option label", true,
                CanAccess(OneOfMany(PositionAccessOperator.Equals, "Yes"), OptionValue("Yes"))),

            ("all rules must pass", false,
                AccessRuleEvaluator.CanAccess(
                    [Numeric(PositionAccessOperator.GreaterThan, "1"), Numeric(PositionAccessOperator.GreaterThan, "100")],
                    [Value(AttributeDataType.Numeric, NumericValue: 5)])),

            ("date equality compares the day only", true,
                CanAccess(Date(PositionAccessOperator.Equals, "2024-05-01"),
                    Value(AttributeDataType.Date, DateValue: new DateTime(2024, 5, 1, 13, 30, 0)))),

            ("contains operator", true,
                CanAccess(Text(PositionAccessOperator.Contains, "engineer"), TextValue("Senior Engineer"))),

            ("image attributes offer no operators", true,
                AccessRuleEvaluator.GetOperatorsFor(AttributeDataType.Image).Count == 0),
        };

        var failures = cases.Where(c => c.Expected != c.Actual).ToList();
        foreach (var c in cases)
            Console.WriteLine($"{(c.Expected == c.Actual ? "PASS" : "FAIL")}  {c.Name}");

        Console.WriteLine(failures.Count == 0
            ? $"\nAll {cases.Length} checks passed."
            : $"\n{failures.Count} of {cases.Length} checks FAILED.");

        return Task.FromResult(failures.Count == 0 ? 0 : 1);
    }

    private static bool CanAccess(PositionAccessRule rule, ProfileAttributeValue value) =>
        AccessRuleEvaluator.CanAccess([rule], [value]);

    private static PositionAccessRule Rule(AttributeDataType type, PositionAccessOperator op, string filterValue) =>
        new()
        {
            AttributeDefinitionId = AttributeId,
            Operator = op,
            FilterValue = filterValue,
            AttributeDefinition = new AttributeDefinition { DataType = type }
        };

    private static PositionAccessRule Numeric(PositionAccessOperator op, string filterValue) =>
        Rule(AttributeDataType.Numeric, op, filterValue);

    private static PositionAccessRule Date(PositionAccessOperator op, string filterValue) =>
        Rule(AttributeDataType.Date, op, filterValue);

    private static PositionAccessRule Boolean(PositionAccessOperator op, string filterValue) =>
        Rule(AttributeDataType.Boolean, op, filterValue);

    private static PositionAccessRule Str(PositionAccessOperator op, string filterValue) =>
        Rule(AttributeDataType.String, op, filterValue);

    private static PositionAccessRule Text(PositionAccessOperator op, string filterValue) =>
        Rule(AttributeDataType.Text, op, filterValue);

    private static PositionAccessRule OneOfMany(PositionAccessOperator op, string filterValue) =>
        Rule(AttributeDataType.OneOfMany, op, filterValue);

    private static ProfileAttributeValue Value(
        AttributeDataType type,
        string? StringValue = null,
        string? TextValue = null,
        decimal? NumericValue = null,
        DateTime? DateValue = null,
        bool? BoolValue = null,
        string? OptionLabel = null,
        bool UseOtherAttribute = false) =>
        new()
        {
            AttributeDefinitionId = UseOtherAttribute ? Guid.NewGuid() : AttributeId,
            AttributeDefinition = new AttributeDefinition { DataType = type },
            StringValue = StringValue,
            TextValue = TextValue,
            NumericValue = NumericValue,
            DateValue = DateValue,
            BoolValue = BoolValue,
            SelectedOption = OptionLabel is null ? null : new AttributeOption { Label = OptionLabel }
        };

    private static ProfileAttributeValue NumericValue(decimal value) =>
        Value(AttributeDataType.Numeric, NumericValue: value);

    private static ProfileAttributeValue BoolValue(bool value) =>
        Value(AttributeDataType.Boolean, BoolValue: value);

    private static ProfileAttributeValue StringValue(string value) =>
        Value(AttributeDataType.String, StringValue: value);

    private static ProfileAttributeValue TextValue(string value) =>
        Value(AttributeDataType.Text, TextValue: value);

    private static ProfileAttributeValue OptionValue(string label) =>
        Value(AttributeDataType.OneOfMany, OptionLabel: label);
}
