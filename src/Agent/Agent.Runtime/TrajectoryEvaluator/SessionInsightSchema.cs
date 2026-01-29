// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Runtime.TrajectoryEvaluator;

/// <summary>
/// Root schema for session insights with structured output
/// </summary>
[Description("Session insights generated from Azure troubleshooting trajectory analysis")]
public class SessionInsightSchema
{
    [Description("Timeline showing the chronological investigation flow with milestones")]
    public required TimelineSection Timeline { get; set; }

    [Description("Evaluation of agent performance if evaluation scores are available")]
    public EvaluationSection? Evaluation { get; set; }

    [Description("Learnings derived from the troubleshooting session")]
    public required DerivedLearningSection DerivedLearning { get; set; }
}

[Description("Timeline section with chronological milestones")]
public class TimelineSection
{
    [Description("List of investigation milestones in chronological order, maximum 8 items")]
    public required List<TimelineMilestone> Milestones { get; set; }
}

[Description("A single milestone in the investigation timeline")]
public class TimelineMilestone
{
    [Description("Name of the milestone")]
    public required string Name { get; set; }

    [Description("Status of the milestone: Initial, Progress, Success, Issue, or Resolved")]
    public required string Status { get; set; }

    [Description("1-2 sentences describing what happened, what was discovered, or what decision was made in clear business language")]
    public required string Description { get; set; }
}

[Description("Agent performance evaluation section")]
public class EvaluationSection
{
    [Description("Score card showing resolved/intent met, autonomy, and adherence scores")]
    public required ScoreCard ScoreCard { get; set; }

    [Description("Qualitative evaluation of what went well and what didn't")]
    public required QualitativeEvaluation QualitativeEvaluation { get; set; }
}

[Description("Score card with individual metric assessments")]
public class ScoreCard
{
    [Description("Resolved / Intent Met score evaluation")]
    public required ScoreEvaluation ResolvedScore { get; set; }

    [Description("Autonomy score evaluation")]
    public required ScoreEvaluation AutonomyScore { get; set; }

    [Description("Adherence score evaluation")]
    public required ScoreEvaluation AdherenceScore { get; set; }
}

[Description("Evaluation of a specific performance score")]
public class ScoreEvaluation
{
    [Description("Raw score value (1-5)")]
    public required int Score { get; set; }

    [Description("Explanation of the score in context, without 'Analysis:' prefix")]
    public required string Explanation { get; set; }
}

[Description("Qualitative evaluation of session performance")]
public class QualitativeEvaluation
{
    [Description("What went well - successful actions and correct reasoning, maximum 3 items")]
    public required List<Achievement> WhatWentWell { get; set; }

    [Description("What didn't go well - deep analysis of failures and limitations, maximum 5 items")]
    public required List<FailureAnalysis> WhatDidntGoWell { get; set; }
}

[Description("Achievement or successful action")]
public class Achievement
{
    [Description("Name of the achievement")]
    public required string Name { get; set; }

    [Description("What the agent did correctly with technical specifics")]
    public required string Description { get; set; }
}

[Description("Failure analysis in compact format")]
public class FailureAnalysis
{
    [Description("Compact description covering: what happened, why, impact, and what should have been done. Keep concise and actionable.")]
    public required string Description { get; set; }
}

[Description("Derived learning section with actionable knowledge")]
public class DerivedLearningSection
{
    [Description("System design knowledge learned from the session, maximum 3 items")]
    public List<SystemDesignKnowledge>? SystemDesignKnowledge { get; set; }

    [Description("Investigation patterns that can be reused, maximum 2 items")]
    public List<InvestigationPattern>? InvestigationPatterns { get; set; }
}

[Description("System design knowledge item")]
public class SystemDesignKnowledge
{
    [Description("Azure service or component name")]
    public required string ServiceOrComponent { get; set; }

    [Description("Specific design principle, configuration pattern, or dependency discovered")]
    public required string Insight { get; set; }
}

[Description("Investigation pattern that can be reused")]
public class InvestigationPattern
{
    [Description("Compact description of the pattern, when to use it, and key steps. Keep concise.")]
    public required string Description { get; set; }
}
