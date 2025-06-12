using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Mocks;
public class MockGrafanaPlugin : IGrafanaPlugin
{
    public Task<byte[]> CaptureScreenshot(string dashboardUid, int width = 1920, int height = 1080)
    {
        throw new NotImplementedException();
    }

    public Task<string> LinkDataSourceToDashboard(string dashboardUid, string dataSourceUid)
    {
        throw new NotImplementedException();
    }

    public Task<string> ModifyGrafanaDashboard(string description, string dashboardName, string existingDashboardUid = null)
    {
        throw new NotImplementedException();
    }

    public Task<string> PublishDashboard(string dashboardJson, bool overwrite = true)
    {
        throw new NotImplementedException();
    }

    public Task<string> PublishDashboardWithPrometheusDataSource(string dashboardJson, string dataSourceName, bool isDefault = false)
    {
        throw new NotImplementedException();
    }

    public Task<string> SetupPrometheusDataSource(string dataSourceName, bool isDefault = false)
    {
        throw new NotImplementedException();
    }
}
