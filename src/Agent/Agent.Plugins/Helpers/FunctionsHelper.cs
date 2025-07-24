using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Agent.Plugins.Helpers;
public class FunctionsHelper
{
    public static bool ValidateEventPrimaryStampName(string eventPrimaryStampName, out string wellFormedEventPrimaryStampName)
    {
        wellFormedEventPrimaryStampName = string.Empty;

        // Check if input is null or empty
        if (string.IsNullOrWhiteSpace(eventPrimaryStampName))
        {
            return false;
        }

        // Regex pattern for waws-prod-xxx-bbb format
        // waws-prod- followed by alphanumeric characters, then -, then alphanumeric characters
        var pattern = @"^waws-prod-[a-zA-Z0-9]+-[a-zA-Z0-9]+$";
        var regex = new Regex(pattern, RegexOptions.IgnoreCase);

        var match = regex.Match(eventPrimaryStampName.Trim());
        
        if (match.Success)
        {
            wellFormedEventPrimaryStampName = eventPrimaryStampName.Trim().ToLowerInvariant();
            return true;
        }

        return false;
    }

    public static string ProcessEventPrimaryStampName(string eventPrimaryStampName, out bool isValid)
    {
        if (ValidateEventPrimaryStampName(eventPrimaryStampName, out string wellFormedEventPrimaryStampName))
        {
            isValid = true;
            return wellFormedEventPrimaryStampName;
        }

        isValid = false;
        return $"The eventPrimaryStampName '{eventPrimaryStampName}' is not in the correct format. " +
               "The expected format is 'waws-prod-xxx-bbb' where 'xxx' and 'bbb' are alphanumeric identifiers. " +
               "Examples of valid formats: 'waws-prod-bn1-001', 'waws-prod-mwh-123', 'waws-prod-euapbn1-005'. " +
               "Please provide a valid eventPrimaryStampName following this pattern.";
    }

    public static string ProcessFunctionName(string functionName, out bool isValid)
    {
        if (!string.IsNullOrWhiteSpace(functionName) && functionName.StartsWith("Host.Functions."))
        {
            isValid = false;
            return $"The functionName '{functionName}' is invalid. correct functionName will be '{functionName.Replace("Host.Functions.", "")}'. Call this function again with the correct functionName.";
        }
        isValid = true;
        return functionName;
    }
}
