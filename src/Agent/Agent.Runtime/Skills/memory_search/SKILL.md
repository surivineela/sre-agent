# Memory Search Skill

## Purpose

Search across agent memory knowledge bases to retrieve past incident trajectories, user memories, and technical documentation. Ground responses in organizational knowledge and learned patterns.

## When This Skill Applies

Load when:
- User asks substantive technical questions requiring organizational context
- Need past incident resolution patterns or troubleshooting guidance
- User references previously shared information or saved memories
- Technical topics benefit from documented knowledge vs. general training
- User explicitly requests to look up, search, or reference uploaded files/documents
- Questions about system architecture, design patterns, or architectural knowledge for which this skill is super useful for

**Do NOT load for:**
- Simple conversational exchanges (greetings, acknowledgments)
- Meta questions about the agent that don't require external knowledge

## Available Tools

### SearchMemory
Comprehensive search across all knowledge bases for any query or task. Searches three distinct sources:
- **Past incident trajectories**: Resolution steps and root causes from previous incidents
- **User memories**: Explicit facts and preferences saved by users  
- **Documentation**: TSG guides, runbooks, and technical documentation

**When to use**: Proactively at conversation start or when exploring any topic to ground responses in historical knowledge and learned patterns. This is your primary tool for leveraging organizational knowledge.

**Query parameter**: Comprehensive search query describing the topic, symptoms, error messages, or concepts. Include relevant technical terms, error codes, service names, or behavior descriptions for better semantic matching.

### SearchIncidentKnowledge
Targeted search for specialized incident resolution knowledge. Searches three sources with focus on incident resolution:
- **Past incident trajectories**: Especially those on the same resource (highest relevance) or with similar symptoms
- **User memories**: Relevant to troubleshooting
- **Technical documentation**: Supporting diagnostic information

**When to use**: Investigating service incidents, troubleshooting failures, or when you need historical context about how similar problems were resolved.

**Parameters**:
- `resourceId`: Full Azure resource ID (e.g., '/subscriptions/{sub-id}/resourceGroups/{rg}/providers/Microsoft.Web/sites/{app-name}'). Prioritizes incidents on exact same resource. Always provide if available - even partial matches help.
- `symptoms`: Detailed description of symptoms, error messages, failure patterns, or observed behaviors. Include error codes, HTTP status codes, service names, timestamps, failure scenarios, or technical indicators.

## Query Strategy

**Create comprehensive queries:**
- Include specific technical terms, service names, error codes, or behavior descriptions
- Be detailed - richer context improves semantic matching
- Examples from plugin:
  - SearchMemory: "Function app cold start performance issues", "HTTP 503 errors in Azure App Service", "Best practices for configuring Azure Redis cache"
  - SearchIncidentKnowledge: "Application returning 502 Bad Gateway after deployment", "Database connection timeouts during peak hours", "Container app crashes with OutOfMemory exceptions"

**Tool Selection:**

**SearchMemory** - Use for:
- General knowledge lookup at conversation start
- "How to" questions and best practices
- Exploring topics or concepts
- No specific resource involved

**SearchIncidentKnowledge** - Use for:
- Investigating specific resource incidents (provide resourceId)
- Troubleshooting active failures
- Symptom-based diagnostic queries
- Need historical context on similar problems

## Using Retrieved Content

**Memory search results include:**

**Past Incidents (Two Categories)**:
1. **Similar Past Incidents on the Same Resource**: Highest likelihood of helping with current resolution
2. **Past Incidents with Similar Symptoms**: May provide insights into resolution

Each includes: Title, Symptoms Observed, Steps Followed for Resolution, Root Cause, Pitfalls to Avoid

**User Memories**: Previously saved facts and preferences (truncated to 300 chars)

**Documentation**: Relevant technical documentation with:
- Document title and type (User Document or TSG Document)
- LLM-generated summary (up to 200 chars)
- Full content when available
- URL for TSG documents
- Relevance scores (vector similarity and reranker scores)

**Quality Filtering**: Results filtered by vector similarity threshold and minimum reranker score. Duplicates removed by title.

**Present naturally:**
- Lead with direct answer addressing the user's question
- Reference specific past incidents or patterns when applicable
- Preserve technical accuracy from documentation
- Don't mention the search process or tools used

**When no results (specific messages from plugin):**
- Overall: "No relevant memories, documents, or past incidents found for the current symptoms"
- Same resource: "No past incidents found on the same resource"
- Similar symptoms: "No past incidents found with similar symptoms"
- User memories: "No relevant user memories found"
- Documentation: "No relevant documents found"

Use general knowledge cautiously when memory search yields no results, clearly distinguishing it from memory findings.

## Response Formatting

**Leverage structured memory results:**

Memory results are organized in markdown sections:

**## Similar Past Incidents on the exact Same Resource**
For each incident:
- **Symptoms**: What was observed
- **Steps followed for resolution**: Actions taken
- **Root Cause**: Identified cause
- **Pitfalls to avoid**: Warnings from experience

**## Past Incidents with Similar Symptoms**
Same structure as above, for broader symptom matches

**## Related User Memories**
Numbered memories with truncated content (300 chars max)

**## Relevant Documentation**
For each document:
- Document title and number
- **Content**: Full or summarized content
- **Link**: URL (for TSG documents)

**Your response should**:
1. Extract key insights addressing the user's question
2. Reference specific resolution steps from past incidents
3. Note pitfalls to avoid
4. Include documentation links when provided

## Knowledge Sources

Memory search covers:
- **Past Incidents**: Trajectories from previous investigations (symptoms, steps, root causes)
- **User Memories**: Facts and preferences explicitly saved by users
- **Documentation**: TSG guides, runbooks, technical documentation (Azure, Kubernetes, CLI)
- **User Uploaded Files**: Custom documents uploaded by users (.md, .txt files up to 16MB)

**User Uploaded Files**:
- Users can upload custom documentation, runbooks, configuration files to the knowledge base
- Accepted formats: .md (Markdown), .txt (Text)
- Maximum file size: 16MB per file
- Files are indexed and searchable through both SearchMemory and SearchIncidentKnowledge
- Uploaded files appear as "User Document" type in search results
- No public URLs (unlike TSG documents)

Note: Memory content may be newer than your training data. Prefer memory results when available.

## Best Practices

**Query Design:**
- Comprehensive queries with technical terms and context
- Include error codes, service names, symptoms
- Provide resourceId when investigating specific resources
- NO Vague or conversational queries

**Content Presentation:**
- Lead with insights from memory findings
- Reference past incident patterns directly
- Preserve technical details from results
- DONT Mention search process or tools

**Tool Selection:**
- SearchMemory for general knowledge and exploration
- SearchIncidentKnowledge for resource-specific troubleshooting
- Use SearchMemory proactively at conversation start
