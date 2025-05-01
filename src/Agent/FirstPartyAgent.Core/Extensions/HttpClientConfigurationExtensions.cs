using System.Net.Http.Headers;
using System.Text;
using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FirstPartyAgent.Core.Extensions;
public static class HttpClientConfigurationExtensions
{
    public static void AddDevOpsHelperHttpClient(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(DevOpsHelperService)).AddHttpMessageHandler(sp =>
        {
            var devOpsSetting = sp.GetRequiredService<DevOpsSetting>();
            IHostEnvironment hostEnvironment = sp.GetRequiredService<IHostEnvironment>();
            return new DevOpsHelperAccessTokenHandler(devOpsSetting, hostEnvironment);
        });
    }

    public class DevOpsHelperAccessTokenHandler : DelegatingHandler
    {
        private readonly DevOpsSetting _devOpsSetting;
        private readonly IHostEnvironment _hostEnvironment;

        public DevOpsHelperAccessTokenHandler(DevOpsSetting devOpsSetting, IHostEnvironment hostEnvironment)
        {
            _devOpsSetting = devOpsSetting;
            _hostEnvironment = hostEnvironment;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(_devOpsSetting?.PatOverride))
            {
                var byteArray = Encoding.ASCII.GetBytes($":{_devOpsSetting?.PatOverride}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
