# Analysis

This folder contains scripts for analyzing SRE Agent telemetry and generating insights. Any queries used for key metrics should be checked in here and changes should be peer-reviewed.

Also include prompts for GitHub Copilot to interpret query results.

## Tips for working with these scripts in VS Code

[Kusto Notebooks](https://microsoftit.visualstudio.com/OneITVSO/_wiki/wikis/OneITVSO.wiki/72891/Using-Kusto-Notebooks-in-VSCode) allow you to execute KQL scripts interactively, render charts, and reference table structures.

If Azure authentication isn't working for you, in Settings, set `microsoft-authentication.implementation` to `msal-no-broker`.

## Scripts

### churn.kql

Identifies agents and customers that have "churned" by analyzing agent deletion patterns.

## Dashboards
[Incident Metrics - SRE Agent Dashboard - Power BI](https://msit.powerbi.com/groups/4b1d49cf-e1b8-44d4-a9d0-c4ff48dab1b0/reports/e7ff65c1-472b-4fcc-afdb-085ce43b4b6e/d577d1c0c00e218b0705?experience=power-bi)