# RAG Evaluator Precision & Recall Implementation Plan

## Context
The current RAG evaluation flow in `RagEvaluator` produces a single retrieval score per `SearchMemory` tool call. We want to extend the evaluator to report:
- **Precision**: Of the retrieved items, what fraction are relevant (using LLM to assess relevance)
- **Recall**: Of all relevant items in an expanded candidate pool (2× K, no thresholds), what fraction were in the original retrieval

## Requirements
- Add precision and recall metrics to `RagEvaluationResult` for downstream analytics
- Use LLM to assess relevance of individual retrieved items
- Run secondary unfiltered memory search with 2× the original K to approximate the full relevant set
- Keep logging and error handling consistent with existing evaluator conventions
- Handle all retrieval types: trajectories, documents, user memories

## Implementation Tasks

### Task 1: Add sanity integration test for precision/recall

**Goal**: Verify precision and recall calculation works end-to-end with simple test data

**Integration Test Approach**:
- Use existing `Agent.Tests.Integration` infrastructure
- Populate test Azure AI Search index with 5 known trajectories from checked-in test data
- Execute `AgentMemoryPluginDefinition.SearchMemoryAsync()` directly with test query and empty resource ID
- Plugin returns markdown-formatted results matching actual SearchMemory tool output
- Test with simple scenario: 3 items retrieved, 2 relevant (based on ground truth) → expect precision ~0.67
- Test data should include: test query and list of relevant trajectory IDs for validation

**Test Data Structure**:
- Create `src/Agent/Agent.Tests.Integration/Data/RagEval/trajectories/*.json` - 5 trajectory files
- Create `src/Agent/Agent.Tests.Integration/Data/RagEval/test_cases.json` - query + relevant trajectory IDs mapping

**Relevant Files**:
- `src/Agent/Agent.Tests.Integration/RagEvaluatorTests.cs` - New integration test file
- `src/Agent/Agent.Runtime/ThreadEvaluator/RagEvaluator.cs` - Evaluation logic to be tested
- `src/Agent/Agent.Tests.Integration/TestHelpers.cs` - Test infrastructure setup
- `src/Agent/Agent.Evals/Rag/TrajectoryRetrievalEval.cs` - Reference for test patterns (index setup, ground truth)

**High-Level Approach**:
Set up test with 5 known trajectories in search index (loaded from checked-in test files). Execute `AgentMemoryPluginDefinition.SearchMemoryAsync()` with empty resource ID and test query (symptoms) to get formatted markdown results. Verify: (1) Search returns expected trajectories, (2) Results are properly formatted as markdown. Once precision/recall implementation is complete (Tasks 2-10), add validation that: (3) PrecisionScore and RecallScore fields exist and are between 0-1, (4) Metrics match expected values based on ground truth.

**Dependencies**: None

---

### Task 2: Extend RagEvaluationResult to include precision/recall fields

**Goal**: Update the result contract to store new metrics without breaking existing consumers

**Integration Test Approach**:
Test should now compile but precision/recall fields will be null until calculation is implemented

**Relevant Files**:
- `src/Agent/Agent.Runtime/ThreadEvaluator/RagEvaluator.cs` - RagEvaluationResult record definition
- `src/Agent/Agent.Web/Controllers/v1/ThreadEvaluationController.cs` - Potential consumer of results
- Grep for `RagEvaluationResult` to find serialization sites

**High-Level Approach**:
Add optional nullable double properties to the `RagEvaluationResult` record:
- `PrecisionScore` - Fraction of retrieved items that are relevant (0.0-1.0 or null)
- `RecallScore` - Fraction of relevant items that were retrieved (0.0-1.0 or null)
- `TotalRetrieved` - Number of items in original retrieval
- `RelevantRetrieved` - Number of relevant items in original retrieval
- `TotalRelevantInExpandedSet` - Number of relevant items in expanded 2× K search

Use nullable types so skipped evaluations can leave them null. Ensure JSON serialization handles nulls properly.

**Dependencies**: Task 1 complete

---

### Task 3: Add method to evaluate individual item relevance using LLM

**Goal**: Implement LLM-based relevance assessment for a single retrieved item (trajectory/document/memory)

**Integration Test Approach**:
Unit test the relevance method with known relevant and irrelevant items, verify true/false output

**Relevant Files**:
- `src/Agent/Agent.Runtime/ThreadEvaluator/RagEvaluator.cs` - Add private helper method
- `src/Agent/Agent.Evals/Evaluators/TrajectorySearchRelevanceEvaluator.cs` - Reference for LLM evaluation patterns
- `src/Agent/Agent.Runtime/Services/IncidentAnalysisService/IcmIncidentAnalysisService.cs` - Reference for IChatClient.GetResponseAsync patterns

**High-Level Approach**:
Create private async method `IsItemRelevant(string query, string item, CancellationToken)` that returns bool. Use IChatClient.GetResponseAsync with a concise prompt: "Does this retrieved item contain information relevant to addressing the query? Answer true or false." Parse response as boolean. On error or uncertain response, default to true (assume relevant) to avoid false negatives.

**Dependencies**: Task 2 complete

---

### Task 4: Add method to parse SearchMemory results into individual items

**Goal**: Extract individual trajectories, documents, and memories from the markdown-formatted SearchMemory result string

**Integration Test Approach**:
Unit test parsing with sample SearchMemory output strings, verify correct item count and content

**Relevant Files**:
- `src/Agent/Agent.Runtime/ThreadEvaluator/RagEvaluator.cs` - Add parsing method
- `src/Agent/Agent.Plugins/Definitions/AgentMemoryPluginDefinition.cs` - Reference for BuildMemoryResponse format

**High-Level Approach**:
Create private method `ParseRetrievalItems(string retrievalResult)` returning `List<string>`. Parse the markdown format:
- Split by section headers: "## Similar Past Incidents on the exact Same Resource", "## Past Incidents with Similar Symptoms", "## Related User Memories", "## Relevant Documentation"
- Within each section, split by item markers: "### " for trajectories, "**Memory N:**" for memories, "**Document N:**" for documents
- Extract full content of each item (including all sub-bullets/fields)
- Return flat list of item strings

**Dependencies**: Task 2 complete

---

### Task 5: Implement precision calculation from original results

**Goal**: Calculate precision by assessing relevance of items in original retrieval

**Integration Test Approach**:
Sanity test should show non-null precision score between 0-1

**Relevant Files**:
- `src/Agent/Agent.Runtime/ThreadEvaluator/RagEvaluator.cs` - Add precision calculation helper

**High-Level Approach**:
Create method `CalculatePrecision(string query, string retrievalResult, CancellationToken)` that:
1. Calls `ParseRetrievalItems` to extract items
2. For each item, calls `IsItemRelevant` to assess relevance
3. Counts relevant items
4. Returns `(precision: relevantCount / totalCount, totalRetrieved: totalCount, relevantRetrieved: relevantCount)`
Handle empty results by returning null precision. Use Task.WhenAll for parallel relevance checks.

**Dependencies**: Tasks 3 and 4 complete

---

### Task 6: Inject IAgentMemoryClient and AgentMemorySettings into RagEvaluator

**Goal**: Make memory search services available to evaluator for expanded search

**Integration Test Approach**:
Tests should still pass with new dependencies injected

**Relevant Files**:
- `src/Agent/Agent.Runtime/ThreadEvaluator/RagEvaluator.cs` - Constructor and fields
- `src/Agent/Agent.Web/Program.cs` - DI registration for RagEvaluator
- `src/Agent/Agent.Tests.Integration/TestHelpers.cs` - Update test DI setup

**High-Level Approach**:
Add constructor parameters `IAgentMemoryClient agentMemoryClient` and `AgentMemorySettings agentMemorySettings`. Store as private readonly fields `_agentMemoryClient` and `_agentMemorySettings`. Update DI registration in Program.cs to provide these dependencies. Update test setup to mock or provide real instances.

**Dependencies**: Task 2 complete (needed before implementing recall)

---

### Task 7: Add method to run expanded unfiltered search for recall

**Goal**: Execute secondary searches with 2× K and no thresholds to get full candidate pool

**Integration Test Approach**:
Verify expanded search returns more items than original (when index has sufficient data)

**Relevant Files**:
- `src/Agent/Agent.Runtime/ThreadEvaluator/RagEvaluator.cs` - Add expanded search method
- `src/Agent/Agent.Data/AgentMemory/IAgentMemoryClient.cs` - Search method signatures
- `src/Agent/Agent.Plugins/Definitions/AgentMemoryPluginDefinition.cs` - Reference for SearchTrajectoriesAsync patterns

**High-Level Approach**:
Create method `GetExpandedRetrievalSet(SearchMemoryCall call, int expandedK, CancellationToken)` that:
- Creates SearchParams with K=expandedK (min 10), VectorSimilarityThreshold=null, ExhaustiveKnn=true
- Calls SearchTrajectoriesAsync (same resource + similar symptoms), SearchUserMemoriesAsync, SearchCustomerDocumentsAsync based on enabled settings
- Formats results similar to AgentMemoryPluginDefinition.BuildMemoryResponse
- Returns list of formatted item strings (not full markdown, just individual items)
- Logs count of expanded items retrieved

**Dependencies**: Tasks 4 and 6 complete

---

### Task 8: Implement recall calculation using expanded set

**Goal**: Calculate recall by comparing original retrieval to expanded 2× K candidate pool

**Integration Test Approach**:
Sanity test should show non-null recall score, recall should typically be >= precision

**Relevant Files**:
- `src/Agent/Agent.Runtime/ThreadEvaluator/RagEvaluator.cs` - Add recall calculation helper

**High-Level Approach**:
Create method `CalculateRecall(string query, List<string> originalItems, SearchMemoryCall call, int originalCount, CancellationToken)` that:
1. Calls `GetExpandedRetrievalSet` with 2× originalCount as expandedK
2. For each item in expanded set, calls `IsItemRelevant`
3. Counts total relevant in expanded set
4. Returns `(recall: relevantInOriginal / totalRelevantInExpanded, totalRelevantInExpanded: count)`
Handle edge cases: if expanded set empty or no relevant items found, return null recall. Use Task.WhenAll for parallel relevance checks.

**Dependencies**: Tasks 3, 4, and 7 complete

---

### Task 9: Create combined precision/recall calculation method

**Goal**: Orchestrate precision and recall calculation together for efficiency

**Integration Test Approach**:
Sanity test should pass with both metrics populated

**Relevant Files**:
- `src/Agent/Agent.Runtime/ThreadEvaluator/RagEvaluator.cs` - Add combined calculation method

**High-Level Approach**:
Create method `CalculatePrecisionAndRecall(SearchMemoryCall call, string retrievalResult, CancellationToken)` that:
1. Parses original retrieval items
2. Calculates precision from original items
3. Runs expanded search and calculates recall
4. Returns tuple with all metrics: `(precision, recall, totalRetrieved, relevantRetrieved, totalRelevant)`
Optimize by reusing original item relevance assessments for recall calculation (items in original are subset of expanded). Wrap in try-catch, return nulls on error. Log warnings for failures.

**Dependencies**: Tasks 5 and 8 complete

---

### Task 10: Wire precision/recall into EvaluateSearchMemoryResult

**Goal**: Integrate precision/recall calculation into main evaluation flow

**Integration Test Approach**:
Sanity test should pass with precision and recall populated in RagEvaluationResult

**Relevant Files**:
- `src/Agent/Agent.Runtime/ThreadEvaluator/RagEvaluator.cs` - EvaluateSearchMemoryResult method

**High-Level Approach**:
In `EvaluateSearchMemoryResult`, after existing retrieval score evaluation:
1. Check if evaluation should be skipped (no results) - if so, set precision/recall to null
2. Call `CalculatePrecisionAndRecall` to get metrics
3. Populate new fields in RagEvaluationResult with calculated values
4. Log calculated metrics at Information level
5. On error, log warning and set metrics to null (don't fail entire evaluation)

Return updated RagEvaluationResult with all fields populated.

**Dependencies**: Task 9 complete

---

### Task 11: Add comprehensive error handling and edge case management

**Goal**: Handle all error scenarios gracefully without failing evaluation

**Integration Test Approach**:
Test edge cases with integration tests:
- Empty retrieval results (precision/recall null)
- All items irrelevant (precision=0, recall=0 or null)
- Expanded search returns nothing (recall=null, precision still calculated)
- LLM evaluation errors (default to relevant, log warning)
- Memory client errors (log error, return nulls)

**Relevant Files**:
- `src/Agent/Agent.Runtime/ThreadEvaluator/RagEvaluator.cs` - All evaluation methods

**High-Level Approach**:
Add try-catch blocks around:
- LLM relevance calls (default to true on error)
- Memory search calls (return empty list on error)
- Parsing logic (log error, return empty list)

Add null/empty checks before calculations. Log appropriate level (Warning for recoverable, Error for unexpected). Ensure RagEvaluationResult always returned with valid ThreadId/CallId even if metrics are null. Document error behavior in method comments.

**Dependencies**: Task 10 complete

---

### Task 12: Expand integration test to realistic scenarios

**Goal**: Validate with real search index and diverse query types

**Integration Test Approach**:
- Use populated Azure AI Search index with trajectories and documents from test data
- Create threads with multiple SearchMemory calls (different query types)
- Test scenarios:
  - High precision query (specific problem, clear matches)
  - Low precision query (vague problem, many false positives)
  - High recall query (common problem, most relevant items retrieved)
  - Low recall query (specific problem, relevant items missed)
- Verify metrics correlate with expected quality
- Verify metrics are logged to ApplicationInsights

**Relevant Files**:
- `src/Agent/Agent.Tests.Integration/RagEvaluatorTests.cs` - Expand test coverage
- `src/Agent/Agent.Evals/Data/TrajectorySearch/` - Test query data
- `src/Agent/Agent.Evals/Data/Trajectory/` - Test trajectory data

**High-Level Approach**:
Expand sanity test into comprehensive suite. Use ClassInitialize to set up search index once with known trajectories. Create multiple test methods for different scenarios. Use test data from Agent.Evals folders. Assert metrics are within expected ranges for each scenario. Add test to verify precision+recall calculations with known ground truth.

**Dependencies**: Task 11 complete

---

## Testing Plan

### Unit Tests
- `IsItemRelevant` with known relevant/irrelevant samples
- `ParseRetrievalItems` with various SearchMemory output formats
- Precision/recall calculations with mock data

### Integration Tests
- End-to-end RagEvaluator with real search index (Task 1, Task 12)
- Edge cases: empty results, errors, disabled features (Task 11)
- Performance test: large retrieval results (100+ items)

### Manual Validation
- Run evaluator on real production threads
- Compare precision/recall with manual assessment
- Verify metrics appear in logs and telemetry

## Success Criteria

✅ RagEvaluationResult includes precision and recall scores
✅ Precision calculated from original retrieval using LLM relevance assessment
✅ Recall calculated from expanded 2× K unfiltered search
✅ All retrieval types handled (trajectories, documents, user memories)
✅ Integration tests validate functionality with real search index
✅ Edge cases handled gracefully (empty results, errors, disabled features)
✅ Logging added for observability of metrics and errors
✅ No breaking changes to existing evaluation functionality
✅ Performance acceptable (evaluation completes within 30s for typical thread)
