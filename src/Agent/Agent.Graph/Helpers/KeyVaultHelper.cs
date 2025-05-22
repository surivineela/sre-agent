using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Agent.Graph.Helpers;
public partial class KeyVaultHelper
{
    [GeneratedRegex("^https://([a-zA-Z0-9\\-]+)\\.vault\\.azure\\.net/*")]
    public static partial Regex KeyValutUriRegex();

    public static string ExtractKeyVaultName(Uri keyVaultUri)
    {
        var uri = keyVaultUri.ToString();
        if (string.IsNullOrEmpty(uri))
        {
            return string.Empty;
        }

        var match = KeyValutUriRegex().Match(uri);
        if (!match.Success)
        {
            return string.Empty;
        }

        return match.Groups[1].Value;
    }
}
