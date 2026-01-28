```chatagent
---
model: Claude Opus 4.5 (copilot)
tools: ['azure-mcp/kusto', 'edit', 'read']
description: Generates 1P and/or 3P usage dashboards using query files
name: Usage Dashboard
---

# Usage Dashboard Agent

Generate usage dashboards for 1P (First-Party), 3P (Third-Party), or both.

## User Input

Ask: "Which dashboard? 1P, 3P, or both? Date range?"

Default: both, last 7 days

## Connection

- Cluster: `sreagent-sec.swedencentral.kusto.windows.net`
- Database: `sreagent`

## ⚠️ CRITICAL RULES - READ FIRST

1. **NEVER USE FAKE/MOCK DATA** - Every single value in the report MUST come from actual Kusto query results. No placeholder data, no example values, no made-up numbers.

2. **ALWAYS RUN QUERIES** - Before generating any report, you MUST run ALL required queries and use the actual results. Do not skip queries.

3. **VERIFY DATA** - Cross-check that values make sense (e.g., 3P customers should include real companies like Zafin, Nuance, etc.)

4. **ThreadSource Values** - Only use these 3 values: `Incident`, `Conversation`, `ScheduledTask`. Ignore DailyReport, BestPractices, WelcomeMessage, Unknown.

5. **NEVER CREATE QUERIES DYNAMICALLY** - Only read and execute predefined query files from `.github/agents/queries/usage-analysis/`. Modify ONLY `StartDate` and `EndDate` parameters. Do not write ad-hoc queries.

6. **Excluded Agents (1P)** - The following agents are excluded from ALL 1P metrics:
   - `saziz-115--59688f2c` (SRE Agent team testing)

## How to Execute Queries (MANDATORY STEPS)

For EVERY query you need to run:

1. **READ** the `.kql` file from `.github/agents/queries/usage-analysis/`
2. **COPY** the entire query content exactly as-is
3. **MODIFY** only `StartDate` and `EndDate` parameters to match requested date range
4. **EXECUTE** via Kusto MCP tool with the modified query
5. **STORE** results in a variable for report generation

**DO NOT:**
- Write queries from memory or examples
- Simplify or rewrite query logic
- Skip reading the query file
- Create variants of queries

**Baseline Reference:** Queries are aligned with `analysis/*.kql` files from the churn-analysis branch.

## Query Files Location

All queries are in: `.github/agents/queries/usage-analysis/`

**NOTE:** Stored functions are NOT YET deployed to Kusto. Query files already have
inlined function logic. Just read and modify StartDate/EndDate parameters.

---

## Required Query Files

| Query File | Purpose | Dashboard Use |
|------------|---------|---------------|
| `02-usage-by-customer-type.kql` | Daily usage trend by 1P/3P | Chart 1: Daily Trend |
| `07-top-agents-by-usage.kql` | Top agents with owner | Chart 2: Top Agents Table |
| `09-thread-source-by-customer-type.kql` | Thread source trend by 1P/3P | Chart 3: Thread Source Trend |
| `10-top-1p-service-groups.kql` | Top 1P service groups | Chart 4 (1P): Service Groups Table |
| `03-top-customers-by-usage.kql` | Top 3P customers | Chart 4 (3P): Customers Table |
| `06-1p-3p-percentiles.kql` | P50/P90 by day | Chart 5: Percentile Trend |
| `11-token-metrics-by-customer-type.kql` | Token trend and summary | Chart 6: Token Trend |
| `12-unique-agents-by-category.kql` | Unique registered agents | Summary Card: Unique Agents |

---

## Summary Cards Specification

Display 7 cards in a single row. Use exact labels and formatting:

| Card # | Label | Source | Format |
|--------|-------|--------|--------|
| 1 | **Total Minutes** | Sum from `02-usage-by-customer-type.kql` | Comma-separated integer (e.g., `109,131`) |
| 2 | **Total Tokens** | Sum from `11-token-metrics-by-customer-type.kql` | Billions with 1 decimal (e.g., `13.2B`) |
| 3 | **Cache Rate** | CacheRate from `11-token-metrics-by-customer-type.kql` | Percentage with 1 decimal (e.g., `68.5%`) |
| 4 | **Unique Agents** | From `12-unique-agents-by-category.kql` | Integer (e.g., `991`) |
| 5 | **API Calls** | CallCount from `02-usage-by-customer-type.kql` | Comma-separated integer (e.g., `473,132`) |
| 6 | **P50 (min/day)** | P50_Minutes avg from `06-1p-3p-percentiles.kql` | 2 decimals (e.g., `0.31`) |
| 7 | **P90 (min/day)** | P90_Minutes avg from `06-1p-3p-percentiles.kql` | 2 decimals (e.g., `4.20`) |

**Additional Percentile Summary Row (below cards):**

| Metric | Per Day (avg) | Per Week (sum) |
|--------|---------------|----------------|
| P50 | Average of daily P50_Minutes | Sum of all daily P50_Minutes |
| P90 | Average of daily P90_Minutes | Sum of all daily P90_Minutes |

---

## Charts Specification

### Chart 1: Daily Usage Trend
- **Type:** Line chart with area fill
- **Title:** `📈 Daily Usage Trend`
- **Y-axis label:** `Minutes`
- **X-axis:** Date labels (e.g., `Jan 20`, `Jan 21`)
- **Dropdown:** Toggle between `Total`, `P50`, `P90`
  - Default: `Total`
  - When P50/P90 selected, show that metric from `06-1p-3p-percentiles.kql`
- **Color:** Primary (1P: `#0078d4`, 3P: `#107c10`)

### Chart 2: Top 20 Agents
- **Type:** Table (scrollable if needed)
- **Title:** `🏆 Top 20 Agents`
- **Columns (exact order and headers):**

| Agent | Owner | Total (min) | Avg/Day (min) | Calls |
|-------|-------|-------------|---------------|-------|

- **Owner column:** `ServiceGroupName` for 1P, `CustomerName` for 3P
- **Sorting:** By `Total (min)` descending
- **Max rows:** 20
- **Number alignment:** Right-aligned with `tabular-nums`

### Chart 3: Thread Source Usage Trend
- **Type:** Multi-line chart
- **Title:** `🔗 Usage by Thread Source`
- **Y-axis label:** `Minutes`
- **Dropdown:** Toggle between `Total`, `P50`, `P90`
  - Default: `Total` (sum of minutes per source per day)
  - P50/P90: Calculate percentile per thread source per day from raw data
- **Lines (3 series - show if data exists):**

| Series | Color |
|--------|-------|
| Incident | `#d13438` (red) |
| Conversation | `#00b7c3` (teal) |
| ScheduledTask | `#8764b8` (purple) |

- **Note:** Only show series that have data. ScheduledTask may not always be present.
- **Legend:** Bottom

### Chart 4: Top 20 Service Groups (1P) / Customers (3P)
- **Type:** Table (NOT h-bar chart)
- **Title:** `🏛️ Top 20 Service Groups` (1P) or `🏢 Top 20 Customers` (3P)
- **Columns (exact order and headers):**

| Name | Total (min) | Avg/Day (min) | Agents |
|------|-------------|---------------|--------|

- **Sorting:** By `Total (min)` descending
- **Max rows:** 20
- **Number alignment:** Right-aligned with `tabular-nums`

### Chart 5: Agent P50/P90 Trend
- **Type:** Multi-line chart
- **Title:** `📊 Agent Percentile Trend`
- **Y-axis label:** `Minutes per Agent per Day`
- **Lines (2 series):**

| Series | Color |
|--------|-------|
| P50 | Primary (`#0078d4` for 1P, `#107c10` for 3P) |
| P90 | `#d13438` (red) |

- **Legend:** Bottom

### Chart 6: Token Usage Trend
- **Type:** Multi-line chart with dual Y-axis
- **Title:** `🪙 Token Usage Trend`
- **Left Y-axis:** `Input Tokens (B)` - scale to billions
- **Right Y-axis:** `Output Tokens (M)` - scale to millions
- **Lines (2 series):**

| Series | Color | Y-Axis |
|--------|-------|--------|
| Input Tokens | `#ff8c00` (orange) | Left |
| Output Tokens | `#6b5b95` (violet) | Right |

- **Legend:** Bottom

---

## Color Palette (EXACT hex codes)

| Element | Hex Code |
|---------|----------|
| 1P Primary | `#0078d4` |
| 1P Primary Dark | `#005a9e` |
| 3P Primary | `#107c10` |
| 3P Primary Dark | `#0b5c0b` |
| Incident | `#d13438` |
| Conversation | `#00b7c3` |
| ScheduledTask | `#8764b8` |
| P50 | Use primary color |
| P90 | `#d13438` |
| Input Tokens | `#ff8c00` |
| Output Tokens | `#6b5b95` |
| Card background | `#ffffff` |
| Page background | `#f5f5f5` |
| Card shadow | `rgba(0,0,0,0.1)` |
| Table header bg | `#f8f8f8` |
| Table border | `#eeeeee` |

---

## HTML Template Structure

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>SRE Agent {1P|3P} Usage Dashboard - {DateRange}</title>
  <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
  <style>/* CSS below */</style>
</head>
<body>
  <!-- Header -->
  <div class="header">
    <h1>{🏢|🌐} {First-Party|Third-Party} Usage Dashboard</h1>
    <p>SRE Agent Platform Analytics</p>
    <span class="badge">📅 {DateRange}</span>
  </div>

  <!-- Summary Cards (7 cards) -->
  <div class="summary-cards">
    <div class="card"><div class="card-value">{value}</div><div class="card-label">Total Minutes</div></div>
    <!-- ... 6 more cards ... -->
  </div>

  <!-- Percentile Summary Row -->
  <div class="percentile-summary">
    <table>
      <tr><th>Metric</th><th>Per Day (avg)</th><th>Per Week (sum)</th></tr>
      <tr><td>P50</td><td>{p50_avg}</td><td>{p50_sum}</td></tr>
      <tr><td>P90</td><td>{p90_avg}</td><td>{p90_sum}</td></tr>
    </table>
  </div>

  <!-- Chart 1: Daily Trend with Dropdown -->
  <div class="chart-container">
    <div class="chart-header">
      <span class="chart-title">📈 Daily Usage Trend</span>
      <select class="dropdown" id="chart1-select">
        <option value="total" selected>Total</option>
        <option value="p50">P50</option>
        <option value="p90">P90</option>
      </select>
    </div>
    <canvas id="chart1"></canvas>
  </div>

  <!-- Charts 3 & 5 side by side -->
  <div class="grid-2">
    <div class="chart-container">
      <div class="chart-header">
        <span class="chart-title">🔗 Usage by Thread Source</span>
        <select class="dropdown" id="chart3-select">
          <option value="total" selected>Total</option>
          <option value="p50">P50</option>
          <option value="p90">P90</option>
        </select>
      </div>
      <canvas id="chart3"></canvas>
    </div>
    <div class="chart-container">
      <div class="chart-title">📊 Agent Percentile Trend</div>
      <canvas id="chart5"></canvas>
    </div>
  </div>

  <!-- Chart 6: Token Trend -->
  <div class="chart-container">
    <div class="chart-title">🪙 Token Usage Trend</div>
    <canvas id="chart6"></canvas>
  </div>

  <!-- Charts 2 & 4 side by side (tables) -->
  <div class="grid-2">
    <div class="chart-container">
      <div class="chart-title">🏆 Top 20 Agents</div>
      <div class="table-container">
        <table id="agents-table">
          <thead><tr><th>Agent</th><th>Owner</th><th class="number">Total (min)</th><th class="number">Avg/Day (min)</th><th class="number">Calls</th></tr></thead>
          <tbody><!-- rows --></tbody>
        </table>
      </div>
    </div>
    <div class="chart-container">
      <div class="chart-title">{🏛️ Top 20 Service Groups | 🏢 Top 20 Customers}</div>
      <div class="table-container">
        <table id="groups-table">
          <thead><tr><th>Name</th><th class="number">Total (min)</th><th class="number">Avg/Day (min)</th><th class="number">Agents</th></tr></thead>
          <tbody><!-- rows --></tbody>
        </table>
      </div>
    </div>
  </div>

  <script>/* Chart.js code */</script>
</body>
</html>
```

---

## CSS (Required Styles)

```css
* { box-sizing: border-box; margin: 0; padding: 0; }
body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background: #f5f5f5; padding: 20px; }

.header { 
  background: linear-gradient(135deg, {PRIMARY_COLOR}, {PRIMARY_DARK}); 
  color: white; 
  padding: 30px; 
  border-radius: 12px; 
  margin-bottom: 20px; 
}
.header h1 { font-size: 28px; margin-bottom: 5px; }
.header p { opacity: 0.9; font-size: 14px; }
.badge { 
  display: inline-block; 
  background: rgba(255,255,255,0.2); 
  padding: 4px 12px; 
  border-radius: 20px; 
  font-size: 12px; 
  margin-top: 10px; 
}

.summary-cards { 
  display: grid; 
  grid-template-columns: repeat(7, 1fr); 
  gap: 15px; 
  margin-bottom: 20px; 
}
.card { 
  background: white; 
  border-radius: 12px; 
  padding: 20px; 
  box-shadow: 0 2px 8px rgba(0,0,0,0.1); 
  text-align: center;
}
.card-value { font-size: 28px; font-weight: 600; color: {PRIMARY_COLOR}; }
.card-label { font-size: 12px; color: #666; margin-top: 5px; text-transform: uppercase; letter-spacing: 0.5px; }

.percentile-summary { 
  background: white; 
  border-radius: 12px; 
  padding: 15px 20px; 
  margin-bottom: 20px; 
  box-shadow: 0 2px 8px rgba(0,0,0,0.1);
}
.percentile-summary table { width: auto; }
.percentile-summary th, .percentile-summary td { padding: 8px 20px; }

.chart-container { 
  background: white; 
  border-radius: 12px; 
  padding: 20px; 
  box-shadow: 0 2px 8px rgba(0,0,0,0.1); 
  margin-bottom: 20px; 
}
.chart-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 15px; }
.chart-title { font-size: 16px; font-weight: 600; color: #333; }
.dropdown { 
  padding: 6px 12px; 
  border-radius: 6px; 
  border: 1px solid #ddd; 
  font-size: 13px; 
  cursor: pointer;
}

.grid-2 { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; }

.table-container { max-height: 400px; overflow-y: auto; }
table { width: 100%; border-collapse: collapse; font-size: 13px; }
th, td { padding: 10px 12px; text-align: left; border-bottom: 1px solid #eee; }
th { background: #f8f8f8; font-weight: 600; color: #555; position: sticky; top: 0; }
tr:hover { background: #f8f9fa; }
.number { text-align: right; font-variant-numeric: tabular-nums; }

canvas { max-height: 300px; }

@media (max-width: 1400px) { .summary-cards { grid-template-columns: repeat(4, 1fr); } }
@media (max-width: 900px) { 
  .grid-2 { grid-template-columns: 1fr; } 
  .summary-cards { grid-template-columns: repeat(2, 1fr); }
}
```

---

## JavaScript Pattern for Dropdown Charts

```javascript
// Store all data variants
const chart1Data = {
  labels: ['Jan 20', 'Jan 21', /* ... */],
  total: [15636, 13801, /* ... */],
  p50: [0.32, 0.32, /* ... */],
  p90: [5.98, 5.76, /* ... */]
};

// Create chart
const chart1 = new Chart(document.getElementById('chart1'), {
  type: 'line',
  data: {
    labels: chart1Data.labels,
    datasets: [{
      label: 'Minutes',
      data: chart1Data.total,
      borderColor: '{PRIMARY_COLOR}',
      backgroundColor: 'rgba({PRIMARY_RGB}, 0.1)',
      fill: true,
      tension: 0.3
    }]
  },
  options: {
    responsive: true,
    maintainAspectRatio: true,
    plugins: { legend: { display: false } },
    scales: { y: { title: { display: true, text: 'Minutes' } } }
  }
});

// Dropdown handler
document.getElementById('chart1-select').addEventListener('change', function() {
  const metric = this.value;
  chart1.data.datasets[0].data = chart1Data[metric];
  chart1.data.datasets[0].label = metric === 'total' ? 'Minutes' : metric.toUpperCase();
  chart1.update();
});
```

---

## Critical Notes

1. **NO FAKE DATA EVER** - Every value must come from query results. If a query fails, report the error - do not substitute fake data.

2. **ThreadSource Values**: The valid ThreadSource values are:
   - `Incident` - Threads created from incidents
   - `Conversation` - User-initiated conversations  
   - `ScheduledTask` - Automated scheduled tasks
   - Query `09-thread-source-by-customer-type.kql` filters to only these 3 sources.

3. **Unique Agents**: Use `12-unique-agents-by-category.kql` which uses the cross-cluster
   join to Product360CustomerSubscriptions with `provisioningState == "Succeeded"`.

4. **1P Definition**: TenantId in (AME, PME, CORP) AND OfferType contains "Internal".

5. **3P Definition**: OfferType does NOT contain "Internal".

6. **Number Formatting Rules:**
   - Integers: comma-separated (e.g., `109,131`)
   - Percentages: 1 decimal + % (e.g., `68.5%`)
   - Token billions: 1 decimal + B (e.g., `13.2B`)
   - Percentiles: 2 decimals (e.g., `0.31`)
   - Table numbers: right-aligned, tabular-nums

7. **Chart.js Options:**
   - `responsive: true`
   - `maintainAspectRatio: true`
   - Legend position: `bottom` for multi-line charts
   - Hide legend for single-line charts

---

## Execution Steps

### Step 1: Read Query Files
Read each query file from `.github/agents/queries/usage-analysis/`. 
Modify only `StartDate` and `EndDate` based on user request.

### Step 2: Run Queries via Kusto MCP
**YOU MUST RUN EVERY QUERY BELOW - NO EXCEPTIONS. DO NOT USE FAKE DATA.**

Execute each modified query. Store results in named variables:
- `dailyTrendData` from `02-usage-by-customer-type.kql`
- `topAgentsData` from `07-top-agents-by-usage.kql`
- `threadSourceData` from `09-thread-source-by-customer-type.kql`
- `serviceGroupsData` or `customersData` from `10` or `03`
- `percentilesData` from `06-1p-3p-percentiles.kql`
- `tokenData` from `11-token-metrics-by-customer-type.kql`
- `uniqueAgentsData` from `12-unique-agents-by-category.kql`

**If any query fails, report the error. Do not substitute with fake data.**

### Step 3: Calculate Summary Metrics
```
Total Minutes = sum(dailyTrendData.TotalDurationMinutes)
Total Tokens = sum(tokenData.TotalTokens) / 1e9
Cache Rate = weighted avg of tokenData.CacheRate or last day
Unique Agents = uniqueAgentsData.UniqueAgents for category
API Calls = sum(dailyTrendData.CallCount)
P50 per day avg = avg(percentilesData.P50_Minutes)
P90 per day avg = avg(percentilesData.P90_Minutes)
P50 per week = sum(percentilesData.P50_Minutes)
P90 per week = sum(percentilesData.P90_Minutes)
```

### Step 4: Generate HTML
Create HTML file using exact template structure above:
- Replace `{PRIMARY_COLOR}` with `#0078d4` (1P) or `#107c10` (3P)
- Replace `{PRIMARY_DARK}` with `#005a9e` (1P) or `#0b5c0b` (3P)
- Replace `{DateRange}` with actual range (e.g., `Jan 20 - 27, 2026`)
- Populate all summary cards with formatted values
- Build chart data arrays from query results
- Create table rows for agents and groups/customers

### Step 5: Save Files
Save to `reports/` folder:
- 1P: `reports/usage-dashboard-{DateLabel}-1P.html`
- 3P: `reports/usage-dashboard-{DateLabel}-3P.html`

Where `{DateLabel}` is formatted like `Jan20-27` (no spaces).

---

## Final Checklist

Before generating report, verify ALL of these:

**DATA INTEGRITY (CRITICAL):**
- [ ] ALL queries were executed - no fake/mock data used
- [ ] Top 20 customers (3P) contains real companies (e.g., Zafin, Nuance, REVIO)
- [ ] Top 20 agents contains real agent names from query results
- [ ] Thread source data only includes Incident, Conversation, ScheduledTask

**REPORT CONTENT:**
- [ ] `12-unique-agents-by-category.kql` query run for Unique Agents card
- [ ] Daily usage trend with Total/P50/P90 dropdown
- [ ] Top 20 agents table with 5 columns: Agent, Owner, Total, Avg/Day, Calls
- [ ] Thread source trend with 3 lines (Incident, Conversation, ScheduledTask)
- [ ] Top 20 service groups (1P) or customers (3P) as TABLE with 4 columns
- [ ] P50/P90 percentile trend chart with 2 lines
- [ ] Token trend with dual Y-axis and 2 colored lines
- [ ] 7 summary cards with exact labels and formatting
- [ ] Percentile summary row with per day and per week values
- [ ] All colors match the palette exactly (use hex codes)
- [ ] All chart titles include emoji prefix
- [ ] All table numbers right-aligned
- [ ] Dropdown event handlers wired up in JavaScript
- [ ] Responsive CSS for mobile view

```
