// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Models
{
    public class Resource
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public List<Resource> ChildResources { get; set; } = new List<Resource>();

        public IDictionary<string, object> GetProperties()
        {
            return new Dictionary<string, object>
            {
                { "Id", Id },
                { "Name", Name },
                { "Type", Type },
            };
        }
    }
}

