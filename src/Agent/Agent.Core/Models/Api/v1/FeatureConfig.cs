// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;


// stored in doc
public sealed record FeatureConfig(
    bool? AutoHandoffEnabled,
    bool? RegionalSearchEnabled,
    bool? AgentMemoryEnabled,
    bool? TrajectoryRetrievalEnabled,
    bool? HandoffReasoningEnabled)
{
    public FeatureConfigModel ToModel()
    {
        return new(
            AutoHandoffEnabled: AutoHandoffEnabled ?? FeatureConfigModel.Default.AutoHandoffEnabled,
            RegionalSearchEnabled: RegionalSearchEnabled ?? FeatureConfigModel.Default.RegionalSearchEnabled,
            AgentMemoryEnabled: AgentMemoryEnabled ?? FeatureConfigModel.Default.AgentMemoryEnabled,
            TrajectoryRetrievalEnabled: TrajectoryRetrievalEnabled ?? FeatureConfigModel.Default.TrajectoryRetrievalEnabled,
            HandoffReasoningEnabled: HandoffReasoningEnabled ?? FeatureConfigModel.Default.HandoffReasoningEnabled);
    }
}


// used at runtime - no nullables allowed. everything should have defined value
public sealed record FeatureConfigModel(
    bool AutoHandoffEnabled,
    bool RegionalSearchEnabled,
    bool AgentMemoryEnabled,
    bool TrajectoryRetrievalEnabled,
    bool HandoffReasoningEnabled)
{
    public static FeatureConfigModel Default { get; } = new(
        AutoHandoffEnabled: false,
        RegionalSearchEnabled: false,
        AgentMemoryEnabled: false,
        TrajectoryRetrievalEnabled: false,
        HandoffReasoningEnabled: false);

    public FeatureConfig ToDocument()
    {
        return new(
            AutoHandoffEnabled: AutoHandoffEnabled,
            RegionalSearchEnabled: RegionalSearchEnabled,
            AgentMemoryEnabled: AgentMemoryEnabled,
            TrajectoryRetrievalEnabled: TrajectoryRetrievalEnabled,
            HandoffReasoningEnabled: HandoffReasoningEnabled);
    }
}
