using Agent.Core.Models;
using Agent.Plugins.Interface;
using System.Text.Json;

namespace Agent.Plugins.Mocks;
public class MockAzureSupportCenterPlugin : IAzureSupportCenterPlugin
{
    public Task<string> GetAzureSupportCenterDiagnosticResultsForQuestion(string resourceId, SupportProductFromArmModel targetSupportProduct, SupportProblemClassificationModel targetSupportProblemClassification, string question)
    {
        var mockApolloDiagnosticsForVmRdpIssue = new List<ApolloDiagnostic>()
        {
            new ApolloDiagnostic()
            {
                Insights = new List<ApolloInsight>()
                {
                    new ApolloInsight()
                    {
                        Id = "VMStopped",
                        Title = "The virtual machine is not running",
                        Results = "<!--issueDescription-->\n<p>We have detected that the virtual machine (VM) RDPWindowsVM is not currently running. To run additional diagnostic tests, the VM must be running.<br></p>\n<!--/issueDescription-->\n<h2><strong>Recommended Steps</strong></h2>\n<ul>\n<li>Please <a data-blade=\"Microsoft_Azure_Compute.VirtualMachineProtoBlade.id.$resourceId\">start</a> the VM RDPWindowsVM, so that we may run additional diagnostic tests</li>\n</ul>\n",
                        ImportanceLevel = "Critical"
                    }
                },
                SolutionId = "CannotRdpAzurePortalInsight",
                Status = "Succeeded",
                StatusDetails = "",
                Steps = new List<ApolloStep>()
                {
                    new ApolloStep()
                    {
                        Id = "2881a751-e3f6-411d-bcdb-16ec55b12850",
                        Message = "We have successfully checked the resource for you.",
                        Type = "Done"
                    }
                },
                ReplacementKey = "<!--76d146a5-358b-4d02-8ad8-e3fbe2f9f8b3-->"
            },
            new ApolloDiagnostic()
            {
                Insights = new List<ApolloInsight>(),
                SolutionId = "TripleFaultAzurePortalInsight",
                Status = "Succeeded",
                StatusDetails = "The diagnostic did not detect any issues",
                Steps = new List<ApolloStep>()
                {
                    new ApolloStep()
                    {
                        Id = "1baa5566-043c-488d-a704-5ba4e58bb994",
                        Message = "We have successfully checked the resource for you.",
                        Type = "Done"
                    }
                },
                ReplacementKey = "<!--1c7cd076-a724-4147-8a12-08c9073f0c38-->"
            },
            new ApolloDiagnostic()
            {
                Insights = new List<ApolloInsight>(),
                SolutionId = "HighCpuUsageAzurePortalInsight",
                Status = "Succeeded",
                StatusDetails = "The diagnostic did not detect any issues",
                Steps = new List<ApolloStep>()
                {
                    new ApolloStep()
                    {
                        Id = "b165eaf2-5af5-473c-ad99-77f7350ca09c",
                        Message = "We have successfully checked the resource for you.",
                        Type = "Done"
                    }
                },
                ReplacementKey = "<!--46bd63c9-b055-47ef-b723-9ce44f266d5e-->"
            }
        };

        return Task.FromResult(JsonSerializer.Serialize(mockApolloDiagnosticsForVmRdpIssue, new JsonSerializerOptions { WriteIndented = true }));
    }

    public Task<List<SupportProblemClassificationModel>> GetSupportProblemClassificationsForProduct(Guid productId)
    {
        if (!productId.ToString().Equals("6f16735c-b0ae-b275-ad3a-03479cfa1396", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Product ID must be a valid Azure VM product ID. Add mock response for other resource types if needed.", nameof(productId));
        }

        return Task.FromResult(new List<SupportProblemClassificationModel>
        {
            new SupportProblemClassificationModel(
                "/providers/Microsoft.Support/services/6f16735c-b0ae-b275-ad3a-03479cfa1396/problemClassifications/92c2396d-b703-973f-1bca-2eea9425b21a",
                "92c2396d-b703-973f-1bca-2eea9425b21a",
                new SupportProblemClassificationPropertiesModel(
                    "Cannot connect to my VM / Failure to connect using RDP or SSH port",
                    new List<SupportProblemSecondaryConsentModel>
                    {
                        new SupportProblemSecondaryConsentModel(
                            "Azure Support may need to access your virtual machine's memory to diagnose the problem. Support may pause it for up to 10 minutes.",
                            "VirtualMachineMemoryDump"
                        )
                    },
                    new SupportProblemClassificationMetadataModel(
                        "Resolve issues related to connecting via RDP or SSH not covered by other support topics",
                        "cannotrdpazureportalinsight;highcpuusageazureportalinsight;cannotrdplrdazureportalinsight;vmnwhealthmaxflowlimit;triplefaultazureportalinsight",
                        "Connectivity",
                        "",
                        "public",
                        "True",
                        "32615526"
                    )
                )
            )
        });
    }

    public Task<List<SupportProductFromArmModel>> GetSupportProductsFromArm(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
        {
            throw new ArgumentException("Resource ID cannot be null or empty.", nameof(resourceId));
        }

        if (resourceId.IndexOf("Microsoft.Compute/virtualMachines", StringComparison.OrdinalIgnoreCase) < 0)
        {
            throw new ArgumentException("Resource ID must be a valid Azure VM resource ID. Add mock response for other resource types if needed.", nameof(resourceId));
        }

        return Task.FromResult(new List<SupportProductFromArmModel>
        {
            new SupportProductFromArmModel(
                "/providers/Microsoft.Support/services/5c41904f-1bcf-76e4-7a54-5fc07468f3cc",
                "5c41904f-1bcf-76e4-7a54-5fc07468f3cc",
                "Microsoft.Support/services",
                new SupportProductFromArmPropertiesModel(
                    "Azure Update Manager",
                    new List<string>
                    {
                        "Microsoft.HybridCompute/machines",
                        "Microsoft.Maintenance/maintenanceConfigurations",
                        "Microsoft.Maintenance/configurationAssignments",
                        "MICROSOFT.AUTOMATION/AUTOMATIONACCOUNTS",
                        "Microsoft.Compute/virtualMachines"
                    },
                    new SupportProductFromArmPropertiesMetadataModel(
                        "public",
                        "ServiceGroupMonitoringManagement",
                        "17470",
                        ""
                    )
                )
            ),
            new SupportProductFromArmModel(
                "/providers/Microsoft.Support/services/6f16735c-b0ae-b275-ad3a-03479cfa1396",
                "6f16735c-b0ae-b275-ad3a-03479cfa1396",
                "Microsoft.Support/services",
                new SupportProductFromArmPropertiesModel(
                    "Virtual Machine running Windows",
                    new List<string>
                    {
                        "MICROSOFT.CLASSICCOMPUTE/VIRTUALMACHINES",
                        "MICROSOFT.COMPUTE/VIRTUALMACHINES"
                    },
                    new SupportProductFromArmPropertiesMetadataModel(
                        "public",
                        "ServiceGroupCompute",
                        "14749",
                        "WINDOWS"
                    )
                )
            )
        });
    }
}
