// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using FirstPartyAgent.Models;

namespace FirstPartyAgent.Tests.Integration.TestCases
{
    public class QuotaAgentBenchmarkTest
    {

        [Theory]
        [MemberData(nameof(TestFiles))]
        public async void TestQuotaAgent(string fileName)
        {
            var testDescriber = LoadFile(fileName);

            TestAgentApplicationBuilder builder = new TestAgentApplicationBuilder().DisableBackgroundTask();
            TestAgentApplication app = builder.Build();

            var request = new QuotaIncidentState
            {
                Incident = new IcmIncident
                {
                    Id = testDescriber.IncidentId,
                    Title = $"Test Incident <{testDescriber.IncidentId}>",
                },
                Summary = testDescriber.Summary,
            };

            var output = await app.CreateQuotaAgentService().Process(request, null);

            foreach (var reply in testDescriber.Replies)
            {
                Assert.Equal(reply.Key, output.ApprovalResult.ToString());
                output = await app.CreateQuotaAgentService().Process(request, new List<Discussion> { new Discussion("John", DiscussionSource.Teams, reply.Value) });
            }

            // Validate the final outcome
            var actual = output.GetTestDescriber(); // Just for easy to write the test data.
            Assert.True(testDescriber.Validate(output), $"The final result is incorrect:\n {output.ToString()}");
        }

        public static IEnumerable<object[]> TestFiles
        {
            get
            {
                yield return new object[] { "ACA_Quota_AutoReject.html" };
                yield return new object[] { "ACA_Quota_AutoApprove.html" };
                yield return new object[] { "ACA_Quota_ManualApprove.html" };
            }
        }

        private QuotaTestDescriber LoadFile(string fileName)
        {
            string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            string filePath = Path.Combine(assemblyDirectory, @"TestData", fileName);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"The file {filePath} was not found.");
            }

            QuotaTestDescriber describer = new QuotaTestDescriber();

            string[] lines = File.ReadAllLines(filePath);
            StringBuilder summary = new StringBuilder();

            foreach (var line in lines)
            {
                if (line.StartsWith("#Reply"))
                {
                    var match = Regex.Match(line, @"#Reply:\s*(?<key>\w+)\s*,\s*(?<value>.*)");
                    if (match.Success)
                    {
                        describer.Replies.Add(match.Groups["key"].Value.Trim(), match.Groups["value"].Value.Trim());
                    }
                    else
                    {
                        throw new InvalidOperationException($"The line {line} is not recognized.");
                    }
                }
                else if (line.StartsWith("#"))
                {
                    var match = Regex.Match(line, @"#(?<key>\w+):\s*(?<value>.*)");
                    if (match.Success)
                    {
                        describer.State.Add(match.Groups["key"].Value.Trim(), match.Groups["value"].Value.Trim());
                    }
                    else
                    {
                        throw new InvalidOperationException($"The line {line} is not recognized.");
                    }

                    continue;
                }

                summary.AppendLine(line);
            }

            describer.IncidentId = "88888888";
            describer.Summary = summary.ToString();

            return describer;
        }

        public class QuotaTestDescriber
        {
            public string? IncidentId { get; set; }

            public string? Summary { get; set; }

            public Dictionary<string, string> State { get; set; } = new Dictionary<string, string>();

            public Dictionary<string, string> Replies { get; set; } = new Dictionary<string, string>();

            public bool Validate(QuotaIncidentState state)
            {
                foreach (var pair in State)
                {
                    if (state.GetType().GetProperty(pair.Key)?.GetValue(state)?.ToString() != pair.Value)
                    {
                        return false;
                    }
                }
                return true;
            }
        }
    }
}
