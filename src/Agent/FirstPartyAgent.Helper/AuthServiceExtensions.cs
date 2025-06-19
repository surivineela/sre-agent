using System;
using System.Collections.Generic;
using System.Linq;
using FirstPartyAgent.Helper.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.S2S.Configuration;
using Microsoft.IdentityModel.S2S.Extensions.AspNetCore;
using Microsoft.IdentityModel.S2S.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Tokens.Saml2;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class AuthServiceCollectionExtensions
    {
        public static void AddBearerAuthFlow(this IServiceCollection services, IConfiguration configuration, bool isDevelopment)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var securitySettings = configuration.GetSection("SecuritySettings").Get<SecuritySettings>();

            ValidateSecuritySettings(securitySettings);

            if (isDevelopment)
            {
                //S2SEventSource.ShowPII = true;
            }

            services.AddAuthentication(S2SAuthenticationDefaults.AuthenticationScheme)
            .AddMiseWithDefaultAuthentication(configuration, options =>
            {
                AadInboundPolicyOptions policyOptions = new AadInboundPolicyOptions()
                {
                    Label = "Default",
                    Authority = securitySettings.Authority,
                    TenantId = securitySettings.TenantId,
                };
                policyOptions.AuthenticationSchemes.Add("Bearer");
                policyOptions.ValidAudiences = new List<string>() { securitySettings.ClientId };

                policyOptions.ValidApplicationIds = securitySettings.AllowedAppIds;
                options.InboundPolicies.Add(policyOptions);
            });
        }

        private static void ValidateSecuritySettings(SecuritySettings securitySettings)
        {
            if(securitySettings == null)
            {
                throw new ArgumentNullException(nameof(securitySettings), "SecuritySettings cannot be null.");
            }
            if (string.IsNullOrWhiteSpace(securitySettings.ClientId))
            {
                throw new ArgumentException("ClientId must be set in SecuritySettings.", nameof(securitySettings.ClientId));
            }
            if (string.IsNullOrWhiteSpace(securitySettings.Authority))
            {
                throw new ArgumentException("Authority must be set in SecuritySettings.", nameof(securitySettings.Authority));
            }
            if (string.IsNullOrWhiteSpace(securitySettings.TenantId))
            {
                throw new ArgumentException("TenantId must be set in SecuritySettings.", nameof(securitySettings.TenantId));
            }
            if (securitySettings.AllowedAppIds == null || !securitySettings.AllowedAppIds.Any())
            {
                throw new ArgumentException("AllowedAppId must contain at least one application ID.", nameof(securitySettings.AllowedAppIds));
            }

        }
    }
}
