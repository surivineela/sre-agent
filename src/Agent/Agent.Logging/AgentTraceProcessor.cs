using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Agent.Logging;

public class AgentTraceProcessor : BaseProcessor<Activity>
{
    private readonly Func<Activity, bool> _filter;
    private readonly ILogger _logger;
    private readonly List<BatchActivityExportProcessor> _batchProcessors = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentTraceProcessor"/> class with the specified logger
    /// and multiple exporters.
    /// </summary>
    /// <param name="logger">The logger to use.</param>
    /// <param name="exporters">Collection of exporters to use for processing activities</param>
    /// <param name="maxQueueSize">The maximum queue size for batching. After the size is reached data are dropped. Default is 2048.</param>
    /// <param name="scheduledDelayMilliseconds">The delay interval in milliseconds between two consecutive exports. Default is 5000.</param>
    /// <param name="exporterTimeoutMilliseconds">How long the export can run before it is cancelled. Default is 30000.</param>
    /// <param name="maxExportBatchSize">The maximum batch size of every export. Default is 512.</param>
    public AgentTraceProcessor(
        ILogger logger,
        IEnumerable<BaseExporter<Activity>>? exporters = null,
        int maxQueueSize = 2048,
        int scheduledDelayMilliseconds = 5000,
        int exporterTimeoutMilliseconds = 30000,
        int maxExportBatchSize = 512)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (exporters != null)
        {
            foreach (var exporter in exporters)
            {
                var batchProcessor = new BatchActivityExportProcessor(
                    exporter,
                    maxQueueSize,
                    scheduledDelayMilliseconds,
                    exporterTimeoutMilliseconds,
                    maxExportBatchSize);

                _batchProcessors.Add(batchProcessor);
            }
        }
        _filter = activity =>
        {
            var operationName = activity?.Tags.FirstOrDefault(t => t.Key == "operation.name").Value;
            return operationName is string name && !string.IsNullOrEmpty(name);
        };
    }

    public override void OnEnd(Activity activity)
    {
        if (_filter(activity))
        {
            var operationName = activity.Tags.FirstOrDefault(t => t.Key == "operation.name").Value;
            _logger.LogInformation($"Processing activity with operation name: {operationName}");

            // If any batch processors are configured, send the activity to each one
            if (_batchProcessors.Count > 0)
            {
                // Process the activity through each batch processor
                foreach (var batchProcessor in _batchProcessors)
                {
                    // Send the activity to the batch processor which will handle batching
                    batchProcessor.OnEnd(activity);
                }
            }

            base.OnEnd(activity);
        }
    }

    /// <inheritdoc/>
    protected override bool OnForceFlush(int timeoutMilliseconds)
    {
        bool result = true;

        // Force flush all batch processors
        foreach (var batchProcessor in _batchProcessors)
        {
            result = batchProcessor.ForceFlush(timeoutMilliseconds) && result;
        }

        return result && base.OnForceFlush(timeoutMilliseconds);
    }

    /// <inheritdoc/>
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        bool result = true;

        // Shut down all batch processors
        foreach (var batchProcessor in _batchProcessors)
        {
            result = batchProcessor.Shutdown(timeoutMilliseconds) && result;
        }

        return result && base.OnShutdown(timeoutMilliseconds);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Dispose all batch processors
            foreach (var batchProcessor in _batchProcessors)
            {
                batchProcessor.Dispose();
            }

            _batchProcessors.Clear();
        }

        base.Dispose(disposing);
    }
}
