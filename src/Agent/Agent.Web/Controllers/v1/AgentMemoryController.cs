// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Models;
using Agent.Data.AgentMemory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using System.Collections.Concurrent;
using Agent.Web.Authorization;
using ArmOperations = Agent.Core.Constants.ArmOperations;

namespace Agent.Web.Controllers.v1
{
    internal record FailedUpload(string FileName, string ErrorMessage);
    [ApiController]
    [Route("api/v1/[controller]")]
    public class AgentMemoryController(ILogger<AgentMemoryController> logger,
                                    IAgentMemoryClient agentMemoryClient,
                                    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
                                    ISearchIndexService searchIndexService,
                                    AgentMemorySettings agentMemorySettings) : ControllerBase
    {
        private HashSet<string> allowedExtensions = [".md", ".txt"];

        [HttpPost("upload")]
        [AuthorizeArmOperation(ArmOperations.AgentMemoryWriteActionId)]
        [RequestFormLimits(MultipartBodyLengthLimit = 100 * 1024 * 1024)] // 100MB limit for the entire request
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadDocument(
            [FromForm] List<IFormFile> files,
            [FromForm] bool triggerIndexing = true) // Default to true for immediate availability of docs for retrieval
        {
            if (files == null || files.Count == 0)
            {
                return BadRequest(new { error = "No files provided" });
            }

            logger.LogInternalInformation($"Received {files.Count} files for upload");

            // Azure AI Search has a maximum file size limit of 16MB
            const long maxFileSize = 16 * 1024 * 1024; // 16MB

            var failedUploads = new List<FailedUpload>();
            var successfulUploads = new List<string>();

            foreach (var file in files)
            {
                if (file == null || file.Length == 0)
                {
                    failedUploads.Add(new FailedUpload(file?.FileName ?? "unknown", "File name or content is empty"));
                    continue;
                }

                if (file.Length > maxFileSize)
                {
                    failedUploads.Add(new FailedUpload(file.FileName, "File exceeds maximum size of 16MB"));
                    continue;
                }

                if (!allowedExtensions.Contains(Path.GetExtension(file.FileName)))
                {
                    failedUploads.Add(new FailedUpload(file.FileName, "File type not allowed"));
                    continue;
                }

                var safeFileName = GetSafeBlobName(file.FileName);
                if (string.IsNullOrWhiteSpace(safeFileName))
                {
                    failedUploads.Add(new FailedUpload(file.FileName, "Invalid file name or too long"));
                    continue;
                }

                try
                {
                    using var stream = file.OpenReadStream();
                    var uploadSuccess = await agentMemoryClient.UploadDocumentAsync(safeFileName, stream);

                    if (!uploadSuccess)
                    {
                        logger.LogInternalError($"Failed to upload file: {file.FileName} - Upload returned false");
                        failedUploads.Add(new FailedUpload(file.FileName, $"Failed to upload file to storage"));
                    }
                    else
                    {
                        successfulUploads.Add(file.FileName);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogInternalError(ex, $"Failed to upload file: {file.FileName}");
                    // TODO: allow uploading files concurrently
                    failedUploads.Add(new FailedUpload(file.FileName, $"Failed to upload file"));
                }
            }

            // Trigger indexing if requested and at least one file was uploaded successfully
            bool indexingTriggered = false;
            if (triggerIndexing && successfulUploads.Count > 0)
            {
                try
                {
                    logger.LogInternalInformation($"Triggering indexer for {successfulUploads.Count} newly uploaded files");
                    await agentMemoryClient.RunIndexerAsync();
                    indexingTriggered = true;
                }
                catch (Exception ex)
                {
                    logger.LogInternalError(ex, "Failed to trigger indexing after upload");
                    // Don't fail the entire request if indexing fails - files are still uploaded
                }
            }

            if (failedUploads.Count > 0)
            {
                return BadRequest(new
                {
                    error = "Failed to upload some files",
                    detail = failedUploads,
                    uploaded = successfulUploads,
                    indexingTriggered
                });
            }

            return Ok(new
            {
                message = $"Files uploaded successfully{(indexingTriggered ? " and indexing triggered" : "")}.",
                uploaded = successfulUploads,
                indexingTriggered
            });
        }

        // Delete a single document
        [HttpDelete("document/{fileName}")]
        [AuthorizeArmOperation(ArmOperations.AgentMemoryDeleteActionId)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteDocument(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return BadRequest(new { error = "File name must be provided" });
            }

            logger.LogInternalInformation($"Received request to delete document: {fileName}");

            // Validate file extension if you want to ensure only certain types can be deleted
            var extension = Path.GetExtension(fileName);
            if (!string.IsNullOrEmpty(extension) && !allowedExtensions.Contains(extension))
            {
                return BadRequest(new { error = "File type not allowed for deletion" });
            }

            var safeFileName = GetSafeBlobName(fileName);
            if (string.IsNullOrWhiteSpace(safeFileName))
            {
                return BadRequest(new { error = "Invalid file name" });
            }

            try
            {
                var deleteSuccess = await agentMemoryClient.DeleteDocumentAsync(safeFileName);

                if (!deleteSuccess)
                {
                    logger.LogInternalWarning($"Document not found or could not be deleted: {fileName}");
                    return NotFound(new { error = $"Document '{fileName}' not found or could not be deleted" });
                }

                logger.LogInternalInformation($"Successfully deleted document: {fileName}");

                await agentMemoryClient.RunIndexerAsync();

                return Ok(new { message = $"Document '{fileName}' deleted successfully" });
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, $"Failed to delete document: {fileName}");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "Failed to delete document" });
            }
        }

        // Delete multiple documents at once
        [HttpDelete("documents")]
        [AuthorizeArmOperation(ArmOperations.AgentMemoryDeleteActionId)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteDocuments([FromBody] List<string> fileNames)
        {
            if (fileNames == null || fileNames.Count == 0)
            {
                return BadRequest(new { error = "No file names provided" });
            }

            logger.LogInternalInformation($"Received request to delete {fileNames.Count} documents");

            var failedDeletions = new ConcurrentBag<FailedUpload>();
            var successfulDeletions = new ConcurrentBag<string>();

            // Configure parallelization settings
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Min(10, fileNames.Count) // Limit concurrency
            };

            await Parallel.ForEachAsync(fileNames, parallelOptions, async (fileName, ct) =>
            {
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    failedDeletions.Add(new FailedUpload(fileName ?? "unknown", "Invalid file name"));
                    return;
                }

                var safeFileName = GetSafeBlobName(fileName);
                if (string.IsNullOrWhiteSpace(safeFileName))
                {
                    failedDeletions.Add(new FailedUpload(fileName, "Invalid file name format"));
                    return;
                }

                try
                {
                    var deleteSuccess = await agentMemoryClient.DeleteDocumentAsync(safeFileName);

                    if (!deleteSuccess)
                    {
                        failedDeletions.Add(new FailedUpload(fileName, "Document not found or could not be deleted"));
                    }
                    else
                    {
                        successfulDeletions.Add(fileName);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogInternalError(ex, $"Failed to delete document: {fileName}");
                    failedDeletions.Add(new FailedUpload(fileName, "Failed to delete document"));
                }
            });

            // Convert ConcurrentBag to List for the response
            var successList = successfulDeletions.ToList();
            var failedList = failedDeletions.ToList();

            if (successList.Count == 0 && failedList.Count > 0)
            {
                return BadRequest(new { error = "Failed to delete all documents", detail = failedList });
            }

            if (successList.Count > 0)
            {
                await agentMemoryClient.RunIndexerAsync();
            }

            return Ok(new
            {
                message = $"Deleted {successList.Count} document(s) successfully",
                deleted = successList,
                failed = failedList
            });
        }

        // Get the status of the AgentMemory feature
        [HttpGet("status")]
        [AuthorizeArmOperation(ArmOperations.AgentMemoryReadActionId)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetAgentMemoryStatus()
        {
            try
            {
                if (agentMemorySettings == null)
                {
                    return Ok(new
                    {
                        enabled = false,
                        documentRetrievalEnabled = false,
                        trajectoryRetrievalEnabled = false,
                        userMemoryRetrievalEnabled = false,
                        message = "AgentMemory configuration not found"
                    });
                }

                return Ok(new
                {
                    enabled = agentMemorySettings.Enabled,
                    documentRetrievalEnabled = agentMemorySettings.DocumentRetrievalEnabled,
                    trajectoryRetrievalEnabled = agentMemorySettings.TrajectoryRetrievalEnabled,
                    userMemoryRetrievalEnabled = agentMemorySettings.UserMemoryRetrievalEnabled,
                    message = agentMemorySettings.Enabled ? "AgentMemory is enabled" : "AgentMemory is disabled"
                });
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Failed to retrieve AgentMemory status");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "Failed to retrieve AgentMemory status" });
            }
        }

        // Azure Blob Storage legal name helper
        private static string? GetSafeBlobName(string fileName)
        {
            // Remove invalid chars, trim, and ensure length is valid for Azure Blob Storage
            // Azure blob names cannot contain: \, /, ?, #, and must be between 1 and 1024 chars
            var invalidChars = Path.GetInvalidFileNameChars().Concat(new[] { '\\', '/', '?', '#' }).ToArray();
            var safeName = new string(fileName.Where(ch => !invalidChars.Contains(ch)).ToArray());
            safeName = safeName.Trim().TrimEnd('.');
            if (safeName.Length == 0 || safeName.Length > 1024)
                return null;
            return safeName;
        }

        [HttpPost("rebuildIndex")]
        [AuthorizeArmOperation(ArmOperations.AgentMemoryWriteActionId)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> TriggerIndexing()
        {
            try
            {
                await agentMemoryClient.RunIndexerAsync();
                return Ok(new { message = "Indexing triggered successfully." });
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Failed to trigger indexing.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to trigger indexing." });
            }
        }

        [HttpPost("indexTrajectory")]
        [AuthorizeArmOperation(ArmOperations.AgentMemoryWriteActionId)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> IndexTrajectory([FromBody] ProcessedTrajectoryOutput_v3 trajectoryOutput)
        {
            logger.LogInternalInformation($"Received trajectory for indexing");

            try
            {
                var embedding = await embeddingGenerator.GenerateVectorForAgentMemoryAsync(trajectoryOutput.SymptomsObserved, logger);
                var memory = AgentMemory.FromTrajectory(
                    id: Guid.NewGuid().ToString(),
                    trajectoryData: trajectoryOutput,
                    embedding: [.. embedding.Span]);

                var result = await searchIndexService.IndexContentAsync(memory);

                if (!result)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to index trajectory content." });
                }

                return Ok(new { message = "Trajectory indexed successfully." });
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Failed to index trajectory.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Failed to index trajectory: {ex.Message}" });
            }
        }

        [HttpGet("documents")]
        [AuthorizeArmOperation(ArmOperations.AgentMemoryReadActionId)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SearchDocuments([FromQuery] string query, [FromQuery] string? filter = null, [FromQuery] uint k = 5, [FromQuery] float? vectorSimilarityThreshold = null, [FromQuery] bool enableHybridSearch = false)
        {
            if (string.IsNullOrWhiteSpace(query) || k <= 0)
            {
                return BadRequest(new { error = "Query must be provided and k must be greater than 0." });
            }

            try
            {
                var results = await agentMemoryClient.SearchCustomerDocumentsAsync(new SearchParams(
                    Query: query,
                    K: k,
                    VectorSimilarityThreshold: vectorSimilarityThreshold,
                    Filter: filter,
                    EnableHybridSearch: enableHybridSearch
                ));
                return Ok(new { results });
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Failed to search documents.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to search documents." });
            }
        }

        [HttpGet("trajectories")]
        [AuthorizeArmOperation(ArmOperations.AgentMemoryReadActionId)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SearchTrajectories([FromQuery] string query, [FromQuery] string? filter = null, [FromQuery] uint k = 5, [FromQuery] float? vectorSimilarityThreshold = null, [FromQuery] bool enableHybridSearch = false)
        {
            if (string.IsNullOrWhiteSpace(query) || k <= 0)
            {
                return BadRequest(new { error = "Query must be provided and k must be greater than 0." });
            }

            try
            {
                var results = await agentMemoryClient.SearchTrajectoriesAsync(new SearchParams(
                    Query: query,
                    K: k,
                    VectorSimilarityThreshold: vectorSimilarityThreshold,
                    Filter: filter,
                    EnableHybridSearch: enableHybridSearch
                ));
                return Ok(new { results });
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Failed to search documents.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to search documents." });
            }
        }

        [HttpGet("userMemories")]
        [AuthorizeArmOperation(ArmOperations.AgentMemoryReadActionId)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SearchUserMemory([FromQuery] string query, [FromQuery] string? filter = null, [FromQuery] uint k = 5, [FromQuery] float? vectorSimilarityThreshold = null, [FromQuery] bool enableHybridSearch = false)
        {
            if (string.IsNullOrWhiteSpace(query) || k <= 0)
            {
                return BadRequest(new { error = "Query must be provided and k must be greater than 0." });
            }

            try
            {
                var results = await agentMemoryClient.SearchUserMemoriesAsync(new SearchParams(
                    Query: query,
                    K: k,
                    VectorSimilarityThreshold: vectorSimilarityThreshold,
                    Filter: filter,
                    EnableHybridSearch: enableHybridSearch
                ));
                return Ok(new { results });
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Failed to search user memory.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to search user memory." });
            }
        }

        [HttpGet("files")]
        [AuthorizeArmOperation(ArmOperations.AgentMemoryReadActionId)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ListFiles(
            [FromQuery] string? prefix = null,
            [FromQuery] int? pageSize = null,
            [FromQuery] string? continuationToken = null,
            CancellationToken cancellationToken = default)
        {

            if (pageSize.HasValue && (pageSize <= 0 || pageSize > 1000))
            {
                return BadRequest(new { error = "pageSize must be between 1 and 1000 if provided." });
            }

            try
            {
                var page = await agentMemoryClient.ListFilesAsync(
                    prefix: prefix,
                    pageSize: pageSize,
                    continuationToken: continuationToken,
                    cancellationToken: cancellationToken);

                return Ok(new
                {
                    files = page.Items,
                    continuationToken = page.ContinuationToken
                });
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Failed to list files.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to list files." });
            }
        }
    }
}
