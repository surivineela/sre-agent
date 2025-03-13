using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Data.DataModels
{
    public interface ICosmosDocument
    {
        string Id { get; }
        string DocumentType { get; }
        string PartitionKey { get; } // Defines the partition key value
    }
}
