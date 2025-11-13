// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using Agent.Core.Helpers;

namespace Agent.Tests.Unit.Helpers;

public class TableFormatterTests
{
  [Fact]
  public void MapsAppInsightsQueryJsonToTsv()
  {
    var json =
        """
            {
              "tables": [
                {
                  "name": "PrimaryResult",
                  "columns": [
                    {
                      "name": "timestamp",
                      "type": "datetime"
                    },
                    {
                      "name": "count_",
                      "type": "long"
                    }
                  ],
                  "rows": [
                    [
                      "2018-02-02T05:00:00Z",
                      "255"
                    ],
                    [
                      "2018-02-01T17:00:00Z",
                      "148"
                    ],
                    [
                      "2018-02-01T18:00:00Z",
                      "453"
                    ]
                  ]
                }
              ]
            }
            """;
    var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

    var tsv = TableFormatter.DataTableResponseStreamToTsv(stream);

    Assert.Equal(
        """
            Result table name: PrimaryResult
            Column count: 2
            Row count: 3
            timestamp (datetime)	count_ (long)
            2018-02-02T05:00:00Z	255
            2018-02-01T17:00:00Z	148
            2018-02-01T18:00:00Z	453

            """.Replace("\r\n", Environment.NewLine),
        tsv);
  }
}
