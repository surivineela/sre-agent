
# ICM Plugin

## Main Functions

### Incident Details

#### GetIncidentInfo

Fetches detailed information about an ICM incident, including summary and discussion entry content.

**Input Parameters:**
- `incidentId` (string): Incident ID.

---

#### GetCustomFields

Retrieves custom fields for a given incident.

**Input Parameters:**
- `incidentId` (string): Incident ID.

---

### Incident Search

#### SearchIncidents

Searches for incidents using a search string, lookback period, and result count limit.

**Input Parameters:**
- `searchString` (string): Search string.
- `lookbackPeriodInDays` (int): Lookback period in days.
- `resultCountLimit` (int): Limit on result count.

---

#### GetQueryableColumnsForIncidentLookup

Returns a dictionary of queryable columns and their allowed operators for advanced incident search.

**Input Parameters:**
- *(none)*

---

#### AdvancedSearchIncidents

Performs an advanced search using column names, operators, and filter values.

**Input Parameters:**
- `lookbackPeriodInDays` (int): Lookback period in days.
- `resultLimit` (int): Limit on result count (max 10).
- `columnNames` (List<string>): List of column names to search.
- `operators` (List<string>): Operator to apply on each column.
- `filterValues` (List<string>): Filter to apply on each column.

---

### Discussion Entries

#### GetAlertingDiscussionEntry

Retrieves the Azure Alerting discussion entry for an incident.

**Input Parameters:**
- `incidentId` (string): Incident ID.

---

#### GetDiscussionEntries

Fetches all discussion entries for an incident.

**Input Parameters:**
- `incidentId` (string): Incident ID.

---

#### PostDiscussionEntry

Posts a new discussion entry to an incident.

**Input Parameters:**
- `incidentId` (string): Incident ID.
- `discussionEntry` (string): Discussion entry (HTML).

---

### Incident Actions

#### TransferIncident

Transfers an incident to another team, adding appropriate tags and discussion entry.

**Input Parameters:**
- `incidentId` (string): Incident ID.
- `discussionEntry` (string): Reason for transferring the incident.
- `tenantName` (string): Tenant ID of the team to transfer the incident to.
- `owningTeam` (string): Team ID of the team to transfer the incident to.

---

#### MitigateIncident

Mitigates an incident, posts a discussion entry, and adds mitigation tags.

**Input Parameters:**
- `incidentId` (string): Incident ID.
- `discussionEntry` (string): Reason for mitigating the incident (HTML).

---

#### DowngradeSeverity

Downgrades the severity of an incident (e.g., from Sev2 to Sev3).

**Input Parameters:**
- `incidentId` (string): Incident ID.
- `discussionEntry` (string): Reason for downgrading the incident (HTML).

---

#### ResolveIncident

Resolves an incident with a discussion entry.

**Input Parameters:**
- `incidentId` (string): Incident ID.
- `discussionEntry` (string): Reason for resolving the incident (HTML).

---

#### AcknowledgeIncident

Acknowledges an incident and adds a processing tag.

**Input Parameters:**
- `incidentId` (string): Incident ID.

---

#### AddTagToIncident

Adds a tag to an incident.

**Input Parameters:**
- `incidentId` (string): Incident ID.
- `tag` (string): Tag to add.

---

### Related and Parent/Child Incidents

#### GetLinkedRelatedIncidentInfo

Gets info for all incidents linked as related to a given incident.

**Input Parameters:**
- `incidentId` (long): Incident ID.

---

#### AddRelatedIncidentLink

Adds a related incident link.

**Input Parameters:**
- `incidentId` (long): Incident ID to assign a related incident to.
- `relatedIncidentId` (long): Incident ID to assign as a related incident.

---

#### RemoveRelatedIncidentLink

Removes a related incident link.

**Input Parameters:**
- `incidentId` (long): Incident ID to remove the related incident from.
- `relatedIncidentId` (long): Incident ID to remove as a related incident.

---

#### GetParentIncidentInfo

Gets info for the parent incident.

**Input Parameters:**
- `incidentId` (long): Incident ID.

---

#### AddParentIncidentLink

Adds a parent incident link.

**Input Parameters:**
- `incidentId` (long): Incident ID to assign a parent to.
- `parentIncidentId` (long): Incident ID to assign as a parent.

---

#### RemoveParentIncidentLink

Removes a parent incident link.

**Input Parameters:**
- `incidentId` (long): Incident ID to remove the parent from.

---

#### GetChildIncidentsInfo

Gets info for all child incidents.

**Input Parameters:**
- `incidentId` (long): Incident ID.

---

## Usage Notes

- The plugin automatically chooses between the ICM API and ICM Workflow client based on availability.
- All major actions are logged and can be integrated with Teams notifications and session messages.
- Some methods process HTML and image content, extracting text from images using AI services if enabled.
- Tags such as `SREAgent_HumanIntervention`, `SREAgent_Processed`, and `SREAgent_Mitigated` are used to track incident processing status.

---