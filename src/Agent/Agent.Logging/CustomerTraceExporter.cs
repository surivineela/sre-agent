// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using OpenTelemetry;

namespace Agent.Logging;

/// <summary>
/// Configuration options for the Customer Trace Exporter.
/// Extends AzureDataExplorerExporterOptions with customer-specific security settings.
/// </summary>
public class CustomerTraceExporterOptions : AzureDataExplorerExporterOptions
{
    /// <summary>
    /// Gets or sets whether to include minimal debugging information in customer traces.
    /// Default is false for maximum security.
    /// </summary>
    public bool IncludeMinimalDebugInfo { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum length for message attributes in customer traces.
    /// Default is 500 characters to prevent exposure of large internal data.
    /// </summary>
    public int MaxMessageLength { get; set; } = 500;

    /// <summary>
    /// Gets or sets whether to include span events in customer traces.
    /// Default is false to minimize potential information exposure.
    /// </summary>
    public bool IncludeSpanEvents { get; set; } = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomerTraceExporterOptions"/> class.
    /// </summary>
    /// <param name="clusterUri">URI of the Azure Data Explorer cluster.</param>
    /// <param name="databaseName">Name of the database to export data to.</param>
    /// <param name="tableName">Name of the table to export data to.</param>
    /// <param name="populateColumns">Optional function to populate custom columns.</param>
    /// <param name="firstPartyAppCertificatePath">Optional path to the First Party App certificate.</param>
    /// <param name="firstPartyAppClientId">Optional First Party App client ID.</param>
    /// <param name="firstPartyAppTenantId">Optional First Party App tenant ID.</param>
    public CustomerTraceExporterOptions(
        string clusterUri,
        string databaseName,
        string tableName,
        PopulateColumnsDelegate? populateColumns = null,
        string? firstPartyAppCertificatePath = "",
        string? firstPartyAppClientId = "",
        string? firstPartyAppTenantId = "")
        : base(clusterUri, databaseName, tableName, populateColumns, firstPartyAppCertificatePath, firstPartyAppClientId, firstPartyAppTenantId)
    {
    }
}

/// <summary>
/// Exporter for sending customer-safe OpenTelemetry trace data to Azure Data Explorer.
/// This exporter filters out sensitive information such as tokens, handoff details, and internal actions
/// before exporting traces that are safe for customer consumption.
/// </summary>
public class CustomerTraceExporter : BaseExporter<Activity>
{
    private readonly AzureDataExplorerExporter _baseExporter;
    private readonly CustomerTraceExporterOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomerTraceExporter"/> class.
    /// </summary>
    /// <param name="options">The configuration options for the customer trace exporter.</param>
    public CustomerTraceExporter(CustomerTraceExporterOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        // Create the underlying AzureDataExplorerExporter with a custom column populator
        // that ensures customer-safe data processing
        var baseOptions = new AzureDataExplorerExporterOptions(
            options.ClusterUri,
            options.DatabaseName,
            options.TableName,
            CreateCustomerSafeColumnPopulator(options),
            options.FirstPartyAppCertificatePath,
            options.FirstPartyAppClientId,
            options.FirstPartyAppTenantId);

        _baseExporter = new AzureDataExplorerExporter(baseOptions);

        Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Initialized Customer Trace Exporter for table: {options.TableName}");
    }

    public override ExportResult Export(in Batch<Activity> batch)
    {
        try
        {
            var filteredActivities = new List<Activity>();

            foreach (var activity in batch)
            {
                // Only include activities that are safe for customer exposure
                if (CustomerTraceFilteringUtilities.ShouldIncludeActivityInCustomerTraces(activity))
                {
                    filteredActivities.Add(activity);
                }
            }

            if (filteredActivities.Count == 0)
            {
                return ExportResult.Success; // No customer-safe activities to export
            }

            // Export the filtered activities using the base exporter
            var filteredBatch = new Batch<Activity>(filteredActivities.ToArray(), filteredActivities.Count);
            var result = _baseExporter.Export(filteredBatch);

            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Exported {filteredActivities.Count}/{batch.Count} customer-safe activities");

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ERROR: Failed to export customer traces - {ex.Message}");
            return ExportResult.Failure;
        }
    }

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        return _baseExporter.Shutdown(timeoutMilliseconds);
    }

    /// <summary>
    /// Creates a customer-safe column populator function that sanitizes trace data.
    /// </summary>
    /// <param name="options">The customer trace exporter options.</param>
    /// <returns>A PopulateColumnsDelegate that ensures customer-safe data.</returns>
    private static PopulateColumnsDelegate CreateCustomerSafeColumnPopulator(CustomerTraceExporterOptions options)
    {
        return (Activity activity, Dictionary<string, object> trace) =>
        {
            // Replace the default trace data with customer-safe version
            var customerSafeData = CustomerTraceFilteringUtilities.CreateCustomerSafeActivityData(activity);

            // Clear existing trace data and populate with safe data
            trace.Clear();
            foreach (var kvp in customerSafeData)
            {
                trace[kvp.Key] = kvp.Value;
            }

            // Add customer-specific metadata
            trace["ExportedForCustomer"] = true;
            trace["ExportTimestamp"] = DateTimeOffset.UtcNow.ToString("O");
            trace["SecurityLevel"] = "CustomerSafe";

            // Add optional debugging info if enabled
            if (options.IncludeMinimalDebugInfo)
            {
                trace["OriginalSpanName"] = activity.DisplayName;
                trace["ActivityKind"] = activity.Kind.ToString();
            }

            // Ensure message lengths are within limits
            foreach (var key in trace.Keys.ToList())
            {
                if (trace[key] is string stringValue && stringValue.Length > options.MaxMessageLength)
                {
                    trace[key] = stringValue.Substring(0, options.MaxMessageLength) + "... [truncated for security]";
                }
            }
        };
    }
}


