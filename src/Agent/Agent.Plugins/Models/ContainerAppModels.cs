// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient.Nodes;

namespace Agent.Plugins.Models;

public sealed record ContainerAppDescriptor(
    string ResourceId,
    string Name,
    string Location,
    string WorkloadProfile,
    string State,
    string ResourceGroup,
    string EnvironmentId,
    Container[] Containers,
    Container[] InitContainers,
    ContainerAppConfigurations? Configurations = null,
    IReadOnlyList<RevisionInfo>? Revisions = null,
    AppHealthInfo? AppHealthInfo = null);

public sealed record Container(
    string Name,
    string Image,
    string Cpu,
    string Memory);

public sealed record RevisionInfo(
    string RevisionName,
    bool IsActive,
    int TrafficWeight,
    string? CreatedOn = null,
    string? LastActiveOn = null,
    string? Fqdn = null,
    string? Template = null,
    int? Replicas = null,
    string? Labels = null,
    string? ProvisioningError = null,
    string? HealthState = null,
    string? ProvisioningState = null,
    string? RunningState = null);

public sealed record RequestCountTimeSeriesData(
    DateTime TimeStamp,
    double TotalRequestCount);

public sealed record CpuUsageTimeSeriesData(
    DateTime TimeStamp,
    double Percent);

public sealed record MemoryUsageTimeSeriesData(
    DateTime TimeStamp,
    double Percent);

public sealed record ContainerAppConfigurations(
    string RevisionMode,
    IngressConfiguration Ingress,
    Registry[] Registries);

public sealed record IngressConfiguration(
    bool IsExternal,
    int TargetPort,
    string Transport,
    string[] Hostnames,
    TrafficConfiguration[] Traffic);

public sealed record TrafficConfiguration(
    string RevisionName,
    int Weight,
    string Label,
    bool LatestRevision);

public sealed record Registry(
    string Server,
    string Username,
    string PasswordSecretRef,
    string Identity);
