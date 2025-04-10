using System.ComponentModel;

namespace Agent.Core.Helpers;
/// <summary>
/// Passed around to various agents, used to describe the state of features of Azure resources.
/// Such as storage keys disabled/enabled
/// </summary>
[Description("Describes whether a feature is enabled or disabled")]
public enum FeatureState
{
    [Description("The feature should be enabled.")]
    Enabled,
    [Description("The feature should be disabled.")]
    Disabled
}
