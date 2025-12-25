// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

/// <summary>
/// Builds command-line options for document commands.
/// </summary>
public static class DocumentCommandOptions
{
    // ============================================================
    // Document Upload Command Options
    // ============================================================

    public static class Upload
    {
        public static readonly Option<string[]> FileOption = new("--file")
        {
            Description = "Path(s) to file(s) or folder(s) to upload (.md, .txt). Can be specified multiple times. Folders are searched recursively.",
            AllowMultipleArgumentsPerToken = true,
            Arity = ArgumentArity.OneOrMore
        };

        public static readonly Option<string> FolderOption = new("--folder")
        {
            Description = "[DEPRECATED] Use --file instead. Path to a folder containing documents to upload"
        };

        public static readonly Option<bool> TriggerIndexingOption = new("--trigger-indexing")
        {
            Description = "[DEPRECATED] Indexing is triggered by default. Use --no-indexing to skip.",
            Arity = ArgumentArity.ZeroOrOne
        };

        public static readonly Option<bool> NoIndexingOption = new("--no-indexing")
        {
            Description = "Skip indexing after upload"
        };

        public static readonly Option<bool> RecursiveOption = new("--recursive")
        {
            Description = "[DEPRECATED] Folders are always searched recursively.",
            Arity = ArgumentArity.ZeroOrOne
        };
    }

    // ============================================================
    // Document Search Command Options
    // ============================================================

    public static class Search
    {
        public static readonly Option<string> QueryOption = new("--query")
        {
            Description = "Search query to find relevant documents"
        };
    }

    // ============================================================
    // Document Get Command Options
    // ============================================================

    public static class Get
    {
        public static readonly Option<string> PrefixOption = new("--prefix")
        {
            Description = "Filter files by prefix"
        };
    }

    // ============================================================
    // Document Delete Command Options
    // ============================================================

    public static class Delete
    {
        public static readonly Option<string> NameOption = new("--name")
        {
            Description = "Name of the document to delete",
            Required = true
        };
    }
}
