// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Models;
using Agent.Data.AgentMemory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;

namespace Agent.Web.Controllers.v1
{
    internal record FailedUpload(string FileName, string ErrorMessage);
    [ApiController]
    [Route("api/v1/[controller]")]
    public class DocumentController(ILogger<DocumentController> logger,
                                    IAgentMemoryClient agentMemoryClient,
                                    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
                                    ISearchIndexService searchIndexService) : ControllerBase
    {
        private HashSet<string> allowedExtensions = [".md", ".txt"];

        [HttpPost("upload")]
        [RequestFormLimits(MultipartBodyLengthLimit = 100 * 1024 * 1024)] // 100MB limit for the entire request
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadDocument([FromForm] List<IFormFile> files)
        {
            if (files == null || files.Count == 0)
            {
                return BadRequest(new { error = "No files provided" });
            }

            logger.LogInternalInformation($"Received {files.Count} files for upload");

            // Azure AI Search has a maximum file size limit of 16MB
            const long maxFileSize = 16 * 1024 * 1024; // 16MB

            var failedUploads = new List<FailedUpload>();

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
                }
                catch (Exception ex)
                {
                    logger.LogInternalError(ex, $"Failed to upload file: {file.FileName}");
                    // TODO: allow uploading files concurrently
                    failedUploads.Add(new FailedUpload(file.FileName, $"Failed to upload file"));
                }
            }

            if (failedUploads.Count > 0)
            {
                return BadRequest(new { error = "Failed to upload some files", detail = failedUploads });
            }

            return Ok(new { message = "Files uploaded successfully." });
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
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> IndexTrajectory([FromBody] ProcessedTrajectoryOutput_v3 trajectoryOutput)
        {
            logger.LogInternalInformation($"Received trajectory for indexing");

            try
            {
                var embedding = await embeddingGenerator.GenerateVectorForAgentMemoryAsync(trajectoryOutput.SymptomsObserved);
                var memory = AgentMemory.FromTrajectory(
                    id: Guid.NewGuid().ToString(),
                    trajectoryData: trajectoryOutput,
                    embedding: [..embedding.Span]);

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

        [HttpGet()]
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

    }
}
