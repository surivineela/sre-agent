// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.RegularExpressions;
using k8s.Models;

namespace Agent.Graph.Crawler;
public static partial class KubernetesHelper
{
    [GeneratedRegex("^(?:http:\\/\\/|https:\\/\\/)?(?<serviceName>[a-z](?:[a-z0-9-]*[a-z0-9])?)(?<serviceNamespace>\\.[a-z](?:[a-z0-9-]*[a-z0-9])?)?:\\d+$")]
    private static partial Regex ServiceUrlRegex();

    public static string ConstructLabelSelector(V1LabelSelector selectors)
    {
        if (selectors == null)
        {
            return string.Empty;
        }
        var labelSelector = new StringBuilder();
        if (selectors.MatchLabels != null && selectors.MatchLabels.Count > 0)
        {
            labelSelector.Append(string.Join(",", selectors.MatchLabels.Select(kvp => $"{kvp.Key}={kvp.Value}")));
        }
        if (selectors.MatchExpressions != null && selectors.MatchExpressions.Count > 0)
        {
            foreach (var expression in selectors.MatchExpressions)
            {
                var operatorString = expression.OperatorProperty.ToString().ToLower();
                var values = string.Join(",", expression.Values);
                labelSelector.Append($"{expression.Key} {operatorString} {values}");
            }
        }

        return labelSelector.ToString();
    }

    public static string ConstructLabelSelector(IDictionary<string, string> selectors)
    {
        if (selectors == null)
        {
            return string.Empty;
        }
        var labelSelector = new StringBuilder();
        if (selectors.Count > 0)
        {
            labelSelector.Append(string.Join(",", selectors.Select(kvp => $"{kvp.Key}={kvp.Value}")));
        }
        return labelSelector.ToString();
    }

    // try to extract service name and service namespace if the val is a service url
    public static bool TryMatchServiceUrl(string val, out string serviceName, out string serviceNamespace)
    {
        var match = ServiceUrlRegex().Match(val);
        if (match.Success)
        {
            serviceName = match.Groups["serviceName"].Value;
            serviceNamespace = match.Groups["serviceNamespace"].Value.TrimStart('.');
            return true;
        }
        else
        {
            serviceName = string.Empty;
            serviceNamespace = string.Empty;
            return false;
        }
    }
}

