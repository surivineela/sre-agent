// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.RegularExpressions;

namespace Agent.Plugins.DataConnectors.TSG
{
    /// <summary>
    /// Helper class for TSG document processing and metadata extraction
    /// </summary>
    internal static class TsgDocumentHelper
    {
        /// <summary>
        /// Determines if a file is a plain text file based on its extension
        /// </summary>
        /// <param name="filePath">The file path to check</param>
        /// <returns>True if the file is a supported text file type</returns>
        public static bool IsPlainTextFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            // Support common text-based file types that might contain TSG content
            string[] supportedExtensions = {
                ".md", ".txt", ".rst", ".adoc", ".asciidoc",
                ".wiki", ".textile", ".org", ".htm", ".html",
                ".json", ".yaml", ".yml", ".xml", ".csv"
            };

            return supportedExtensions.Contains(extension);
        }

        /// <summary>
        /// Parses Azure DevOps URI to extract organization, project, and repository information
        /// </summary>
        /// <param name="uri">The Azure DevOps URI to parse</param>
        /// <returns>Tuple containing organization, project name, and repository name, or null if parsing fails</returns>
        public static (string Organization, string ProjectName, string RepositoryName)? ParseAzureDevOpsUri(Uri uri)
        {
            try
            {
                // Examples:
                // https://dev.azure.com/myorg/myproject/_git/myrepo
                // https://myorg.visualstudio.com/myproject/_git/myrepo

                if (uri.Host == "dev.azure.com")
                {
                    var pathSegments = uri.AbsolutePath.Trim('/').Split('/');
                    if (pathSegments.Length >= 4)
                    {
                        return (pathSegments[0], pathSegments[1], pathSegments[3]); // Skip "_git"
                    }
                }
                else if (uri.Host.EndsWith(".visualstudio.com"))
                {
                    var organization = uri.Host.Replace(".visualstudio.com", "");
                    var pathSegments = uri.AbsolutePath.Trim('/').Split('/');
                    if (pathSegments.Length >= 3)
                    {
                        return (organization, pathSegments[0], pathSegments[2]); // Skip "_git"
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Generates a unique document ID for the TSG document
        /// </summary>
        /// <param name="repository">Repository settings</param>
        /// <param name="filePath">File path within the repository</param>
        /// <returns>Sanitized document ID suitable for Azure Search</returns>
        public static string GenerateDocumentId(AzureDevOpsSettings repository, string filePath)
        {
            // Create the base ID
            string baseId = $"{repository.Organization}-{repository.ProjectName}-{repository.RepositoryName}-{filePath.Replace('/', '-').Replace('\\', '-')}";

            // Sanitize for Azure Search document key requirements
            // Azure Search keys can only contain letters, digits, underscore (_), dash (-), or equal sign (=)
            return SanitizeDocumentId(baseId);
        }

        /// <summary>
        /// Sanitizes a document ID to comply with Azure Search key requirements
        /// </summary>
        /// <param name="documentId">The document ID to sanitize</param>
        /// <returns>Sanitized document ID</returns>
        public static string SanitizeDocumentId(string documentId)
        {
            if (string.IsNullOrEmpty(documentId))
                return string.Empty;

            // Remove or replace invalid characters for Azure Search document keys
            // Valid characters: letters, digits, underscore (_), dash (-), equal sign (=)
            var sanitized = new StringBuilder();

            foreach (char c in documentId)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '=')
                {
                    sanitized.Append(c);
                }
                else
                {
                    // Replace invalid characters with underscore
                    sanitized.Append('_');
                }
            }

            string result = sanitized.ToString();

            // Ensure the result is not empty and doesn't start/end with invalid characters
            if (string.IsNullOrEmpty(result))
            {
                result = "document_" + Guid.NewGuid().ToString("N")[..8]; // Use first 8 chars of GUID as fallback
            }

            // Remove consecutive underscores for cleaner IDs
            result = Regex.Replace(result, "_+", "_");

            // Trim underscores from start/end
            result = result.Trim('_');

            // Ensure it's not empty after trimming
            if (string.IsNullOrEmpty(result))
            {
                result = "document_" + Guid.NewGuid().ToString("N")[..8];
            }

            return result;
        }

        /// <summary>
        /// Extracts the title from the document content or file path
        /// </summary>
        /// <param name="filePath">The file path</param>
        /// <param name="content">The file content</param>
        /// <returns>Extracted title</returns>
        public static string ExtractTitle(string filePath, string content)
        {
            // Try to extract title from markdown header
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines.Take(10)) // Check first 10 lines
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("# "))
                {
                    return trimmed.Substring(2).Trim();
                }
            }

            // Fall back to file name without extension
            return Path.GetFileNameWithoutExtension(filePath);
        }

        /// <summary>
        /// Determines the document type based on file extension
        /// </summary>
        /// <param name="filePath">The file path</param>
        /// <returns>Document type string</returns>
        public static string DetermineDocumentType(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".md" => "Markdown",
                ".txt" => "Text",
                ".rst" => "RestructuredText",
                ".adoc" or ".asciidoc" => "AsciiDoc",
                ".wiki" => "Wiki",
                ".html" or ".htm" => "HTML",
                ".json" => "JSON",
                ".yaml" or ".yml" => "YAML",
                ".xml" => "XML",
                _ => "Document"
            };
        }

        /// <summary>
        /// Extracts service name from repository or file path
        /// </summary>
        /// <param name="repository">Repository settings</param>
        /// <param name="filePath">File path within the repository</param>
        /// <returns>Extracted service name</returns>
        public static string ExtractServiceName(AzureDevOpsSettings repository, string filePath)
        {
            // Extract service name from path or use repository name as fallback
            var pathParts = filePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (pathParts.Length > 1)
            {
                return pathParts[0]; // Use first directory as service name
            }
            return repository.RepositoryName;
        }

        /// <summary>
        /// Extracts tags from document content and file path
        /// </summary>
        /// <param name="content">Document content</param>
        /// <param name="filePath">File path</param>
        /// <returns>List of extracted tags</returns>
        public static List<string> ExtractTags(string content, string filePath)
        {
            var tags = new List<string>();

            // Add tags based on file path
            var pathParts = filePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            tags.AddRange(pathParts.Take(pathParts.Length - 1)); // All directories in path

            // Add tags based on file extension
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (!string.IsNullOrEmpty(extension))
            {
                tags.Add(extension.TrimStart('.'));
            }

            // Add common TSG-related tags based on content
            string contentLower = content.ToLowerInvariant();
            if (contentLower.Contains("troubleshoot")) tags.Add("troubleshooting");
            if (contentLower.Contains("debug")) tags.Add("debugging");
            if (contentLower.Contains("error")) tags.Add("error-handling");
            if (contentLower.Contains("issue")) tags.Add("issue-resolution");
            if (contentLower.Contains("fix")) tags.Add("fix");
            if (contentLower.Contains("solution")) tags.Add("solution");

            return tags.Distinct().ToList();
        }

        /// <summary>
        /// Extracts team information from repository or file path
        /// </summary>
        /// <param name="repository">Repository settings</param>
        /// <param name="filePath">File path within the repository</param>
        /// <returns>Extracted team name or null</returns>
        public static string? ExtractTeam(AzureDevOpsSettings repository, string filePath)
        {
            // Extract team from path structure or repository naming
            var pathParts = filePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (pathParts.Length > 2)
            {
                return pathParts[1]; // Use second directory level as team
            }
            return null;
        }

        /// <summary>
        /// Generates a URL for the document in Azure DevOps
        /// </summary>
        /// <param name="repository">Repository settings</param>
        /// <param name="filePath">File path within the repository</param>
        /// <returns>Azure DevOps URL for the document</returns>
        public static string GenerateDocumentUrl(AzureDevOpsSettings repository, string filePath)
        {
            return $"https://dev.azure.com/{repository.Organization}/{repository.ProjectName}/_git/{repository.RepositoryName}?path={filePath}";
        }

        /// <summary>
        /// Creates a safe repository name for blob storage
        /// </summary>
        /// <param name="dataSourceUri">The data source URI</param>
        /// <returns>Safe repository name for blob storage</returns>
        public static string GetSafeRepositoryName(Uri dataSourceUri)
        {
            return dataSourceUri.Host
                .ToLowerInvariant()
                .Replace(".", "-") + "-" +
                dataSourceUri.AbsolutePath
                .Trim('/')
                .Replace("/", "-")
                .Replace("_", "-")
                .ToLowerInvariant();
        }

        /// <summary>
        /// Parses a TSG document from file content and metadata
        /// </summary>
        /// <param name="filePath">The file path</param>
        /// <param name="content">The file content</param>
        /// <param name="repository">Repository settings</param>
        /// <param name="dataSourceUri">Data source URI</param>
        /// <returns>Parsed TSG document metadata or null if parsing fails</returns>
        public static TsgDocumentMetadata? ParseTsgDocument(string filePath, string content, AzureDevOpsSettings repository, Uri dataSourceUri)
        {
            try
            {
                // Create a unique identifier for the document
                string id = GenerateDocumentId(repository, filePath);

                // Extract title from file name or content
                string title = ExtractTitle(filePath, content);

                // Determine document type based on file extension
                string documentType = DetermineDocumentType(filePath);

                // Extract service name from repository or path
                string serviceName = ExtractServiceName(repository, filePath);

                // Extract tags from content or path
                var tags = ExtractTags(content, filePath);

                // Create the document URL
                string url = GenerateDocumentUrl(repository, filePath);

                var document = new TsgDocumentMetadata
                {
                    Id = id,
                    Title = title,
                    Contents = content,
                    Filter = serviceName, // Use service name as filter
                    Source = dataSourceUri.ToString(),
                    DocumentType = documentType,
                    ServiceName = serviceName,
                    Url = url,
                    LastModified = DateTime.UtcNow, // TODO: Get actual last modified date from Azure DevOps
                    IndexedAt = DateTime.UtcNow,
                    Tags = tags,
                    Team = ExtractTeam(repository, filePath),
                    Repository = repository.RepositoryName,
                    FilePath = filePath,
                    MetadataConcat = $"{title} {serviceName} {string.Join(" ", tags)} {repository.RepositoryName}"
                };

                return document;
            }
            catch
            {
                return null;
            }
        }
    }
}
