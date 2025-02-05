// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Models;

public class TeamsMessage
{

    public string Content { get; set; }
    public string Image { get; set; }

    public TeamsMessage(string content, string image = null)
    {
        this.Content = content;
        this.Image = image ?? string.Empty;
    }
}

