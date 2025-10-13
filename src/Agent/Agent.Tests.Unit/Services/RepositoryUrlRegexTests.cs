// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;
using Agent.Plugins.Services;
using Shouldly;

namespace Agent.Tests.Unit.Services;

/// <summary>
/// Tests for repository URL regex patterns used for GitHub and Azure DevOps validation.
/// </summary>
public class RepositoryUrlRegexTests
{
    #region GitHub Repository URL Tests

    public static TheoryData<string, bool, string> GitHubUrlTestData =>
        new()
        {
            // Valid public GitHub URLs
            { "https://github.com/microsoft/vscode", true, "Standard GitHub repo" },
            { "https://github.com/facebook/react", true, "GitHub repo with hyphen in owner" },
            { "https://github.com/dotnet/aspnetcore", true, "GitHub repo with multiple parts" },
            { "https://github.com/user/repo.git", true, "GitHub repo with .git suffix" },
            { "https://github.com/user_name/repo-name", true, "GitHub repo with underscore and hyphen" },
            { "https://github.com/org.name/repo.name", true, "GitHub repo with dots" },
            
            // Valid enterprise GitHub URLs
            { "https://github.tools.digital.engie.com/EngieOneCloudAzure/Cluster_Provisioning", true, "Enterprise GitHub with subdomain" },
            { "https://github.enterprise.company.com/org/repo", true, "Enterprise GitHub URL" },
            { "https://github.company.local/team/project", true, "Enterprise GitHub with .local TLD" },
            { "https://github.internal.corp/user/repo.git", true, "Enterprise GitHub with .git" },
            
            // Invalid GitHub URLs
            { "http://github.com/user/repo", false, "HTTP instead of HTTPS" },
            { "https://github.com/user", false, "Missing repository name" },
            { "https://github.com/user/repo/extra", false, "Extra path segments" },
            { "https://gitlab.com/user/repo", false, "GitLab URL" },
            { "https://bitbucket.org/user/repo", false, "Bitbucket URL" },
            { "https://github.com/user/repo with spaces", false, "URL with spaces" },
            { "github.com/user/repo", false, "Missing protocol" },
            { "https://github.com/", false, "Missing owner and repo" },
            { "https://github.com/user/", false, "Missing repo name with trailing slash" },
            { "", false, "Empty string" },
        };

    [Theory]
    [MemberData(nameof(GitHubUrlTestData))]
    public void GithubRepoRegex_ValidatesUrlsCorrectly(string url, bool expectedMatch, string testCaseName)
    {
        // Arrange
        var regex = new Regex(GraphService.GithubRepoRegexPattern, RegexOptions.IgnoreCase);
        _ = testCaseName; // Used for test case identification

        // Act
        bool actualMatch = regex.IsMatch(url);

        // Assert
        actualMatch.ShouldBe(expectedMatch, $"URL: {url}");
    }

    [Fact]
    public void GithubRepoRegex_ExtractsCorrectComponents()
    {
        // Arrange
        var regex = new Regex(GraphService.GithubRepoRegexPattern, RegexOptions.IgnoreCase);
        var url = "https://github.com/microsoft/vscode";

        // Act
        var match = regex.Match(url);

        // Assert
        match.Success.ShouldBeTrue();
    }

    [Fact]
    public void GithubRepoRegex_HandlesEnterpriseGitHubUrl()
    {
        // Arrange
        var regex = new Regex(GraphService.GithubRepoRegexPattern, RegexOptions.IgnoreCase);
        var url = "https://github.tools.digital.engie.com/EngieOneCloudAzure/Cluster_Provisioning";

        // Act
        var match = regex.Match(url);

        // Assert
        match.Success.ShouldBeTrue();
    }

    #endregion

    #region Azure DevOps Repository URL Tests

    public static TheoryData<string, bool, string> AzureDevOpsUrlTestData =>
        new()
        {
            // Valid dev.azure.com URLs
            { "https://dev.azure.com/myorg/myproject/_git/myrepo", true, "Standard Azure DevOps URL" },
            { "https://dev.azure.com/microsoft/vscode/_git/vscode", true, "Real-world example" },
            { "https://dev.azure.com/org-name/project-name/_git/repo-name", true, "URL with hyphens" },
            { "https://dev.azure.com/org123/project456/_git/repo789", true, "URL with numbers" },
            { "https://dev.azure.com/org.name/proj.name/_git/repo.name", true, "URL with dots" },
            
            // Valid visualstudio.com URLs
            { "https://myorg.visualstudio.com/myproject/_git/myrepo", true, "Legacy Visual Studio URL" },
            { "https://contoso.visualstudio.com/DefaultCollection/_git/MyRepo", true, "Legacy with DefaultCollection" },
            { "https://fabrikam.visualstudio.com/project/_git/repository", true, "Legacy fabrikam example" },
            
            // Valid URLs with encoded characters
            { "https://dev.azure.com/org%20name/project%20name/_git/repo%20name", true, "URL with encoded spaces" },
            { "https://dev.azure.com/org/My%20Project/_git/My%20Repo", true, "Partial encoding" },
            
            // Valid URLs with authentication (username)
            { "https://user@dev.azure.com/org/project/_git/repo", true, "URL with username" },
            { "https://user.name@dev.azure.com/org/project/_git/repo", true, "URL with dotted username" },
            
            // Invalid Azure DevOps URLs
            { "http://dev.azure.com/org/project/_git/repo", false, "HTTP instead of HTTPS" },
            { "https://dev.azure.com/org/project", false, "Missing _git and repo" },
            { "https://dev.azure.com/org/_git/repo", false, "Missing project" },
            { "https://dev.azure.com/org/project/_git", false, "Missing repo name" },
            { "https://dev.azure.com/org/project/_git/repo/extra", false, "Extra path segments" },
            { "https://github.com/org/repo", false, "GitHub URL" },
            { "https://gitlab.com/org/repo", false, "GitLab URL" },
            { "dev.azure.com/org/project/_git/repo", false, "Missing protocol" },
            { "https://dev.azure.com", false, "Only domain" },
            { "", false, "Empty string" },
        };

    [Theory]
    [MemberData(nameof(AzureDevOpsUrlTestData))]
    public void AzDoRepoRegex_ValidatesUrlsCorrectly(string url, bool expectedMatch, string testCaseName)
    {
        // Arrange
        var regex = new Regex(GraphService.AzDoRepoRegexPattern, RegexOptions.IgnoreCase);
        _ = testCaseName; // Used for test case identification

        // Act
        bool actualMatch = regex.IsMatch(url);

        // Assert
        actualMatch.ShouldBe(expectedMatch, $"URL: {url}");
    }

    [Fact]
    public void AzDoRepoRegex_ExtractsCorrectComponentsFromDevAzureUrl()
    {
        // Arrange
        var regex = new Regex(GraphService.AzDoRepoRegexPattern, RegexOptions.IgnoreCase);
        var url = "https://dev.azure.com/myorg/myproject/_git/myrepo";

        // Act
        var match = regex.Match(url);

        // Assert
        match.Success.ShouldBeTrue();
        match.Groups["organization"].Value.ShouldBe("myorg");
        match.Groups["project"].Value.ShouldBe("myproject");
        match.Groups["repo"].Value.ShouldBe("myrepo");
    }

    [Fact]
    public void AzDoRepoRegex_ExtractsCorrectComponentsFromVisualStudioUrl()
    {
        // Arrange
        var regex = new Regex(GraphService.AzDoRepoRegexPattern, RegexOptions.IgnoreCase);
        var url = "https://fabrikam.visualstudio.com/DefaultCollection/_git/MyRepo";

        // Act
        var match = regex.Match(url);

        // Assert
        match.Success.ShouldBeTrue();
        match.Groups["organization"].Value.ShouldBe("fabrikam");
        match.Groups["project"].Value.ShouldBe("DefaultCollection");
        match.Groups["repo"].Value.ShouldBe("MyRepo");
    }

    [Fact]
    public void AzDoRepoRegex_HandlesEncodedCharacters()
    {
        // Arrange
        var regex = new Regex(GraphService.AzDoRepoRegexPattern, RegexOptions.IgnoreCase);
        var url = "https://dev.azure.com/org/My%20Project/_git/My%20Repo";

        // Act
        var match = regex.Match(url);

        // Assert
        match.Success.ShouldBeTrue();
        match.Groups["organization"].Value.ShouldBe("org");
        match.Groups["project"].Value.ShouldBe("My%20Project");
        match.Groups["repo"].Value.ShouldBe("My%20Repo");
    }

    [Fact]
    public void AzDoRepoRegex_HandlesUsernameInUrl()
    {
        // Arrange
        var regex = new Regex(GraphService.AzDoRepoRegexPattern, RegexOptions.IgnoreCase);
        var url = "https://user@dev.azure.com/myorg/myproject/_git/myrepo";

        // Act
        var match = regex.Match(url);

        // Assert
        match.Success.ShouldBeTrue();
        match.Groups["organization"].Value.ShouldBe("myorg");
        match.Groups["project"].Value.ShouldBe("myproject");
        match.Groups["repo"].Value.ShouldBe("myrepo");
    }

    #endregion

    #region Edge Cases and Security Tests

    [Theory]
    [InlineData("https://github.com/../../etc/passwd", false, "Path traversal attempt")]
    [InlineData("https://github.com/user/repo;rm -rf /", false, "Command injection attempt")]
    [InlineData("https://github.com/user/repo' OR '1'='1", false, "SQL injection pattern")]
    [InlineData("https://github.com/user/repo<script>alert('xss')</script>", false, "XSS attempt")]
    public void GithubRepoRegex_RejectsSecurityThreats(string url, bool expectedMatch, string testCaseName)
    {
        // Arrange
        var regex = new Regex(GraphService.GithubRepoRegexPattern, RegexOptions.IgnoreCase);
        _ = testCaseName;

        // Act
        bool actualMatch = regex.IsMatch(url);

        // Assert
        actualMatch.ShouldBe(expectedMatch);
    }

    [Theory]
    [InlineData("https://dev.azure.com/../../etc/passwd/_git/repo", false, "Path traversal attempt")]
    [InlineData("https://dev.azure.com/org/proj/_git/repo;rm -rf /", false, "Command injection attempt")]
    [InlineData("https://dev.azure.com/org/proj/_git/repo' OR '1'='1", false, "SQL injection pattern")]
    [InlineData("https://dev.azure.com/org/proj/_git/repo<script>alert('xss')</script>", false, "XSS attempt")]
    public void AzDoRepoRegex_RejectsSecurityThreats(string url, bool expectedMatch, string testCaseName)
    {
        // Arrange
        var regex = new Regex(GraphService.AzDoRepoRegexPattern, RegexOptions.IgnoreCase);
        _ = testCaseName;

        // Act
        bool actualMatch = regex.IsMatch(url);

        // Assert
        actualMatch.ShouldBe(expectedMatch);
    }

    #endregion
}
