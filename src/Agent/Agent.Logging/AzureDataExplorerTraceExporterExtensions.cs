using System;
using Kusto.Ingest;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Kusto.Data;
using System.Security.Cryptography.X509Certificates;

namespace Agent.Logging;

/// <summary>
/// Extension methods for adding AzureDataExplorerExporter to TracerProviderBuilder.
/// </summary>
public static class AzureDataExplorerExporterExtensions
{
    /// <summary>
    /// Adds Azure Data Explorer exporter to the TracerProviderBuilder.
    /// </summary>
    /// <param name="builder">The <see cref="TracerProviderBuilder"/> to add the exporter to.</param>
    /// <param name="configure">An action to configure the exporter options.</param>
    /// <returns>The <see cref="TracerProviderBuilder"/> instance for chaining additional operations.</returns>
    public static TracerProviderBuilder AddAzureDataExplorerExporter(
        this TracerProviderBuilder builder,
        Action<AzureDataExplorerExporterOptions> configure)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        var options = new AzureDataExplorerExporterOptions();
        configure?.Invoke(options);

        var kustoIngestClient = default(IKustoIngestClient);
        if (options.FirstPartyAppCertificatePath != "" && options.FirstPartyAppClientId != "" && options.FirstPartyAppTenantId != "")
        {
            var certPem = File.ReadAllText($"{options.FirstPartyAppCertificatePath}/tls.crt");
            var keyPem = File.ReadAllText($"{options.FirstPartyAppCertificatePath}/tls.key");
            var certificate = X509Certificate2.CreateFromPem(certPem, keyPem);
            var kustoConnectionStringBuilder = new KustoConnectionStringBuilder(options.ClusterUri)
                        .WithAadApplicationCertificateAuthentication(applicationClientId: options.FirstPartyAppClientId, certificate, authority: options.FirstPartyAppTenantId, sendX5c: true);

            kustoIngestClient = KustoIngestFactory.CreateDirectIngestClient(kustoConnectionStringBuilder);
        }
        else
        {
            if (string.IsNullOrEmpty(options.ClusterUri))
            {
                throw new ArgumentException("ClusterUri must be specified", nameof(configure));
            }
            var kustoConnectionStringBuilder = new KustoConnectionStringBuilder(options.ClusterUri)
            .WithAadAzCliAuthentication();
            kustoIngestClient = KustoIngestFactory.CreateDirectIngestClient(kustoConnectionStringBuilder);
        }

        if (string.IsNullOrEmpty(options.DatabaseName))
        {
            throw new ArgumentException("DatabaseName must be specified", nameof(configure));
        }

        if (string.IsNullOrEmpty(options.TableName))
        {
            throw new ArgumentException("TableName must be specified", nameof(configure));
        }

        // Register the exporter with the TracerProviderBuilder using a factory function
        return builder.AddProcessor(sp =>
        {
            var exporter = new AzureDataExplorerExporter(
                kustoIngestClient,
                options.DatabaseName,
                options.TableName);

            // Use the built-in BatchActivityExportProcessor from OpenTelemetry
            return new BatchActivityExportProcessor(
                exporter,
                options.MaxQueueSize,
                options.ScheduledDelayMilliseconds,
                options.ExporterTimeoutMilliseconds,
                options.MaxExportBatchSize);
        });
    }
}

/// <summary>
/// Options for configuring the Azure Data Explorer exporter.
/// </summary>
public class AzureDataExplorerExporterOptions
{
    /// <summary>
    /// Gets or sets the database name to ingest data into.
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the table name to ingest data into.
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the cluster uri to ingest data into.
    /// </summary>
    public string ClusterUri { get; set; } = string.Empty;

    public string FirstPartyAppClientId { get; set; } = string.Empty;
    public string FirstPartyAppTenantId { get; set; } = string.Empty;
    public string FirstPartyAppCertificatePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether to use batch processing.
    /// When true, activities are first collected in a batch before being sent to Kusto.
    /// </summary>
    public bool UseBatchProcessing { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum size of the queue used by the batch processor.
    /// </summary>
    public int MaxQueueSize { get; set; } = 2048;

    /// <summary>
    /// Gets or sets the delay between scheduled batch exports in milliseconds.
    /// </summary>
    public int ScheduledDelayMilliseconds { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the exporter timeout in milliseconds.
    /// </summary>
    public int ExporterTimeoutMilliseconds { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the maximum batch size for exports.
    /// </summary>
    public int MaxExportBatchSize { get; set; } = 512;
}
