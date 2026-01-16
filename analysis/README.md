# Analysis

This folder contains scripts for analyzing SRE Agent telemetry and generating insights. Any queries used for key metrics should be checked in here and changes should be peer-reviewed.

Also include prompts for GitHub Copilot to interpret query results.

## Tips for working with these scripts in VS Code

[Kusto Notebooks](https://microsoftit.visualstudio.com/OneITVSO/_wiki/wikis/OneITVSO.wiki/72891/Using-Kusto-Notebooks-in-VSCode) allow you to execute KQL scripts interactively, render charts, and reference table structures.

If Azure authentication isn't working for you, in Settings, set `microsoft-authentication.implementation` to `msal-no-broker`.

## Scripts

### churn.kql

Identifies agents and customers that have "churned" by analyzing agent deletion patterns.
