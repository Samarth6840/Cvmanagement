using CvManagement.Data.Entities.Attributes;
using CvManagement.Data.Entities.Positions;
using CvManagement.Data.Entities.Profiles;

namespace CvManagement.Services.Positions;

public static class AccessRuleEvaluator
{
    public static bool CanAccess(ICollection<PositionAccessRule> rules, List<ProfileAttributeValue> values) =>
        rules.Count == 0
        || rules.All(r => IsSatisfied(r, values.FirstOrDefault(v => v.AttributeDefinitionId == r.AttributeDefinitionId)));

    public static bool IsSatisfied(PositionAccessRule rule, ProfileAttributeValue? value)
    {
        if (value is null) return false;
        return rule.AttributeDefinition?.DataType switch
        {
            AttributeDataType.Numeric => CompareDecimal(value.NumericValue, rule),
            AttributeDataType.Date => CompareDate(value.DateValue, rule),
            AttributeDataType.Boolean => CompareBool(value.BoolValue, rule),
            AttributeDataType.OneOfMany => CompareString(value.SelectedOption?.Label, rule),
            _ => CompareString(GetText(value, rule.AttributeDefinition.DataType), rule)
        };
    }

    private static string? GetText(ProfileAttributeValue value, AttributeDataType dataType) =>
        dataType switch
        {
            AttributeDataType.String => value.StringValue,
            AttributeDataType.Text => value.TextValue,
            _ => null
        };

    private static bool CompareDecimal(decimal? v, PositionAccessRule rule)
    {
        if (!v.HasValue || !decimal.TryParse(rule.FilterValue, out var expected)) return false;
        return rule.Operator switch
        {
            PositionAccessOperator.GreaterThan => v > expected,
            PositionAccessOperator.LessThan => v < expected,
            PositionAccessOperator.Equals => v == expected,
            PositionAccessOperator.NotEquals => v != expected,
            _ => false
        };
    }

    private static bool CompareDate(DateTime? v, PositionAccessRule rule)
    {
        if (!v.HasValue || !DateTime.TryParse(rule.FilterValue, out var expected)) return false;
        return rule.Operator switch
        {
            PositionAccessOperator.GreaterThan => v > expected,
            PositionAccessOperator.LessThan => v < expected,
            PositionAccessOperator.Equals => v.Value.Date == expected.Date,
            PositionAccessOperator.NotEquals => v.Value.Date != expected.Date,
            _ => false
        };
    }

    private static bool CompareBool(bool? v, PositionAccessRule rule)
    {
        if (!v.HasValue) return false;
        var expected = bool.TryParse(rule.FilterValue, out var b) && b;
        return rule.Operator switch
        {
            PositionAccessOperator.Equals => v == expected,
            PositionAccessOperator.NotEquals => v != expected,
            _ => false
        };
    }

    private static bool CompareString(string? v, PositionAccessRule rule)
    {
        if (v is null) return false;
        return rule.Operator switch
        {
            PositionAccessOperator.Equals => string.Equals(v.Trim(), rule.FilterValue.Trim(), StringComparison.OrdinalIgnoreCase),
            PositionAccessOperator.NotEquals => !string.Equals(v.Trim(), rule.FilterValue.Trim(), StringComparison.OrdinalIgnoreCase),
            PositionAccessOperator.Contains => v.Contains(rule.FilterValue, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}