using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Agent.Web.Services
{
    public class HttpClientService
    {
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _environment;
        private readonly string _baseUrl;

        public HttpClientService(IConfiguration configuration, IHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
            _baseUrl = DetermineBaseUrl();
        }

        public HttpClient CreateHttpClient()
        {
            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(_baseUrl);
            return httpClient;
        }

        private string DetermineBaseUrl()
        {
            // Try to get from configuration
            var configBaseUrl = _configuration["BaseUrl"];
            if (!string.IsNullOrEmpty(configBaseUrl))
            {
                return configBaseUrl;
            }

            // Check environment variables (for production/kubernetes)
            var aspNetCoreUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
            if (!string.IsNullOrEmpty(aspNetCoreUrls))
            {
                var urls = aspNetCoreUrls.Split(';');
                if (urls.Length > 0)
                {
                    return urls[0].Replace("+", "localhost");
                }
            }

            // Fallback to development defaults
            return _environment.IsDevelopment()
                ? "http://localhost:5073"
                : "http://localhost:8080";
        }
    }
}
