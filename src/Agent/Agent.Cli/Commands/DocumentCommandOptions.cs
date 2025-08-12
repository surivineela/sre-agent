using System.CommandLine;

namespace Agent.Cli.Commands;

/// <summary>
/// Builds command-line options for document commands.
/// </summary>
public static class DocumentCommandOptions
{
    // Document upload options (mutually exclusive)
    public static readonly Option<string> FileOption = new("--file")
    {
        Description = "Path to a single document file to upload (.md, .txt files supported)"
    };

    public static readonly Option<string> FolderOption = new("--folder")
    {
        Description = "Path to a folder containing documents to upload (.md, .txt files will be discovered recursively)"
    };

    // Optional parameters for upload
    public static readonly Option<bool> TriggerIndexingOption = new("--trigger-indexing")
    {
        Description = "Trigger indexing after upload for immediate availability (default: true)",
        Arity = ArgumentArity.ZeroOrOne
    };

    public static readonly Option<bool> NoIndexingOption = new("--no-indexing")
    {
        Description = "Skip indexing after upload (files will need manual indexing later)"
    };

    public static readonly Option<bool> RecursiveOption = new("--recursive")
    {
        Description = "Search for files recursively in subdirectories when using --folder (default: true)",
        Arity = ArgumentArity.ZeroOrOne
    };

    // Document search options
    public static readonly Option<string> QueryOption = new("--query")
    {
        Description = "Search query to find relevant documents in the knowledge base"
    };

    // Document reindex option
    public static readonly Option<bool> ReindexOption = new("--reindex")
    {
        Description = "Trigger reindexing of all documents in the knowledge base"
    };
}
