namespace Agent.Cli.Helpers;

/// <summary>
/// Helper class for parsing command-line arguments and key-value pairs.
/// </summary>
public static class ArgumentParser
{
    /// <summary>
    /// Parses an array of key-value pairs into a dictionary.
    /// Expected format: ["--key1", "value1", "--key2", "value2", ...]
    /// </summary>
    /// <param name="pairs">Array of key-value pairs</param>
    /// <returns>Dictionary containing the parsed key-value pairs</returns>
    public static Dictionary<string, object> ParseKeyValuePairs(string[] pairs)
    {
        var dict = new Dictionary<string, object>();
        for (int i = 0; i < pairs.Length - 1; i += 2)
        {
            dict[pairs[i].TrimStart('-')] = pairs[i + 1];
        }
        return dict;
    }
}
