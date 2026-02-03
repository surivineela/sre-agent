// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Validation;
using Agent.Data.DataModels;

namespace Agent.Web.Validation;

/// <summary>
/// Interface for validating code repository configurations.
/// </summary>
public interface ICodeRepoValidator
{
    /// <summary>
    /// Validates a code repository document model.
    /// </summary>
    /// <param name="model">The repository model to validate (URL will be normalized in-place).</param>
    /// <returns>Validation result containing errors and warnings.</returns>
    Task<ApiValidationResult> ValidateCodeRepoAsync(CodeRepoDocumentModel model);
}
