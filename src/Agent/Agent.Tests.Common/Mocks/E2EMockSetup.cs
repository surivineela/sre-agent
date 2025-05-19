using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace Agent.Tests.Common.Mocks;
public class E2EMockSetup
{
    public BasicMockSetup BasicMocks { get; set; }
    public IAuthenticationService AuthenticationService { get; set; } = Mock.Of<IAuthenticationService>();
    public string GraphName { get; set; }

    public E2EMockSetup(DateTimeOffset mockedCurrentDateTime, string graphName, ILogger? logger)
    {
        this.BasicMocks = new BasicMockSetup(mockedCurrentDateTime, logger);
        this.GraphName = graphName;
    }
}
