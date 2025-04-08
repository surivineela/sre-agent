// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Prometheus.Services;

using global::Prometheus;

// push metric to azure monitor workspace(Azure managed prometheus) using https://prometheus.io/docs/specs/prw/remote_write_spec/
public interface IRemoteWriteService
{
    // Useful when sending a batch of metrics, where the caller adds to the WriteRequest.Timeseries
    // Returns true if the request was successful, false otherwise
    Task<bool> RemoteWriteAsync(global::Prometheus.Protobuf.WriteRequest writeRequest);
}