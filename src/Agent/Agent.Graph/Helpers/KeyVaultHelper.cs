// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;

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
