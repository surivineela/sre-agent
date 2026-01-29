#!/usr/bin/env python3
"""Quick script to verify agent counts with proper 3P filtering"""

from azure.identity import AzureCliCredential
from azure.kusto.data import KustoClient, KustoConnectionStringBuilder

credential = AzureCliCredential()
kcsb = KustoConnectionStringBuilder.with_azure_token_credential(
    'https://sreagent-sec.swedencentral.kusto.windows.net', 
    credential
)
client = KustoClient(kcsb)

# Match the user's original query more closely
query1 = """
let ReportStartDate = datetime(2026-01-16);
let ReportEndDate = datetime(2026-01-23);

// Agents active within the reporting week only
All('AgentDocumentDBState')
| where PreciseTimeStamp >= ReportStartDate and PreciseTimeStamp < ReportEndDate
| where isnotempty(agentEndpoint)
| extend shortAgentName = tostring(split(tostring(split(agentEndpoint, "/")[-1]), ".")[0])
| where shortAgentName !startswith "e2e" and shortAgentName !startswith "crud"
| join kind=leftouter (
    cluster("customerdomrptwus3prod.westus3.kusto.windows.net")
    .database("customerdomdata")
    .Product360CustomerSubscriptions
    | project SubscriptionId, OfferType
) on $left.subscriptionId == $right.SubscriptionId
| where OfferType !contains "Internal"
| summarize lastSnapshot = max(PreciseTimeStamp), firstSnapshot = min(PreciseTimeStamp) by shortAgentName
| extend isDeleted = lastSnapshot < startofday(ReportEndDate - 1d)
| summarize 
    TotalAgents = dcount(shortAgentName),
    ActiveAgents = dcountif(shortAgentName, not(isDeleted)),
    DeletedAgents = dcountif(shortAgentName, isDeleted)
"""

print("Agent counts (reporting week only, no 90-day lookback):")
print("-" * 50)
result = client.execute('sreagent', query1)
for row in result.primary_results[0]:
    print(f'Total: {row["TotalAgents"]}, Active: {row["ActiveAgents"]}, Deleted: {row["DeletedAgents"]}')
