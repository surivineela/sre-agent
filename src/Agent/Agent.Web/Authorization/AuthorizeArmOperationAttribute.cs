// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.FeatureManagement;

namespace Agent.Web
{
    /// <summary>
    /// Attribute to declare a single required ARM-like operation for an API action.
    /// Authorization succeeds only if all declared operations (from multiple attributes)
    /// are present in the incoming request header 'x-allowed-actions' (comma-separated list).
    /// Note: Multiple operations in a single attribute (comma-separated) are NOT allowed
    /// and will cause authorization to fail.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class AuthorizeArmOperationAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private const string HeaderName = "x-allowed-actions";
        private const string FeatureFlagName = "PdpAuthZv2";

        /// <summary>
        /// Single required action represented by this attribute instance.
        /// </summary>
        public string Action { get; }

        public AuthorizeArmOperationAttribute(string action)
        {
            Action = action?.Trim() ?? string.Empty;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            if (context?.HttpContext is null)
            {
                context!.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
                return;
            }

            var hostEnvironment = context.HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
            if (hostEnvironment.IsDevelopment())
            {
                return;
            }

            var featureManager = context.HttpContext.RequestServices.GetService(typeof(IFeatureManager)) as IFeatureManager;
            if (featureManager == null)
            {
                return;
            }

            bool enabled;
            try
            {
                enabled = await featureManager.IsEnabledAsync(FeatureFlagName);
            }
            catch
            {
                enabled = true;
            }

            if (!enabled)
            {
                return;
            }

            if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var headerValues))
            {
                context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
                return;
            }

            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var value in headerValues)
            {
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }
                foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        allowed.Add(token);
                    }
                }
            }

            // Ensure all required actions are present
            if (!allowed.Contains(Action))
            {
                context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
                return;
            }

            return;
        }
    }
}
