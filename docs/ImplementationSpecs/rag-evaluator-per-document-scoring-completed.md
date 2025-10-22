# RAG Evaluator Per-Document Scoring Implementation - Completed

## Overview
Implemented per-document scoring functionality for the RAG (Retrieval-Augmented Generation) evaluator to provide granular evaluation of each retrieved item (trajectories, documents, user memories) using a single LLM call.

**Completion Date**: 2025-10-15

---

## Problem Statement

The original RAG evaluator produced only a single aggregate retrieval score per `SearchMemory` tool call. This made it difficult to:
- Understand which specific documents were relevant vs. irrelevant
- Calculate precision (fraction of retrieved items that are relevant)
- Assess ranking quality (whether most relevant items appear at the top)
- Debug retrieval quality issues

---

## Solution Design

### Architecture Decision: Single LLM Call with Per-Document Assessment

**Chosen Approach**: Single LLM call that evaluates all documents at once and returns structured JSON with per-document scores.

**Rationale**:
1. **Cost Efficiency**: One API call instead of N separate calls
2. **Context Awareness**: LLM can compare documents relatively and maintain consistent scoring thresholds
3. **Consistency**: Same evaluation context ensures fair comparison across all items
4. **Performance**: Significantly faster than sequential document evaluation

**Alternative Considered**: Multiple LLM calls (one per document)
- Rejected due to higher API costs and potential inconsistency in scoring thresholds

### Data Model

#### Input Structure
The evaluator receives markdown-formatted retrieval results from `AgentMemoryPluginDefinition.SearchMemoryAsync()`:

```markdown
## Past Incidents with Similar Symptoms
### Web App High CPU - Memory Leak Investigation
- **Symptoms:** High CPU usage at 95%...
- **Steps followed for resolution:** 1. Monitored memory usage...
- **Root Cause:** Memory leak in application code...
- **Pitfalls to avoid:** Avoid restarting app...

## Related User Memories
**Memory 1:**
> Some user memory content...

## Relevant Documentation
**Document 1:**
```
Document content here
```
```

#### Output Structure
The LLM returns structured evaluation:

```csharp
public record RetrievalEvaluationResult(
    string ThoughtChain,                      // LLM's step-by-step reasoning
    List<DocumentRelevanceScore> DocumentScores,  // Per-item evaluations
    string RankingReasoning,                  // Ranking quality explanation
    int RankingQualityScore                   // 1-5 scale
);

public record DocumentRelevanceScore(
    string Title,           // Trajectory title or "Memory N" or "Document N"
    int RelevanceScore,     // 1-5 scale, >=4 considered "relevant"
    string Reasoning        // Brief explanation
);
```

#### Result Record
Extended `RagEvaluationResult` to include new metrics:

```csharp
public record RagEvaluationResult(
    Guid ThreadId,
    Guid AgentContextId,
    string CallId,
    string ResourceId,
    string SearchQuery,
    double RetrievalScore,      // Average of all document relevance scores
    double Precision,            // Fraction of docs with score >= 4
    double? Recall,              // Nullable - to be populated later
    int RankingScore,            // LLM's ranking quality assessment (1-5)
    string Explanation,          // JSON-serialized DocumentScores
    string Reasoning,            // LLM's thought chain
    DateTime EvaluatedAt
);
```

---

## Implementation Details

### 1. Enhanced Prompt Engineering

#### System Prompt
Positioned evaluator as expert in retrieval quality assessment:
- Defines the goal: evaluate CONTEXT chunks based on QUERY
- Emphasizes relevance without factual correctness bias
- Explains evaluation is based on definitions and data provided

#### Input Structure Documentation
Added explicit documentation of the markdown format to help LLM parse items:

```
1. **Trajectories (Past Incidents)**:
   - Section headers: "## Similar Past Incidents..." or "## Past Incidents with Similar Symptoms..."
   - Item identifier: `### [Trajectory Title]`
   - Content: Bullet points with Symptoms, Steps, Root Cause, Pitfalls

2. **User Memories**:
   - Section header: "## Related User Memories"
   - Item identifier: `**Memory N:**`
   - Content: Quoted text

3. **Documents**:
   - Section header: "## Relevant Documentation"
   - Item identifier: `**Document N:**`
   - Content: Code blocks (```)
```

#### Retrieval Score Definitions (1-5 Scale)
Provided 5 detailed rating levels with examples:

- **Score 1**: Irrelevant context, external knowledge bias
- **Score 2**: Partially relevant, poor ranking, external bias
- **Score 3**: Relevant context ranked at bottom
- **Score 4**: Relevant context ranked in middle
- **Score 5**: Highly relevant, well-ranked, no bias

Each definition includes multiple real-world examples from Microsoft.Extensions.AI.Evaluation.Quality library.

#### Output Instructions
Structured output request:

```
- **ThoughtChain**: Step-by-step reasoning starting with "Let's think step by step:"

- **DocumentScores**: Array with one entry per CONTEXT item
  - **Title**: Extract title/identifier exactly as it appears
  - **RelevanceScore**: Integer 1-5 for this specific item
  - **Reasoning**: Brief explanation for the score

- **RankingReasoning**: Assess if most relevant items appear at top

- **RankingQualityScore**: Integer 1-5 for overall ranking quality
```

### 2. Metric Calculations

#### Precision
```csharp
var totalDocs = evalResult.DocumentScores.Count;
var relevantDocs = evalResult.DocumentScores.Count(d => d.RelevanceScore >= 4);
var precision = totalDocs > 0 ? (double)relevantDocs / totalDocs : 0;
```

**Relevance Threshold**: Documents with RelevanceScore >= 4 are considered "relevant"

#### Retrieval Score (Average)
```csharp
var averageRelevanceScore = totalDocs > 0
    ? evalResult.DocumentScores.Average(d => d.RelevanceScore)
    : 0;
```

#### Recall
Currently set to `null` - to be implemented in future phase using expanded 2× K search approach.

### 3. Error Handling

Implemented three levels of error handling:

1. **Skip Evaluation**: When no relevant results found
   ```csharp
   RetrievalScore: -1,
   Precision: -1,
   Recall: null,
   RankingScore: -1,
   Explanation: "Skipped - No relevant results found"
   ```

2. **Evaluation Failure**: When LLM call fails
   ```csharp
   RetrievalScore: -1,
   Precision: -1,
   Recall: null,
   RankingScore: -1,
   Explanation: "Evaluation failed",
   Reasoning: $"Error: {ex}"
   ```

3. **Graceful Degradation**: Log errors, continue evaluation, set metrics to null/defaults

---

## Testing

### Test Setup
Created `RagEvaluatorTests` in `Agent.Evals` project:

```csharp
[TestClass]
[DoNotParallelize]
public class RagEvaluatorTests
{
    private static IHost? _host;
    private static readonly IReadOnlyList<RagTestCase> _testCases = InitializeTestCases();

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext testContext)
    {
        var builder = TestHelpers.BuildTestApp(out _);
        builder
            .RegisterDefaultServices()
            .ConfigureAgentMemory()
            .AddAgentMemoryPlugin();

        _host = builder.Build();
        await _host.StartAsync();
    }
}
```

### Test Case Structure
```csharp
public sealed record RagTestCase(
    string Query,
    string ResourceId,
    IReadOnlySet<string> RelevantTrajectoryTitles,
    string Description
);
```

### Example Test Case
```csharp
new RagTestCase(
    Query: "web app experiencing high CPU usage",
    ResourceId: "test-resource-id",
    RelevantTrajectoryTitles: new HashSet<string>
    {
        "Web App High CPU - Memory Leak Investigation",
        "App Service Performance Degradation - High CPU"
    },
    Description: "Query should retrieve CPU-related trajectories. Ground truth: 2 clearly relevant."
)
```

### Test Validation
The test validates:
1. Search returns expected trajectory titles
2. Results are properly formatted as markdown
3. RagEvaluator successfully evaluates retrieval quality
4. Per-document scores are calculated for each item
5. Expected trajectories receive RelevanceScore >= 4
6. Ranking quality score >= 3 (acceptable)

### Test Results (Example Run)

**Query**: "web app experiencing high CPU usage"

**Retrieved Items**: 3 trajectories

**Document Scores**:
1. **Web App High CPU - Memory Leak Investigation: 5/5**
   - Reasoning: "Directly matches a web app with high CPU and slow responses; provides concrete steps (heap dump analysis) and a plausible root cause (memory leak) for such symptoms."

2. **App Service Performance Degradation - High CPU: 5/5**
   - Reasoning: "Strongly relevant high-CPU scenario for an application service; offers diagnostic and remediation steps (optimize DB queries/indexes) commonly applicable to web apps."

3. **Container App CPU Spikes During Peak Hours: 4/5**
   - Reasoning: "Relevant to high CPU symptoms but focused on container autoscaling and peak-traffic spikes; useful if the web app is containerized or traffic-driven, slightly less direct otherwise."

**Ranking Quality Score**: 5/5

**Ranking Reasoning**: "The most relevant trajectories for a web app with high CPU are listed first: the memory leak investigation (web app specific) and app service high-CPU due to DB queries. The container autoscaling case, while relevant, is less directly applicable and is correctly placed third. Overall, the ranking surfaces the most useful information at the top."

**Calculated Metrics**:
- **Precision**: 3/3 = 1.0 (all documents scored >= 4)
- **Retrieval Score (Average)**: (5 + 5 + 4) / 3 = 4.667
- **Ranking Score**: 5/5
- **Recall**: null (to be implemented)

---

## Files Modified

### Core Implementation
1. **`src/Agent/Agent.Runtime/ThreadEvaluator/RagEvaluator.cs`**
   - Added `DocumentRelevanceScore` record (line 280-283)
   - Added `RetrievalEvaluationResult` record (line 285-288)
   - Updated `RagEvaluationResult` record with new fields (line 458-471)
   - Enhanced prompt with input structure documentation (line 305-327)
   - Added detailed output instructions for per-document scoring (line 392-413)
   - Updated `EvaluateSearchMemoryResult` to calculate metrics (line 219-248)
   - Updated error handling in catch blocks (line 250-268, 203-216)

### Test Implementation
2. **`src/Agent/Agent.Evals/Rag/RagEvaluatorTests.cs`**
   - Created new test class with DI setup
   - Implemented `PrecisionRecallSanityTest` data-driven test
   - Added `RagTestCase` record for test data structure
   - Replaced all `Console.WriteLine` with `TestContext.WriteLine` for CI/CD integration
   - Added detailed assertions for per-document scores and ranking quality

### Supporting Files
3. **`src/Agent/Agent.Plugins/Definitions/AgentMemoryPluginDefinition.cs`**
   - Referenced for understanding markdown output format
   - No modifications made

4. **`src/Agent/Agent.Evals/Rag/RagTestHelpers.cs`**
   - Used for test index setup (existing infrastructure)
   - No modifications made

---

## Key Design Decisions

### 1. Relevance Threshold: Score >= 4
**Decision**: Documents with RelevanceScore >= 4 (out of 5) are considered "relevant" for precision calculation.

**Rationale**:
- Aligns with 5-point scale definitions where 4 = "Relevant Context Ranked Middle" and 5 = "Highly Relevant, Well Ranked"
- Conservative threshold ensures only clearly relevant documents count as "relevant"
- Scores 1-3 represent irrelevant, partially relevant, or poorly-ranked content

### 2. Single LLM Call Architecture
**Decision**: Evaluate all documents in a single LLM call rather than separate calls per document.

**Rationale**:
- **Cost**: One API call vs N calls (significant savings at scale)
- **Consistency**: LLM maintains consistent scoring thresholds across all items
- **Context**: LLM can compare documents relatively
- **Performance**: Faster than sequential evaluation

**Trade-offs**:
- More complex prompt engineering required
- JSON parsing adds complexity
- Potential for larger context windows

### 3. Separate Ranking Score
**Decision**: Store ranking quality as separate field from retrieval score.

**Rationale**:
- **Distinct Concerns**: Retrieval score measures "how relevant are docs" vs ranking score measures "are relevant docs at top"
- **Actionable Insights**: Teams can identify if problem is retrieval quality or ranking quality
- **Evaluation Clarity**: Separating metrics makes evaluation results easier to interpret

### 4. Recall Placeholder
**Decision**: Add `Recall` field as nullable but set to `null` initially.

**Rationale**:
- Recall calculation requires expanded 2× K search (more complex implementation)
- Per-document scoring is valuable independently
- Incremental delivery allows testing precision metrics first
- Nullable type allows distinguishing "not calculated" from "calculated as 0"

### 5. TestContext vs Console Output
**Decision**: Use `TestContext.WriteLine` instead of `Console.WriteLine` in tests.

**Rationale**:
- TestContext output is captured in test result logs
- Better integration with CI/CD pipelines (Azure DevOps, GitHub Actions)
- Test explorers in Visual Studio/Rider display TestContext output
- Console output may not appear in all test runners

### 6. Explanation Field as JSON
**Decision**: Store `DocumentScores` as JSON-serialized string in `Explanation` field.

**Rationale**:
- Provides detailed per-document breakdown for debugging
- Can be parsed for downstream analytics
- Preserves all evaluation details without changing database schema
- Human-readable when viewing evaluation results

---

## Testing Observations

### LLM Evaluation Quality
The LLM (GPT-4) demonstrated excellent evaluation capabilities:

1. **Accurate Relevance Assessment**: Correctly identified all 3 CPU-related trajectories as relevant (scores 4-5)
2. **Nuanced Scoring**: Distinguished between highly relevant (5/5) and moderately relevant (4/5) items
3. **Clear Reasoning**: Provided specific explanations for each score
4. **Ranking Awareness**: Correctly assessed that most relevant items were ranked first
5. **Contextual Understanding**: Recognized "web app" vs "container app" distinction in relevance

### Example Thought Process
The LLM's reasoning demonstrated step-by-step analysis:
```
"Let's think step by step:
- The query indicates a web app experiencing high CPU usage. So the most relevant
  contexts will be past incidents that involve (a) web apps (or closely related
  app services) and (b) high CPU symptoms, ideally with resolution steps and
  root causes that could map to this situation.
- Review each trajectory:
  1) Web App High CPU - Memory Leak Investigation directly mentions a web app...
  2) App Service Performance Degradation also features high CPU...
  3) Container App CPU Spikes focuses on CPU spikes during peak traffic..."
```

This validates the prompt engineering approach and confirms LLMs can effectively assess retrieval quality.

---

## Performance Considerations

### Current Performance
- **Test Execution Time**: ~26-28 seconds for single test case
- **Breakdown**:
  - Test setup/teardown: ~2-3 seconds
  - Search execution: ~1-2 seconds
  - LLM evaluation call: ~20-22 seconds

### Optimization Opportunities (Future Work)
1. **Caching**: Cache LLM evaluations for identical retrieval results
2. **Batch Processing**: Evaluate multiple SearchMemory calls in parallel
3. **Prompt Optimization**: Reduce token count in prompts while maintaining quality
4. **Model Selection**: Consider faster models (e.g., GPT-3.5-turbo) for non-critical evaluations

### Scalability
Current design scales well:
- Single LLM call per SearchMemory evaluation (not per document)
- Parallel evaluation of multiple threads is possible
- No additional database queries beyond existing pattern

---

## Future Work

### Phase 2: Recall Implementation
Based on the original implementation plan:

**Approach**: Expanded 2× K search with no filtering thresholds
```
1. Run secondary search with K=2×original_K, no vector similarity thresholds
2. Evaluate relevance of all items in expanded set
3. Calculate recall = relevant_in_original / total_relevant_in_expanded_set
```

**Challenges**:
- Requires dependency injection of `IAgentMemoryClient` and `AgentMemorySettings`
- Need to handle different retrieval types (trajectories, documents, memories)
- Performance impact: doubles search operations per evaluation

**Tasks** (from original plan):
- Task 6: Inject IAgentMemoryClient and AgentMemorySettings
- Task 7: Add expanded unfiltered search method
- Task 8: Implement recall calculation
- Task 9: Create combined precision/recall calculation

### Additional Enhancements
1. **Ground Truth Validation**: Add test cases with explicit ground truth annotations
2. **Multiple Test Scenarios**:
   - Low precision queries (vague symptoms, many false positives)
   - High precision queries (specific problems, clear matches)
   - Edge cases (no results, all irrelevant, parsing failures)
3. **Performance Benchmarking**: Measure and optimize LLM evaluation latency
4. **Analytics Dashboard**: Visualize precision/recall trends over time
5. **A/B Testing**: Compare different prompt variations for evaluation quality

---

## Metrics and Success Criteria

### Completed Objectives
✅ **Per-Document Scoring**: LLM evaluates each retrieved item individually
✅ **Precision Calculation**: Fraction of retrieved items that are relevant
✅ **Ranking Quality Assessment**: Separate metric for ranking effectiveness
✅ **Structured Output**: JSON-formatted per-document scores with reasoning
✅ **Test Coverage**: Integration test validates end-to-end functionality
✅ **Error Handling**: Graceful degradation on failures
✅ **Documentation**: Clear explanations of design decisions and implementation

### Validation Results
- **Test Pass Rate**: 100% (1/1 tests passing)
- **LLM Evaluation Quality**: High (accurate relevance assessment, clear reasoning)
- **Performance**: Acceptable (~26s per evaluation, dominated by LLM call latency)
- **Code Quality**: Clean separation of concerns, well-documented

### Outstanding Work
⏳ **Recall Implementation**: Requires expanded search approach (Phase 2)
⏳ **Additional Test Cases**: Need more diverse scenarios
⏳ **Production Validation**: Run on real production threads

---

## Lessons Learned

### 1. Prompt Engineering is Critical
The quality of LLM evaluation directly depends on prompt clarity:
- Explicit input structure documentation helped LLM parse markdown correctly
- Detailed rating definitions with examples improved scoring consistency
- Requesting structured JSON output required careful formatting instructions

### 2. Single Call Architecture Works Well
Benefits exceeded expectations:
- API cost savings significant at scale
- LLM maintained consistent scoring thresholds across items
- Relative comparison between items improved evaluation quality

### 3. Test-Driven Development Accelerated Implementation
Writing the test first:
- Clarified requirements and edge cases early
- Provided immediate validation of changes
- Caught compilation errors in multiple locations

### 4. Incremental Delivery Reduces Risk
Implementing precision first (without recall):
- Allowed validation of per-document scoring architecture
- Provided value independently before tackling more complex recall calculation
- Enabled early feedback on evaluation quality

### 5. Console vs TestContext Matters
Using `TestContext.WriteLine`:
- Improved CI/CD integration significantly
- Made test results more discoverable in Azure DevOps
- Better developer experience in Visual Studio test explorer

---

## References

### Internal Documentation
- [RAG Evaluator Precision & Recall Implementation Plan](./rag-evaluator-precision-recall.md)
- Original task breakdown and requirements

### External References
- [Microsoft.Extensions.AI.Evaluation.Quality - RetrievalEvaluator.cs](https://github.com/dotnet/extensions/blob/43bde7f6e8b6e8f66af1dbf5690d9aa6ee6df809/src/Libraries/Microsoft.Extensions.AI.Evaluation.Quality/RetrievalEvaluator.cs#L133)
  - Source of prompt examples and rating definitions
  - Note: We implemented our own evaluator because the library hard-coded temperature settings incompatible with GPT-4

### Related Code
- `Agent.Plugins.Definitions.AgentMemoryPluginDefinition`: Retrieval markdown format
- `Agent.Evals.Rag.TrajectoryRetrievalEval`: Reference for test patterns
- `Agent.Evals.Evaluators.TrajectorySearchRelevanceEvaluator`: Related LLM evaluation patterns

---

## Acknowledgments

Implementation completed based on requirements and design discussions with the team. Special thanks to the Microsoft.Extensions.AI team for open-sourcing their evaluation prompts and rating definitions.

---

**Document Version**: 1.0
**Last Updated**: 2025-10-15
**Author**: Implementation team
**Status**: Completed - Phase 1 (Per-Document Scoring with Precision)
