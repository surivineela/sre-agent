# SRE Agent

[![Build Status](https://msazure.visualstudio.com/One/_apis/build/status%2FOneBranch%2FAAPT-Antares-OperationalAgent%2FSREAgent-Runtime%2FSREAgent-Runtime-PullRequest?repoName=serverless-paas-balam%2Fsreagent-runtime&branchName=main)](https://msazure.visualstudio.com/One/_build/latest?definitionId=420740&repoName=serverless-paas-balam%2Fsreagent-runtime&branchName=main)

Azure SRE Agent is a unified agentic platform for monitoring and troubleshooting Azure applications and services.

![Component Diagram](docs/images/sre-components.svg)

## Quick Start

1. [Join the SRE Agent Devs Security Group](https://idweb.microsoft.com/IdentityManagement/aspx/common/GlobalSearchResult.aspx?searchtype=e0c132db-08d8-4258-8bce-561687a8a51e&content=srea-dev&popupFromClipboard=%2Fidentitymanagement%2Faspx%2FGroups%2FEditGroup.aspx%3Fid%3Dc1f5644a-2ef3-499f-ad48-39b3dc889eeb) to get permissions to push to this repository.

2. Set up your development environment by following our [Development Setup Guide](docs/development-setup.md)

3. Run the application following the [Running the Application](docs/running-the-app.md) guide

4. Authoring your first agent? [Read the Agent Handbook.](https://github.com/serverless-paas-balam/sreagent-runtime/wiki/Agents-Handbook).

## Document Management

The SRE Agent includes document management capabilities through the `srectl` CLI tool:

- **Upload Documents**: Add documents and folders to the agent's knowledge base
- **Search Documents**: Query indexed documents for relevant information
- **Reindex**: Rebuild the document index for improved search performance

See the [SRECTL Reference](docs/Extensibility/srectl-reference.md) for detailed command usage.

## Project Resources

- [Github Tracking Board](https://github.com/orgs/serverless-paas-balam/projects/196/views/2)
- [Architecture Overview](docs/architecture.md)
- [Graph Database Guide](docs/graph-database.md)
- [Graph Visualization Guide](docs/graph-visualization.md)
- [Deployment Guide](docs/deployment.md)

## Need Help?

- Review the [Troubleshooting Guide](docs/troubleshooting.md).  
- Email **srea-devs@microsoft.com**.
