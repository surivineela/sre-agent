namespace FirstPartyAgent.Core.Models;
public class IncidentAdvancedSearchFilter
{
    private static readonly Dictionary<string, List<string>> ValidPropertyOperators = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "CorrelationId", new List<string> { "==" } },
            { "CreateDate", new List<string> { "==", ">", ">=", "<", "<=" } },
            { "IncidentId", new List<string> { "==", ">", ">=", "<", "<=" } },
            { "OccurringDatacenter", new List<string> { "==" } },
            { "OccurringDeviceGroup", new List<string> { "==" } },
            { "OccurringDeviceName", new List<string> { "==" } },
            { "OccurringEnvironment", new List<string> { "==" } },
            { "OccurringServiceInstanceId", new List<string> { "==" } },
            { "IncidentType", new List<string> { "==" } },
            { "Keywords", new List<string> { "==", "contains", "has" } },
            { "ModifiedDate", new List<string> { "==", ">", ">=", "<", "<=" } },
            { "OwningTeamId", new List<string> { "==" } },
            { "OwningTenantId", new List<string> { "==" } },
            { "ParentIncidentId", new List<string> { "==", "!=" } },
            { "RoutingId", new List<string> { "==" } },
            { "Severity", new List<string> { "==", "!=", ">", ">=", "<", "<=" } },
            { "Status", new List<string> { "==", "!=", ">", ">=", "<", "<=" } },
            { "SourceIncidentId", new List<string> { "==" } },
            { "SourceId", new List<string> { "==" } },
            { "Title", new List<string> { "==", "contains", "has" } }
        };

    private static readonly List<string> DateTimeProperties = new List<string>
        {
            "CreateDate",
            "ModifiedDate",
        };

    private static readonly List<string> GuidProperties = new List<string>
        {
            "SourceIncidentId"
        };

    public static Dictionary<string, List<string>> GetQueryableIncidentProperties() => IncidentAdvancedSearchFilter.ValidPropertyOperators;

    public string ColumnName { get; }
    public string Operator { get; }
    public string Value { get; }
    public bool IsNullComparison { get; }

    public IncidentAdvancedSearchFilter(string columnName, string @operator, string value)
    {
        if (string.IsNullOrWhiteSpace(columnName))
            throw new ArgumentException("Column name cannot be null or empty", nameof(columnName));

        if (string.IsNullOrWhiteSpace(@operator))
            throw new ArgumentException("Operator cannot be null or empty", nameof(@operator));

        // Normalize the column name and operator
        ColumnName = columnName.Trim();
        Operator = @operator.Trim().ToLowerInvariant();
        Value = value;

        // Check if this is a null comparison
        IsNullComparison = string.IsNullOrWhiteSpace(value) || string.Equals(value, "null", StringComparison.OrdinalIgnoreCase);

        // Validate that the property supports this operator
        if (!ValidPropertyOperators.TryGetValue(ColumnName, out var supportedOperators))
        {
            throw new ArgumentException($"Property '{ColumnName}' is not supported for filtering. Supported properties and operators are: {System.Text.Json.JsonSerializer.Serialize(ValidPropertyOperators)}", nameof(columnName));
        }

        if (!supportedOperators.Contains(Operator))
        {
            throw new ArgumentException($"Operator '{Operator}' is not supported for property '{ColumnName}'. Supported operators are: {System.Text.Json.JsonSerializer.Serialize(supportedOperators)}", nameof(@operator));
        }

        // Validate datetime format if required
        if (DateTimeProperties.Contains(ColumnName) && !IsNullComparison)
        {
            if (!DateTime.TryParse(value, out _))
            {
                throw new ArgumentException($"Value for '{ColumnName}' must be a valid datetime in format 'yyyy-MM-ddTHH:mm:ss'", nameof(value));
            }
        }

        // Validate GUID format if required
        if (GuidProperties.Contains(ColumnName) && !IsNullComparison)
        {
            if (!Guid.TryParseExact(value, "D", out _) && !Guid.TryParseExact(value, "N", out _))
            {
                throw new ArgumentException($"Value for '{ColumnName}' must be a valid GUID", nameof(value));
            }
        }
    }

    // Generate the Kusto filter expression for this filter
    public string ToKustoFilterExpression()
    {
        if (IsNullComparison)
        {
            return Operator switch
            {
                "==" => $"(isnull({ColumnName}) or isempty({ColumnName}))",
                "!=" => $"(isnotnull({ColumnName}) and isnotempty({ColumnName}))",
                _ => throw new InvalidOperationException($"Unsupported operator '{Operator}' for null comparison. Supported operators are eq, ne")
            };
        }

        string formattedValue;

        // Format value based on property type
        if (DateTimeProperties.Contains(ColumnName))
        {
            // Format as datetime('yyyy-MM-ddTHH:mm:ss')
            DateTime dateTime = DateTime.Parse(Value);
            formattedValue = $"datetime({dateTime:yyyy-MM-ddTHH:mm:ss})";
        }
        else if (GuidProperties.Contains(ColumnName))
        {
            // Format as guid('xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx')
            formattedValue = $"guid('{Value}')";
        }
        else if (int.TryParse(Value, out _) || double.TryParse(Value, out _))
        {
            // Numeric values don't need quotes
            formattedValue = Value;
        }
        else
        {
            // For string values, escape single quotes and add surrounding quotes
            formattedValue = $"'{Value.Replace("'", "''")}'";
        }

        // Return the formatted filter expression
        return $"{ColumnName} {Operator} {formattedValue}";

    }
}
