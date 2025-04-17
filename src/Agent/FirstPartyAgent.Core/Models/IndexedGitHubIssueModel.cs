using System.ComponentModel;
using Newtonsoft.Json;

namespace FirstPartyAgent.Core.Models;
public class IndexedGitHubIssueModel
{
    [Description("Unique identifier for the GitHub issue. Not to be confused with issueId within the repository")]
    public string id { get; set; }

    [Description("GitHub issue number in the repo")]
    public string issueId { get; set; }

    [Description("Link to GitHub issue")]
    public string issueUrl { get; set; }

    [Description("Owner of the GitHub repo")]
    public string owner { get; set; }

    [Description("Name of the GitHub repo")]
    public string repository { get; set; }

    [Description("Title of the GitHub issue")]
    public string title { get; set; }

    [Description("Body of the GitHub issue")]
    public string body { get; set; }

    [JsonIgnore]
    public List<IndexedGitHubIssueComment> commentsList { get; set; } = new List<IndexedGitHubIssueComment>();

    private string _commentsJson = string.Empty;

    [Description("JSON serialized list of comments on the GitHub issue")]
    public string comments
    {
        get
        {
            return _commentsJson;
        }
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _commentsJson = value;
                try
                {
                    commentsList = JsonConvert.DeserializeObject<List<IndexedGitHubIssueComment>>(value);
                }
                catch (JsonException)
                {
                    commentsList = new List<IndexedGitHubIssueComment>();
                }
            }
        }
    }

    [JsonIgnore]
    public List<string> labelsList { get; set; } = new List<string>();

    private string _labelsCsv = string.Empty;
    [Description("Labels on the GitHub issue")]
    public string labels
    {
        get
        {
            return _labelsCsv;
        }
        set
        {
            _labelsCsv = value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                labelsList = value.Split(',').Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()).ToList();
            }
        }
    }

    [Description("State of the GitHub issue")]
    public string state { get; set; }


    [Description("Descriptive summary of the GitHub issue")]
    public string descriptiveSummary { get; set; }

    [Description("Timestamp of when the GitHub issue was created")]
    public DateTime createdTimestamp { get; set; }

    [Description("Last updated timestamp of the GitHub issue")]
    public DateTime lastUpdatedTimestamp { get; set; }
}

public class IndexedGitHubIssueComment
{
    [Description("Last updated timestamp of the comment")]
    public DateTime commentTimestamp { get; set; }

    [Description("Body of the comment")]
    public string body { get; set; }
}

