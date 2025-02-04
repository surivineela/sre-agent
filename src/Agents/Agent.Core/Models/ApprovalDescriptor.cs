using Azure.ResourceManager.AppService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agents.Core.Models;

public sealed record ApprovalDescriptor(
    string ResourceId,
    string OperationName);
