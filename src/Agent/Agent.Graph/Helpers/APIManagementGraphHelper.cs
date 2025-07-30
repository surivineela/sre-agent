using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Graph.Helpers;
public class APIManagementGraphHelper
{
    public static class Constants
    {
        public const string ManagementAzureBaseUrl = "https://management.azure.com";
        public const string ApicApiVersion = "2023-07-01-preview";
        public const string ApicDefaultWorkspaceSegment = "/workspaces/default";

        public const string ArmOperation = "ArmOperation";
    }
}
