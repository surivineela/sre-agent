using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Agent.Core.Services.TokenService;
using Agent.Core.Helpers;
using Agent.Core.Models.ICM;
using Agent.Core.Configuration;
using Microsoft.AspNetCore.WebUtilities;
using Agent.Logging;

namespace Agent.Core.Services
{
    public interface IICMAPIClient
    {
        //bool IsEnabled();
        Task<Incident> GetIncidentAsync(string incidentId);
        Task<List<Incident>> GetIncidentsAsync(uint limit, uint offset, DateTime? lastModifiedDate, string? owningServiceId, string? titleContains);
        Task<List<CustomField>> GetCustomFieldsAsync(string incidentId);
        Task<List<SearchItem>> SearchIncidentsAsync(string searchString);
        Task<List<DiscussionEntry>> GetIncidentDiscussionEntriesAsync(string incidentId);
        Task<string> TransferIncidentAsync(string incidentId, string discussionEntry, string tenantId, string teamId);
        Task<string> ChangeSeverityAsync(string incidentId, int severity, string discussionEntry, bool htmlRendering = true);
        Task<string> MitigateIncidentAsync(string incidentId, string discussionEntry, bool isCustomerImpacting = false, bool isNoise = false, string howFixed = "", string mitigateContactAlias = "antagent-1p");
        Task<string> ResolveIncidentAsync(string incidentId, string discussionEntry, bool isCustomerImpacting = false, bool isNoise = false, string resolveContactAlias = "antagent-1p");
        Task<string> PostDiscussionEntryAsync(string incidentId, string discussionEntry, bool htmlRendering = true);
        Task<string> SetIncidentTags(string incidentId, List<string> tags);
        Task<string> AddTagToIncident(string incidentId, string tag);
        Task<string> AddKeywordToIncident(string incidentId, string keyword);
        Task<string> AcknowledgeIncidentAsync(string incidentId, string acknowledgeContactAlias = "antagent-1p");
        Task<List<string>> GetLinkedRelatedIncidentInfoAsync(long incidentId);
        Task<string> AddRelatedIncidentLinkAsync(long incidentId, long relatedIncidentId);
        Task<string> RemoveRelatedIncidentLinkAsync(long incidentId, long relatedIncidentId);
        Task<string> GetParentIncidentInfoAsync(long incidentId);
        Task<string> AddParentIncidentLinkAsync(long incidentId, long parentIncidentId);
        Task<string> RemoveParentIncidentLinkAsync(long incidentId);
        Task<List<string>> GetChildIncidentsInfoAsync(long incidentId);
        Task<List<IncidentRepairItem>> GetIncidentRepairItemsAsync(long incidentId);
    }

    public class ICMAPIClient : IICMAPIClient
    {
        private readonly bool IsDevelopment;
        private readonly ICMAPISettings _icmApiSettings;
        private static HttpClient _httpClient;
        private readonly int TimeoutInSeconds = 60;
        private readonly string IcmAPIPathPrefix;
        private readonly AuthType _authType;
        private readonly ILogger<ICMAPIClient> _logger;
        private readonly string _identity = string.Empty;
        private readonly LoggingHttpMessageHandler _loggingHandler;

        public ICMAPIClient(IHostEnvironment environment, IncidentManagementSettings incidentManagementSettings, ILogger<ICMAPIClient> logger, ActionSettings actionSettings, LoggingHttpMessageHandler loggingHandler)
        {
            _logger = logger;
            _loggingHandler = loggingHandler;
            _icmApiSettings = incidentManagementSettings.ICMAPI;
            IsDevelopment = environment.IsDevelopment();
            _identity = actionSettings.Identity ?? string.Empty;
            //if (!_icmApiSettings.Enabled)
            //{
            //    return;
            //}
            if (string.IsNullOrWhiteSpace(_icmApiSettings.APIEndpoint))
            {
                throw new Exception("The environment variable 'ICMAPI:APIEndpoint' is not set.");
            }

            _authType = AuthType.None;
            if (!string.IsNullOrEmpty(_identity))
            {
                _authType = AuthType.ManagedIdentity;
                IcmAPIPathPrefix = "/api2/user/incidentapi";
            }
            else if (!string.IsNullOrWhiteSpace(_icmApiSettings.CertificateSubjectName))
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

        //public bool IsEnabled()
        //{
        //    return _icmApiSettings.Enabled;
        //}

        private void InitializeHttpClient()
        {
            if (_authType == AuthType.UserToken)
            {
                _loggingHandler.InnerHandler = new HttpClientHandler();
                _httpClient = new HttpClient(_loggingHandler)
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

                _loggingHandler.InnerHandler = handler;
                _httpClient = new HttpClient(_loggingHandler)
                {
                    Timeout = TimeSpan.FromSeconds(TimeoutInSeconds)
                };
            }
            else if (_authType == AuthType.ManagedIdentity)
            {
                _loggingHandler.InnerHandler = new HttpClientHandler();
                _httpClient = new HttpClient(_loggingHandler)
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
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);
            if (_authType == AuthType.ManagedIdentity)
            {
                string authToken = await ICMAPITokenService.Instance.GetAuthorizationTokenAsync();
                requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);
            }
            var response = await _httpClient.SendAsync(requestMessage);
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
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri);
            requestMessage.Content = new StringContent(JsonConvert.SerializeObject(content), Encoding.UTF8, "application/json");
            if (_authType == AuthType.ManagedIdentity)
            {
                string authToken = await ICMAPITokenService.Instance.GetAuthorizationTokenAsync();
                requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);
            }
            var response = await _httpClient.SendAsync(requestMessage);
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
                var requestMessage = new HttpRequestMessage(HttpMethod.Patch, requestUri);
                requestMessage.Content = new StringContent(JsonConvert.SerializeObject(content), Encoding.UTF8, "application/json");
                if (_authType == AuthType.ManagedIdentity)
                {
                    string authToken = await ICMAPITokenService.Instance.GetAuthorizationTokenAsync();
                    requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);
                }
                var response = await _httpClient.SendAsync(requestMessage);
                return response;
        }

        private async Task<HttpResponseMessage> SendICMDeleteRequestAsync(string apiPath)
        {
            if (string.IsNullOrWhiteSpace(apiPath))
            {
                throw new ArgumentException("apiPath must be provided.", nameof(apiPath));
            }
            var requestUri = $"{_icmApiSettings.APIEndpoint}{apiPath}";
            var requestMessage = new HttpRequestMessage(HttpMethod.Delete, requestUri);
            if (_authType == AuthType.ManagedIdentity)
            {
                string authToken = await ICMAPITokenService.Instance.GetAuthorizationTokenAsync();
                requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);
            }
            var response = await _httpClient.SendAsync(requestMessage);
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

        public async Task<List<Incident>> GetIncidentsAsync(uint limit, uint offset, DateTime? lastModifiedDate, string? owningServiceId, string? titleContains)
        {
            var modifiedDate = lastModifiedDate.HasValue ? lastModifiedDate.Value : DateTime.UtcNow.AddDays(-30); // Default to 30 days ago if no date is provided

            if (lastModifiedDate < DateTime.UtcNow.AddDays(-90))
            {
                throw new ArgumentException("lastModifiedDate cannot be more than 90 days ago.", nameof(lastModifiedDate));
            }

            owningServiceId = owningServiceId ?? _icmApiSettings.OwningServiceId;
            var serviceIdFilter = !string.IsNullOrWhiteSpace(owningServiceId) ? $" and OwningServiceId eq {owningServiceId}" : string.Empty;
            var titleFilter = !string.IsNullOrWhiteSpace(titleContains) ? $" and contains(Title, '{titleContains}')" : string.Empty;
            var queryParams = new Dictionary<string, string?>()
            {
                ["$top"] = limit.ToString(),
                ["$skip"] = offset.ToString(),
                ["$orderby"] = "LastModifiedDate desc",
                ["$filter"] = $"LastModifiedDate gt {modifiedDate.ToString("yyyy-MM-ddTHH:mm:ss'Z'")}{serviceIdFilter}{titleFilter}"
            };

            return await GetIncidentsAsyncInternal(queryParams);
        }

        private async Task<List<Incident>> GetIncidentsAsyncInternal(Dictionary<string, string?> queryParams)
        {
            var apiPath = QueryHelpers.AddQueryString($"{IcmAPIPathPrefix}/incidents", queryParams);
            var response = await SendICMGetRequestAsync(apiPath);
            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                var oDataResponse = JsonConvert.DeserializeObject<ODataResponse<JObject>>(responseString);

                var incidents = oDataResponse.Value.Select(o =>
                {
                    var incidentJObj = TextProcessingHelpers.FillICMAPIIncidentJObject(o);
                    return JsonConvert.DeserializeObject<Incident>(JsonConvert.SerializeObject(incidentJObj));
                }).ToList();
                return incidents;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to retrieve incident. Status code: {response.StatusCode}. Error content: {errorContent}");
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
            if (_icmApiSettings.ReadOnly)
            {
                return ("Success. ICM API is in read-only mode.");
            }
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
            if (_icmApiSettings.ReadOnly)
            {
                return ("Success. ICM API is in read-only mode.");
            }
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

        public async Task<string> MitigateIncidentAsync(string incidentId, string discussionEntry, bool isCustomerImpacting = false, bool isNoise = false, string howFixed = "", string mitigateContactAlias = "antagent-1p")
        {
            if (_icmApiSettings.ReadOnly)
            {
                return ("Success. ICM API is in read-only mode.");
            }
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

        public async Task<List<SearchItem>> SearchIncidentsAsync(string searchString)
        {
            var content = new
            {
                SearchString = searchString,
                IncludeCorrelated = false,
                OrderColumn = "CreateDate",
                OrderDir = "desc",
                Skip = 0,
                Top = 100,
            };
            var response = await SendICMPostRequestAsync($"/api2/user/omnisearch/SearchIncidents", content);
            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                var obj = JsonConvert.DeserializeObject<JObject>(responseString);

                if (obj == null || !obj.TryGetValue("value", out var incidentsToken))
                {
                    return new List<SearchItem>();
                }

                var incidents = incidentsToken.ToObject<List<SearchItem>>();
                return incidents ?? new List<SearchItem>();
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new List<SearchItem>(); // Return empty list if no incidents found
            }
            else
            {
                throw new Exception($"Failed to search for incidents {response.StatusCode}");
            }
        }

        public async Task<string> ResolveIncidentAsync(string incidentId, string discussionEntry, bool isCustomerImpacting = false, bool isNoise = false, string resolveContactAlias = "antagent-1p")
        {
            if (_icmApiSettings.ReadOnly)
            {
                return ("Success. ICM API is in read-only mode.");
            }
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

        public async Task<string> PostDiscussionEntryAsync(string incidentId, string discussionEntry, bool htmlRendering = true)
        {
            if (_icmApiSettings.ReadOnly)
            {
                return ("Success. ICM API is in read-only mode.");
            }
            var content = new
            {
                Description = discussionEntry
            };
            var response = await SendICMPatchRequestAsync($"{IcmAPIPathPrefix}/incidents({incidentId})", content);
            if (response.IsSuccessStatusCode)
            {
                return "Discussion entry posted successfully.";
            }
            else
            {
                _logger.LogInternalError($"Failed to post discussion entry. Status code: {response.StatusCode} : {await response.Content.ReadAsStringAsync()}");
                return "Hi";
                //throw new Exception($"Failed to post discussion entry. Status code: {response.StatusCode}");
            }
        }

        public async Task<string> SetIncidentTags(string incidentId, List<string> tags)
        {
            if (_icmApiSettings.ReadOnly)
            {
                return ("Success. ICM API is in read-only mode.");
            }
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
            if (_icmApiSettings.ReadOnly)
            {
                return ("Success. ICM API is in read-only mode.");
            }
            var incident = await GetIncidentAsync(incidentId);
            return await SetIncidentTags(incidentId, incident.Tags.Append(tag).ToList());
        }

        public async Task<string> AddKeywordToIncident(string incidentId, string keyword)
        {
            if (_icmApiSettings.ReadOnly)
            {
                return ("Success. ICM API is in read-only mode.");
            }

            var incident = await GetIncidentAsync(incidentId);
            var updatedKeywords = string.IsNullOrWhiteSpace(incident.Keywords) ? keyword : $"{incident.Keywords}, {keyword}";
            var content = new
            {
                Keywords = updatedKeywords,
            };
            var response = await SendICMPatchRequestAsync($"{IcmAPIPathPrefix}/incidents({incidentId})", content);
            if (response.IsSuccessStatusCode)
            {
                return "Keyword added successfully.";
            }
            else
            {
                throw new Exception($"Failed to add keyword to incident. Status code: {response.StatusCode}");
            }
        }

        public async Task<string> AcknowledgeIncidentAsync(string incidentId, string acknowledgeContactAlias = "antagent-1p")
        {
            if (_icmApiSettings.ReadOnly)
            {
                return ("Success. ICM API is in read-only mode.");
            }
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

        #region RelatedIncident CRD
        public async Task<List<string>> GetLinkedRelatedIncidentInfoAsync(long incidentId)
        {
            var response = await SendICMGetRequestAsync($"{IcmAPIPathPrefix}/incidents({incidentId})/RelatedIncidents");
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var obj = JsonConvert.DeserializeObject<JObject>(responseContent);
                if (obj == null || !obj.TryGetValue("value", out var incidentsToken))
                {
                    return new List<string> { "No related incidents found." };
                }
                else
                {
                    // Loop through each element in the value array and add it to a List of string
                    List<string> relatedIncidents = new List<string>();
                    if (incidentsToken?.Count() > 0)
                    {
                        foreach (var incident in incidentsToken)
                        {
                            var incidentIdValue = incident["Id"]?.ToString();
                            if (!string.IsNullOrEmpty(incidentIdValue))
                            {
                                var incidentString = JsonConvert.SerializeObject(incident);
                                relatedIncidents.Add(incidentString);
                            }
                        }
                    }
                    else
                    {
                        relatedIncidents.Add("No related incidents found.");
                    }
                    return relatedIncidents;
                }
            }
            else
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return new List<string> { $"No related incidents found.: {await response.Content.ReadAsStringAsync()}" };
                }
                else
                {
                    throw new Exception($"Failed to retrieve related incidents. Status code: {response.StatusCode} : {await response.Content.ReadAsStringAsync()}");
                }
            }
        }

        public async Task<string> AddRelatedIncidentLinkAsync(long incidentId, long relatedIncidentId)
        {
            if (_icmApiSettings.ReadOnly)
            {
                return "Success. ICM API is in read-only mode.";
            }
            var content = new
            {
                url = $"{_icmApiSettings.APIEndpoint}{IcmAPIPathPrefix}/incidents({relatedIncidentId}L)" // Note the "L" character in request body. This suffix is mandatory and is not a typo.
            };
            var response = await SendICMPostRequestAsync($"{IcmAPIPathPrefix}/incidents({incidentId})/\\$links/RelatedIncidents", content);
            if (response.IsSuccessStatusCode)
            {
                return "Related incident linked successfully.";
            }
            else
            {
                throw new Exception($"Failed to link related incident. Status code: {response.StatusCode} : {await response.Content.ReadAsStringAsync()}");
            }
        }

        public async Task<string> RemoveRelatedIncidentLinkAsync(long incidentId, long relatedIncidentId)
        {
            if (_icmApiSettings.ReadOnly)
            {
                return "Success. ICM API is in read-only mode.";
            }
            var response = await SendICMDeleteRequestAsync($"{IcmAPIPathPrefix}/incidents({incidentId})/\\$links/RelatedIncidents({relatedIncidentId})");
            if (response.IsSuccessStatusCode)
            {
                return "Related incident removed successfully.";
            }
            else
            {
                throw new Exception($"Failed to remove related incident. Status code: {response.StatusCode} : {await response.Content.ReadAsStringAsync()}");
            }
        }
        #endregion

        #region ParentIncident CRD
        public async Task<string> GetParentIncidentInfoAsync(long incidentId)
        {
            var response = await SendICMGetRequestAsync($"{IcmAPIPathPrefix}/incidents({incidentId})/ParentIncident");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
            else
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return $"No parent incident found.: {await response.Content.ReadAsStringAsync()}";
                }
                else
                {
                    throw new Exception($"Failed to retrieve parent incident. Status code: {response.StatusCode} : {await response.Content.ReadAsStringAsync()}");
                }
            }
        }

        public async Task<string> AddParentIncidentLinkAsync(long incidentId, long parentIncidentId)
        {
            if (_icmApiSettings.ReadOnly)
            {
                return "Success. ICM API is in read-only mode.";
            }

            var content = new
            {
                url = $"{_icmApiSettings.APIEndpoint}{IcmAPIPathPrefix}/incidents({parentIncidentId}L)" // Note the "L" character in request body. This suffix is mandatory and is not a typo.
            };

            var response = await SendICMPostRequestAsync($"{IcmAPIPathPrefix}/incidents({incidentId})/\\$links/ParentIncident", content);
            if (response.IsSuccessStatusCode)
            {
                return "Parent incident linked successfully.";
            }
            else
            {
                throw new Exception($"Failed to link parent to incident. Status code: {response.StatusCode} : {await response.Content.ReadAsStringAsync()}");
            }
        }

        public async Task<string> RemoveParentIncidentLinkAsync(long incidentId)
        {
            if (_icmApiSettings.ReadOnly)
            {
                return "Success. ICM API is in read-only mode.";
            }

            var response = await SendICMDeleteRequestAsync($"{IcmAPIPathPrefix}/incidents({incidentId})/\\$links/ParentIncident");
            if (response.IsSuccessStatusCode)
            {
                return "Parent incident removed successfully.";
            }
            else
            {
                throw new Exception($"Failed to remove parent from incident. Status code: {response.StatusCode} : ${await response.Content.ReadAsStringAsync()}");
            }
        }
        #endregion

        public async Task<List<string>> GetChildIncidentsInfoAsync(long incidentId)
        {
            var response = await SendICMGetRequestAsync($"{IcmAPIPathPrefix}/incidents({incidentId})/ChildIncidents");
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var obj = JsonConvert.DeserializeObject<JObject>(responseContent);
                if (obj == null || !obj.TryGetValue("value", out var incidentsToken))
                {
                    return new List<string> { "No child incidents found." };
                }
                else
                {
                    // Loop through each element in the value array and add it to a List of string
                    List<string> childIncidents = new List<string>();
                    if (incidentsToken?.Count() > 0)
                    {
                        foreach (var incident in incidentsToken)
                        {
                            var incidentIdValue = incident["Id"]?.ToString();
                            if (!string.IsNullOrEmpty(incidentIdValue))
                            {
                                var incidentString = JsonConvert.SerializeObject(incident);
                                childIncidents.Add(incidentString);
                            }
                        }
                    }
                    else
                    {
                        childIncidents.Add("No child incidents found.");
                    }
                    return childIncidents;
                }
            }
            else
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return new List<string> { $"No child incidents found.: {await response.Content.ReadAsStringAsync()}" };
                }
                else
                {
                    throw new Exception($"Failed to retrieve child incidents. Status code: {response.StatusCode} : {await response.Content.ReadAsStringAsync()}");
                }
            }
        }

        public async Task<List<IncidentRepairItem>> GetIncidentRepairItemsAsync(long incidentId)
        {
            var content = new
            {
                Id = $"{incidentId}",
                IdType = "icm.incident"
            };

            string apiBasePath = _authType == AuthType.Certificate ? $"{IcmAPIPathPrefix.Replace("/api/", "/api2/")}/incidentapi" : IcmAPIPathPrefix;

            var response = await SendICMPostRequestAsync($"{apiBasePath}/incidents/externallink/repairitems/get", content);

            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                var repairItems = JsonConvert.DeserializeObject<List<IncidentRepairItem>>(responseString);
                return repairItems;
            }
            else
            {
                throw new Exception($"Failed to retrieve incident repair items. Status code: {response.StatusCode} : {await response.Content.ReadAsStringAsync()}");
            }
        }
    }

    public class NullableICMAPIClient : IICMAPIClient
    {
        public Task<string> AcknowledgeIncidentAsync(string incidentId, string acknowledgeContactAlias = "antagent-1p")
        {
            throw new NotImplementedException();
        }

        public Task<string> AddKeywordToIncident(string incidentId, string keyword)
        {
            throw new NotImplementedException();
        }

        public Task<string> AddParentIncidentLinkAsync(long incidentId, long parentIncidentId)
        {
            throw new NotImplementedException();
        }

        public Task<string> AddRelatedIncidentLinkAsync(long incidentId, long relatedIncidentId)
        {
            throw new NotImplementedException();
        }

        public Task<string> AddTagToIncident(string incidentId, string tag)
        {
            throw new NotImplementedException();
        }

        public Task<string> ChangeSeverityAsync(string incidentId, int severity, string discussionEntry, bool htmlRendering = true)
        {
            throw new NotImplementedException();
        }

        public Task<List<string>> GetChildIncidentsInfoAsync(long incidentId)
        {
            throw new NotImplementedException();
        }

        public Task<List<CustomField>> GetCustomFieldsAsync(string incidentId)
        {
            throw new NotImplementedException();
        }

        public Task<Incident> GetIncidentAsync(string incidentId)
        {
            throw new NotImplementedException();
        }

        public Task<List<DiscussionEntry>> GetIncidentDiscussionEntriesAsync(string incidentId)
        {
            throw new NotImplementedException();
        }

        public Task<List<IncidentRepairItem>> GetIncidentRepairItemsAsync(long incidentId)
        {
            throw new NotImplementedException();
        }

        public Task<List<Incident>> GetIncidentsAsync(uint limit, uint offset, DateTime? lastModifiedDate, string? owningServiceId, string? titleContains)
        {
            throw new NotImplementedException();
        }

        public Task<List<string>> GetLinkedRelatedIncidentInfoAsync(long incidentId)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetParentIncidentInfoAsync(long incidentId)
        {
            throw new NotImplementedException();
        }

        public bool IsEnabled()
        {
            throw new NotImplementedException();
        }

        public Task<string> MitigateIncidentAsync(string incidentId, string discussionEntry, bool isCustomerImpacting = false, bool isNoise = false, string howFixed = "", string mitigateContactAlias = "antagent-1p")
        {
            throw new NotImplementedException();
        }

        public Task<string> PostDiscussionEntryAsync(string incidentId, string discussionEntry, bool htmlRendering = true)
        {
            throw new NotImplementedException();
        }

        public Task<string> RemoveParentIncidentLinkAsync(long incidentId)
        {
            throw new NotImplementedException();
        }

        public Task<string> RemoveRelatedIncidentLinkAsync(long incidentId, long relatedIncidentId)
        {
            throw new NotImplementedException();
        }

        public Task<string> ResolveIncidentAsync(string incidentId, string discussionEntry, bool isCustomerImpacting = false, bool isNoise = false, string resolveContactAlias = "antagent-1p")
        {
            throw new NotImplementedException();
        }

        public Task<List<SearchItem>> SearchIncidentsAsync(string searchString)
        {
            throw new NotImplementedException();
        }

        public Task<string> SetIncidentTags(string incidentId, List<string> tags)
        {
            throw new NotImplementedException();
        }

        public Task<string> TransferIncidentAsync(string incidentId, string discussionEntry, string tenantId, string teamId)
        {
            throw new NotImplementedException();
        }
    }
}


