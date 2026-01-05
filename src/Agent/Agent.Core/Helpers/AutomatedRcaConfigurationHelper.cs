// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using Agent.Core.Configuration;

namespace Agent.Core.Helpers;

public static class AutomatedRcaConfigurationHelper
{
    private const string LegacyDefaultTag = "RCAPreflightProcessed";

    public static string ResolveResultTag(AutomatedRCASettings? settings, string? orchestratorAgentName)
    {
        if (settings == null)
        {
            return LegacyDefaultTag;
        }

        if (!string.IsNullOrWhiteSpace(orchestratorAgentName)
            && settings.ResultTags != null
            && TryGetValueCaseInsensitive(settings.ResultTags, orchestratorAgentName, out var configuredTag)
            && !string.IsNullOrWhiteSpace(configuredTag))
        {
            return configuredTag;
        }

        if (!string.IsNullOrWhiteSpace(settings.DefaultResultTag))
        {
            return settings.DefaultResultTag!;
        }

        return LegacyDefaultTag;
    }

    public static ThreadLinkResult BuildThreadLink(AutomatedRCASettings? settings, Guid threadId)
    {
        var baseUrl = settings?.WebBaseUrl;

        var isLocal = string.IsNullOrWhiteSpace(baseUrl)
            || baseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            || baseUrl.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || baseUrl.Contains("::1", StringComparison.OrdinalIgnoreCase);

        var threadPath = isLocal
            ? $"/static/#/views/thread/{threadId}"
            : $"/sreDeepLink/views%2Fthread%2F{threadId}";

        var link = isLocal || string.IsNullOrWhiteSpace(baseUrl)
            ? threadPath
            : $"{baseUrl!.TrimEnd('/')}{threadPath}";

        var accessNote = isLocal ? string.Empty : (settings?.AccessNote ?? string.Empty);

        return new ThreadLinkResult(link, isLocal, accessNote);
    }

    private static bool TryGetValueCaseInsensitive(Dictionary<string, string> source, string key, out string value)
    {
        if (source.Comparer == StringComparer.OrdinalIgnoreCase)
        {
            return source.TryGetValue(key, out value!);
        }

        foreach (var kvp in source)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kvp.Value;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }
}

public sealed record ThreadLinkResult(string Link, bool IsLocal, string AccessNote);
