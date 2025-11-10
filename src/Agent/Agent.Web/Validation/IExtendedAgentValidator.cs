// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Validation;
using Agent.Data.DataModels;
using Agent.Web.ApiResources;

namespace Agent.Web.Validation;

/// <summary>
/// Interface for validating extended agent API resources before persisting to storage.
/// Used primarily during dry-run operations to validate resource structure and constraints.
/// </summary>
public interface IExtendedAgentValidator
{
    /// <summary>
    /// Validates an AgentDocumentModel.
    /// </summary>
    /// <param name="model">The agent model to validate.</param>
    /// <returns>ApiCommandResult indicating success or validation errors.</returns>
    Task<AgentValidationResult> ValidateAgentAsync(AgentDocumentModel model);

    /// <summary>
    /// Validates a ToolDocumentModel.
    /// </summary>
    /// <param name="model">The tool model to validate.</param>
    /// <returns>ApiCommandResult indicating success or validation errors.</returns>
    Task<AgentValidationResult> ValidateToolAsync(ToolDocumentModel model);

    /// <summary>
    /// Validates a ConnectorDocumentModel.
    /// </summary>
    /// <param name="model">The connector model to validate.</param>
    /// <returns>ApiCommandResult indicating success or validation errors.</returns>
    Task<AgentValidationResult> ValidateConnectorAsync(ConnectorDocumentModel model);

    /// <summary>
    /// Validates a PlugInConfigDocumentModel.
    /// </summary>
    /// <param name="model">The plugin config model to validate.</param>
    /// <returns>ApiCommandResult indicating success or validation errors.</returns>
    Task<AgentValidationResult> ValidatePluginConfigAsync(PlugInConfigDocumentModel model);

    /// <summary>
    /// Validates a CommonPromptDocumentModel.
    /// </summary>
    /// <param name="model">The common prompt model to validate.</param>
    /// <returns>ApiCommandResult indicating success or validation errors.</returns>
    Task<AgentValidationResult> ValidateCommonPromptAsync(CommonPromptDocumentModel model);

    /// <summary>
    /// Validates a CommonToolsListDocumentModel.
    /// </summary>
    /// <param name="model">The common tools list model to validate.</param>
    /// <returns>ApiCommandResult indicating success or validation errors.</returns>
    Task<AgentValidationResult> ValidateCommonToolsListAsync(CommonToolsListDocumentModel model);
}
