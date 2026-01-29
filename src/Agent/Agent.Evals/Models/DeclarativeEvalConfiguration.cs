// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Evals.Models;

public class DeclarativeEvalConfiguration
{
    [Required]
    public required EvalTestSuite TestSuite { get; set; }
}

public class EvalTestSuite
{
    [Required]
    public required string Name { get; set; }

    public string? Description { get; set; }

    [Required]
    public required EvalConfiguration Configuration { get; set; }

    [Required]
    public required List<string> Plugins { get; set; }

    [Required]
    public required List<EvalTestCase> TestCases { get; set; }

    public EvalEvaluation? Evaluation { get; set; }
}

public class EvalConfiguration
{
    public string? Timeout { get; set; }

    public string? Database { get; set; }

    public EvalToolReplay? ToolReplay { get; set; }
}

public class EvalToolReplay
{
    public string? LogDirectory { get; set; }

    public List<string>? SkipReplayFunctions { get; set; }

    public List<string>? FuzzyMatchFunctions { get; set; }
}

public class EvalTestCase
{
    [Required]
    public required string Name { get; set; }

    [Required]
    public required List<string> StartMessages { get; set; }

    public EvalEvaluation? Evaluation { get; set; }
}

public class EvalEvaluation
{
    public string? GroundedContext { get; set; }

    public string? ExampleResponse { get; set; }

    public EvalAssertions? Assertions { get; set; }

    public EvalAutoReply? AutoReply { get; set; }
}

public class EvalAssertions
{
    public EvalScoreAssertion? Equivalence { get; set; }

    public EvalScoreAssertion? Groundedness { get; set; }
}

public class EvalScoreAssertion
{
    public int? MinimumScore { get; set; }
}

public class EvalAutoReply
{
    public string? DefaultReply { get; set; }

    public string? AssessmentBreakCondition { get; set; }
}
