namespace Agent.Core.Helpers;

/// <summary>
/// Helper methods for normalizing and validating code repository URLs.
/// </summary>
public static class CodeRepoUrlHelper
{
    /// <summary>
    /// Normalizes a repository URL to a consistent format.
    /// </summary>
    /// <param name="url">The URL to normalize.</param>
    /// <returns>The normalized URL.</returns>
    /// <exception cref="ArgumentException">Thrown when the URL is invalid or uses an unsupported protocol.</exception>
    public static string NormalizeRepoUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL cannot be null or whitespace.", nameof(url));
        }

        // Try to parse the URL
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException($"Invalid URL format: {url}", nameof(url));
        }

        // Only allow HTTP and HTTPS protocols
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException($"Only HTTP and HTTPS protocols are supported. URL uses '{uri.Scheme}' protocol.", nameof(url));
        }

        // Build normalized URL
        var builder = new UriBuilder(uri)
        {
            Scheme = Uri.UriSchemeHttps, // Always use HTTPS
            Port = -1, // Remove explicit port (use default)
            Fragment = string.Empty, // Remove fragment
            Query = string.Empty // Remove query parameters
        };

        // Lowercase the host
        builder.Host = builder.Host.ToLowerInvariant();

        // Remove .git suffix from path
        var path = builder.Path.TrimEnd('/');
        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            path = path.Substring(0, path.Length - 4);
        }

        // Remove trailing slashes
        builder.Path = path.TrimEnd('/');

        return builder.Uri.ToString();
    }

    /// <summary>
    /// Extracts the organization name from an Azure DevOps repository URL.
    /// </summary>
    /// <param name="url">The Azure DevOps repository URL.</param>
    /// <returns>The organization name, or null if it cannot be extracted.</returns>
    public static string? ExtractAzureDevOpsOrganization(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        try
        {
            var uri = new Uri(url);

            // Handle both formats:
            // https://dev.azure.com/{org}/{project}/_git/{repo}
            // https://{org}.visualstudio.com/{project}/_git/{repo}

            if (uri.Host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase))
            {
                var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length > 0)
                {
                    return segments[0]; // First segment is the organization
                }
            }
            else if (uri.Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase))
            {
                // Extract org from subdomain (e.g., myorg.visualstudio.com)
                var hostParts = uri.Host.Split('.');
                if (hostParts.Length > 0)
                {
                    return hostParts[0];
                }
            }
        }
        catch
        {
            // Invalid URL, return null
        }

        return null;
    }
}
