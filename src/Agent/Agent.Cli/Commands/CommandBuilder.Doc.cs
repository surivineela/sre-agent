// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

public static partial class CommandBuilder
{
    private static class DocumentCommand
    {
        public static Command Build()
        {
            var doc = new Command("doc", "Document management commands. Upload and manage documents like TSGs, architecture docs, runbooks, and other reference materials for agents to use")
            {
                CreateDocumentUploadCommand(),
                CreateDocumentSearchCommand(),
                CreateDocumentReindexCommand()
            };

            // Add default action for doc command to show formatted help
            doc.SetAction(pr => ShowFormattedDocHelp(doc));
            return doc;
        }

        private static Command CreateDocumentUploadCommand()
        {
            var cmd = new Command("upload", CommandExamples.Document.UploadDescription)
            {
                DocumentCommandOptions.FileOption,
                DocumentCommandOptions.FolderOption,
                DocumentCommandOptions.TriggerIndexingOption,
                DocumentCommandOptions.NoIndexingOption,
                DocumentCommandOptions.RecursiveOption
            };

            cmd.SetAction(DocumentCommandHandlers.HandleUploadCommand);
            return cmd;
        }

        private static Command CreateDocumentSearchCommand()
        {
            var cmd = new Command("search", CommandExamples.Document.SearchDescription)
            {
                DocumentCommandOptions.QueryOption
            };

            cmd.SetAction(DocumentCommandHandlers.HandleSearchCommand);
            return cmd;
        }

        private static Command CreateDocumentReindexCommand()
        {
            var cmd = new Command("reindex", CommandExamples.Document.ReindexDescription);
            cmd.SetAction(DocumentCommandHandlers.HandleReindexCommand);
            return cmd;
        }
    }
}
