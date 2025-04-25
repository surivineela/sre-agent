// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Helpers;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace FirstPartyAgent.Core.Services
{
    public interface IICMAPIClient
    {
        bool IsEnabled();
        Task<Incident> GetIncidentAsync(string incidentId);
        Task<List<CustomField>> GetCustomFieldsAsync(string incidentId);
        Task<List<DiscussionEntry>> GetIncidentDiscussionEntriesAsync(string incidentId);
        Task<string> TransferIncidentAsync(string incidentId, string discussionEntry, string tenantId, string teamId);
        Task<string> ChangeSeverityAsync(string incidentId, int severity, string discussionEntry, bool htmlRendering = true);
        Task<string> MitigateIncidentAsync(string incidentId, string discussionEntry, bool isCustomerImpacting = false, bool isNoise = false, string howFixed = "", string mitigateContactAlias = "antagent-1p");
        Task<string> ResolveIncidentAsync(string incidentId, string discussionEntry, bool isCustomerImpacting = false, bool isNoise = false, string resolveContactAlias = "antagent-1p");
        Task<string> PostDiscussionEntryAsync(string incidentId, string discussionEntry, bool htmlRendering = true);
        Task<string> SetIncidentTags(string incidentId, List<string> tags);
        Task<string> AddTagToIncident(string incidentId, string tag);
        Task<string> AcknowledgeIncidentAsync(string incidentId, string acknowledgeContactAlias = "antagent-1p");
    }

    public class ICMAPIClient : IICMAPIClient
    {
        private readonly bool IsDevelopment;
        private readonly ICMAPISettings _icmApiSettings;
        private static HttpClient _httpClient;
        private readonly int TimeoutInSeconds = 60;
        private readonly string IcmAPIPathPrefix;
        private readonly AuthType _authType;

        public ICMAPIClient(IHostEnvironment environment, ICMAPISettings icmApiSettings)
        {
            _icmApiSettings = icmApiSettings;
            IsDevelopment = environment.IsDevelopment();
            if (!icmApiSettings.Enabled)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(_icmApiSettings.APIEndpoint))
            {
                throw new Exception("The environment variable 'ICMAPI:APIEndpoint' is not set.");
            }

            _authType = AuthType.None;
            if (!string.IsNullOrWhiteSpace(_icmApiSettings.CertificateSubjectName))
            {
                _authType = AuthType.Certificate;
                IcmAPIPathPrefix = "/api/cert";
            }
            else if (!string.IsNullOrWhiteSpace(_icmApiSettings.UserToken))
            {
                _authType = AuthType.UserToken;
                IcmAPIPathPrefix = "/api2/user/incidentapi";
            }
            else
            {
                throw new Exception("At least one of the environment variables 'ICMAPI:UserToken' or 'ICMAPI:CertificateSubjectName' must be set.");
            }

            InitializeHttpClient();
        }

        public bool IsEnabled()
        {
            return _icmApiSettings.Enabled;
        }

        private void InitializeHttpClient()
        {
            if (_authType == AuthType.UserToken)
            {
                _httpClient = new HttpClient()
                {
                    Timeout = TimeSpan.FromSeconds(TimeoutInSeconds)
                };
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_icmApiSettings.UserToken}");
            }
            else if (_authType == AuthType.Certificate)
            {
                var handler = new HttpClientHandler();

                // Open the "My" certificate store in the current user's context.
                using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
                {
                    store.Open(OpenFlags.ReadOnly);

                    // Locate the certificate by matching the subject name.
                    var certificates = store.Certificates.Find(X509FindType.FindBySubjectName, _icmApiSettings.CertificateSubjectName, validOnly: false);
                    if (certificates == null || certificates.Count == 0)
                    {
                        throw new Exception($"Certificate with subject matching '{_icmApiSettings.CertificateSubjectName}' not found.");
                    }

                    // Use the first matching certificate.
                    handler.ClientCertificates.Add(certificates[0]);
                }

                _httpClient = new HttpClient(handler)
                {
                    Timeout = TimeSpan.FromSeconds(TimeoutInSeconds)
                };
            }
            else
            {
                throw new Exception("Could not initialize http client for ICM APIs as no auth was set.");
            }
        }

        private async Task<HttpResponseMessage> SendICMGetRequestAsync(string apiPath)
        {
            if (string.IsNullOrWhiteSpace(apiPath))
            {
                throw new ArgumentException("apiPath must be provided.", nameof(apiPath));
            }

            var requestUri = $"{_icmApiSettings.APIEndpoint}{apiPath}";
            var response = await _httpClient.GetAsync(requestUri);

            return response;
        }

        private async Task<HttpResponseMessage> SendICMPostRequestAsync(string apiPath, object content)
        {
            if (string.IsNullOrWhiteSpace(apiPath))
            {
                throw new ArgumentException("apiPath must be provided.", nameof(apiPath));
            }
            if (content == null)
            {
                throw new ArgumentException("content must be provided.", nameof(content));
            }
            var requestUri = $"{_icmApiSettings.APIEndpoint}{apiPath}";
            var httpContent = new StringContent(JsonConvert.SerializeObject(content), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(requestUri, httpContent);
            return response;
        }

        private async Task<HttpResponseMessage> SendICMPatchRequestAsync(string apiPath, object content)
        {
            if (string.IsNullOrWhiteSpace(apiPath))
            {
                throw new ArgumentException("apiPath must be provided.", nameof(apiPath));
            }
            if (content == null)
            {
                throw new ArgumentException("content must be provided.", nameof(content));
            }

            var requestUri = $"{_icmApiSettings.APIEndpoint}{apiPath}";
            var httpContent = new StringContent(JsonConvert.SerializeObject(content), Encoding.UTF8, "application/json");

            var response = await _httpClient.PatchAsync(requestUri, httpContent);
            return response;
        }

        public async Task<Incident> GetIncidentAsync(string incidentId)
        {
            var response = await SendICMGetRequestAsync($"{IcmAPIPathPrefix}/incidents({incidentId})");
            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                var obj = JsonConvert.DeserializeObject<JObject>(responseString);
                obj = TextProcessingHelpers.FillICMAPIIncidentJObject(obj);
                var incident = JsonConvert.DeserializeObject<Incident>(JsonConvert.SerializeObject(obj));
                return incident;
            }
            else
            {
                throw new Exception($"Failed to retrieve incident. Status code: {response.StatusCode}");
            }
        }

        public async Task<List<CustomField>> GetCustomFieldsAsync(string incidentId)
        {
            var apiPath = $"{IcmAPIPathPrefix}/incidents({incidentId})/GetIncidentDetails?$expand=CustomFields";
            var response = await SendICMGetRequestAsync(apiPath);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to retrieve custom fields. Status code: {response.StatusCode}");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            var obj = JsonConvert.DeserializeObject<JObject>(responseString);

            if (obj == null || !obj.TryGetValue("CustomFields", out var customFieldsToken))
            {
                return new List<CustomField>();
            }

            var customFields = customFieldsToken.ToObject<List<CustomField>>();
            return customFields ?? new List<CustomField>();
        }

        public async Task<List<DiscussionEntry>> GetIncidentDiscussionEntriesAsync(string incidentId)
        {
            var response = await SendICMGetRequestAsync($"{IcmAPIPathPrefix}/incidents({incidentId})/DescriptionEntries?/$inlinecount=allpages");
            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                var oDataModel = JsonConvert.DeserializeObject<ODataResponse<DiscussionEntry>>(responseString);
                return oDataModel.Value;
            }
            else
            {
                throw new Exception($"Failed to retrieve incident discussion entries. Status code: {response.StatusCode}");
            }
        }

        public async Task<string> TransferIncidentAsync(string incidentId, string discussionEntry, string tenantId, string teamId)
        {
            var content = new
            {
                TransferParameters = new
                {
                    OwningTenantPublicId = tenantId,
                    OwningTeamPublicId = teamId,
                    Description = new
                    {
                        Text = discussionEntry,
                        RenderType = "Plaintext", // ICM APIs yield 403 BadRequest when using "Html" rendering on this API
                    },
                },
            };
            var response = await SendICMPostRequestAsync($"{IcmAPIPathPrefix}/incidents({incidentId})/TransferIncident", content);
            if (response.IsSuccessStatusCode)
            {
                return "Incident transferred successfully.";
            }
            else
            {
                throw new Exception($"Failed to transfer incident. Status code: {response.StatusCode}");
            }
        }

        public async Task<string> ChangeSeverityAsync(string incidentId, int severity, string discussionEntry, bool htmlRendering = true)
        {
            var content = new
            {
                Severity = severity,
                NewDescriptionEntry = new
                {
                    Text = discussionEntry,
                    RenderType = htmlRendering ? "Html" : "Plaintext",
                },
            };
            var response = await SendICMPatchRequestAsync($"{IcmAPIPathPrefix}/incidents({incidentId})", content);
            if (response.IsSuccessStatusCode)
            {
                return "Severity changed successfully.";
            }
            else
            {
                throw new Exception($"Failed to change severity. Status code: {response.StatusCode}");
            }
        }

        public async Task<string> MitigateIncidentAsync(string incidentId, string discussionEntry, bool isCustomerImpacting=false, bool isNoise=false, string howFixed="", string mitigateContactAlias="antagent-1p")
        {
            var content = new
            {
                MitigateParameters = new
                {
                    IsCustomerImpacting = isCustomerImpacting,
                    IsNoise = isNoise,
                    Mitigation = discussionEntry,
                    HowFixed = howFixed,
                    MitigateContactAlias = mitigateContactAlias,
                },
            };
            var response = await SendICMPostRequestAsync($"{IcmAPIPathPrefix}/incidents({incidentId})/MitigateIncident", content);
            if (response.IsSuccessStatusCode)
            {
                return "Incident mitigated successfully.";
            }
            else
            {
                throw new Exception($"Failed to mitigate incident. Status code: {response.StatusCode}");
            }
        }

        public async Task<string> ResolveIncidentAsync(string incidentId, string discussionEntry, bool isCustomerImpacting=false, bool isNoise=false, string resolveContactAlias= "antagent-1p")
        {
            var content = new
            {
                ResolveParameters = new
                {
                    IsCustomerImpacting = isCustomerImpacting,
                    IsNoise = isNoise,
                    Description = new
                    {
                        Text = discussionEntry,
                        RenderType = "Plaintext", // ICM APIs yield 403 BadRequest when using "Html" rendering
                    },
                    ResolveContactAlias = resolveContactAlias,
                },
            };
            var response = await SendICMPostRequestAsync($"{IcmAPIPathPrefix}/incidents({incidentId})/ResolveIncident", content);
            if (response.IsSuccessStatusCode)
            {
                return "Incident resolved successfully.";
            }
            else
            {
                throw new Exception($"Failed to resolve incident. Status code: {response.StatusCode}");
            }
        }

        public async Task<string> PostDiscussionEntryAsync(string incidentId, string discussionEntry, bool htmlRendering=true)
        {
            var content = new
            {
                NewDescriptionEntry = new
                {
                    Text = discussionEntry,
                    RenderType = htmlRendering ? "Html" : "Plaintext",
                },
            };
            var response = await SendICMPatchRequestAsync($"{IcmAPIPathPrefix}/incidents({incidentId})", content);
            if (response.IsSuccessStatusCode)
            {
                return "Discussion entry posted successfully.";
            }
            else
            {
                throw new Exception($"Failed to post discussion entry. Status code: {response.StatusCode}");
            }
        }

        public async Task<string> SetIncidentTags(string incidentId, List<string> tags)
        {
            var content = new
            {
                Tags = tags,
            };
            var response = await SendICMPatchRequestAsync($"{IcmAPIPathPrefix}/incidents({incidentId})", content);
            if (response.IsSuccessStatusCode)
            {
                return "Tags added successfully.";
            }
            else
            {
                throw new Exception($"Failed to add tag to incident. Status code: {response.StatusCode}");
            }
        }

        public async Task<string> AddTagToIncident(string incidentId, string tag)
        {
            var incident = await GetIncidentAsync(incidentId);
            return await SetIncidentTags(incidentId, incident.Tags.Append(tag).ToList());
        }

        public async Task<string> AcknowledgeIncidentAsync(string incidentId, string acknowledgeContactAlias = "antagent-1p")
        {
            var content = new
            {
                AcknowledgementParameters = new
                {
                    AcknowledgeContactAlias = acknowledgeContactAlias,
                },
            };
            var response = await SendICMPostRequestAsync($"{IcmAPIPathPrefix}/incidents({incidentId})/AcknowledgeIncident", content);
            if (response.IsSuccessStatusCode)
            {
                return "Incident acknowledged successfully.";
            }
            else
            {
                throw new Exception($"Failed to acknowledge incident. Status code: {response.StatusCode}");
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}

