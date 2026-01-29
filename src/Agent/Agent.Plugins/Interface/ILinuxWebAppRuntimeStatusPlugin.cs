// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Plugins.Interface;

/// <summary>
/// Plugin for getting the site runtime status of Linux Web Apps
/// </summary>
public interface ILinuxWebAppRuntimeStatusPlugin
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the linux web app</param>
    /// <returns>Site runtime status</returns>
    Task<string> GetLinuxWebAppRuntimeStatus(string resourceId);
}
