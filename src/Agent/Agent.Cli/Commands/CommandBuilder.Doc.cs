// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;

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
                CreateDocumentGetCommand(),
                CreateDocumentDeleteCommand(),
                CreateDocumentReindexCommand()
            };

            return doc;
        }

        private static Command CreateDocumentUploadCommand()
        {
            var cmd = new Command("upload", CommandExamples.Document.UploadDescription)
            {
                DocumentCommandOptions.Upload.FileOption,
                DocumentCommandOptions.Upload.NoIndexingOption,

                // Deprecated options kept for backward compatibility
                DocumentCommandOptions.Upload.FolderOption,
                DocumentCommandOptions.Upload.TriggerIndexingOption,
                DocumentCommandOptions.Upload.RecursiveOption
            };

            cmd.AddValidator(result =>
            {
                var filePaths = result.GetValue(DocumentCommandOptions.Upload.FileOption);
                var folderPath = result.GetValue(DocumentCommandOptions.Upload.FolderOption);

                if ((filePaths == null || filePaths.Length == 0) && string.IsNullOrEmpty(folderPath))
                {
                    result.AddError(ErrorMessageHelper.InvalidParameter("--file must be specified with at least one path."));
                }
            });

            cmd.SetAction(DocumentCommandHandlers.HandleUploadCommand);
            return cmd;
        }

        private static Command CreateDocumentSearchCommand()
        {
            var cmd = new Command("search", CommandExamples.Document.SearchDescription)
            {
                DocumentCommandOptions.Search.QueryOption
            };

            cmd.SetAction(DocumentCommandHandlers.HandleSearchCommand);
            return cmd;
        }

        private static Command CreateDocumentGetCommand()
        {
            var cmd = new Command("get", CommandExamples.Document.GetDescription)
            {
                DocumentCommandOptions.Get.PrefixOption
            };

            cmd.SetAction(DocumentCommandHandlers.HandleGetCommand);
            return cmd;
        }

        private static Command CreateDocumentDeleteCommand()
        {
            var cmd = new Command("delete", CommandExamples.Document.DeleteDescription)
            {
                DocumentCommandOptions.Delete.NameOption
            };

            cmd.SetAction(DocumentCommandHandlers.HandleDeleteCommand);
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
