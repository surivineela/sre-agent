// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Cli.Models
{
    /// <summary>
    /// Represents a YAML API version with strongly-typed version comparison.
    /// </summary>
    public record YamlApiVersion
    {
        /// <summary>
        /// API version for V1 Extension YAML format
        /// </summary>
        public static readonly YamlApiVersion V1 = new("agent.platform.ai/v1", 1);

        /// <summary>
        /// API version for V2 Extension YAML format
        /// </summary>
        public static readonly YamlApiVersion V2 = new("azuresre.ai/v2", 2);

        /// <summary>
        /// The full API version string (e.g., "azuresre.ai/v2")
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// The numeric version number (1, 2, etc.)
        /// </summary>
        public int Version { get; }

        private YamlApiVersion(string value, int version)
        {
            Value = value;
            Version = version;
        }

        /// <summary>
        /// Parses an API version string and returns the corresponding YamlApiVersion.
        /// </summary>
        /// <param name="apiVersionString">The API version string to parse</param>
        /// <returns>The corresponding YamlApiVersion, or null if not recognized</returns>
        public static YamlApiVersion? Parse(string? apiVersionString)
        {
            if (string.IsNullOrWhiteSpace(apiVersionString))
                return null;

            if (apiVersionString.Equals(V1.Value, StringComparison.OrdinalIgnoreCase))
                return V1;

            if (apiVersionString.Equals(V2.Value, StringComparison.OrdinalIgnoreCase))
                return V2;

            return null;
        }

        public override string ToString() => Value;

        public static implicit operator string(YamlApiVersion version) => version.Value;

        // String comparison operators - allow comparing YamlApiVersion with string directly
        // The string is automatically parsed before comparison
        public static bool operator ==(YamlApiVersion? version, string? versionString)
        {
            var parsed = Parse(versionString);
            return version?.Version == parsed?.Version;
        }

        public static bool operator !=(YamlApiVersion? version, string? versionString)
        {
            return !(version == versionString);
        }

        public static bool operator ==(string? versionString, YamlApiVersion? version)
        {
            return version == versionString;
        }

        public static bool operator !=(string? versionString, YamlApiVersion? version)
        {
            return !(versionString == version);
        }
    }
}
