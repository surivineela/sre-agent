using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Cli.Validations
{
    public static class ToolValidation
    {
        public static bool ValidateTool(string name, string type, out List<string> errors)
        {
            errors = new List<string>();
            if (string.IsNullOrWhiteSpace(name))
                errors.Add("Tool name must not be empty.");
            if (name != null && name.Any(char.IsWhiteSpace))
                errors.Add("Tool name must not contain whitespace.");
            if (string.IsNullOrWhiteSpace(type))
                errors.Add("Tool type must not be empty.");
            // Add more tool-specific validation as needed
            return errors.Count == 0;
        }
    }
}
