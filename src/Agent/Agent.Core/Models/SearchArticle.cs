using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Search.Documents.Indexes;

namespace Agent.Core.Models;
public class SearchArticle
{
    [SimpleField(IsKey = true)]
    public string Id { get; set; } = string.Empty;

    [SearchableField]
    public string Content { get; set; } = string.Empty;

    [SearchableField]
    public string Title { get; set; } = string.Empty;

    [SimpleField]
    public string Url { get; set; } = string.Empty;
}
