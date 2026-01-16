# Azure DevOps MCP Tools

**Server Version**: 2.2.2
**Total Tools**: 77

## Core (3 tools)

1. **core_list_project_teams** - Retrieve a list of teams for the specified Azure DevOps project.
2. **core_list_projects** - Retrieve a list of projects in your Azure DevOps organization.
3. **core_get_identity_ids** - Retrieve Azure DevOps identity IDs for a provided search filter.

## Work (10 tools)

4. **work_list_team_iterations** - Retrieve a list of iterations for a specific team in a project.
5. **work_create_iterations** - Create new iterations in a specified Azure DevOps project.
6. **work_list_iterations** - List all iterations in a specified Azure DevOps project.
7. **work_assign_iterations** - Assign existing iterations to a specific team in a project.
8. **work_get_team_capacity** - Get the team capacity of a specific team and iteration in a project.
9. **work_update_team_capacity** - Update the team capacity of a team member for a specific iteration in a project.
10. **work_get_iteration_capacities** - Get an iteration's capacity for all teams in iteration and project.
11. **wit_list_backlogs** - Receive a list of backlogs for a given project and team.
12. **wit_list_backlog_work_items** - Retrieve a list of backlogs of for a given project, team, and backlog category
13. **wit_get_work_items_for_iteration** - Retrieve a list of work items for a specified iteration.

## Pipelines (11 tools)

14. **pipelines_get_build_definitions** - Retrieves a list of build definitions for a given project.
15. **pipelines_get_build_definition_revisions** - Retrieves a list of revisions for a specific build definition.
16. **pipelines_get_builds** - Retrieves a list of builds for a given project.
17. **pipelines_get_build_log** - Retrieves the logs for a specific build.
18. **pipelines_get_build_log_by_id** - Get a specific build log by log ID.
19. **pipelines_get_build_changes** - Get the changes associated with a specific build.
20. **pipelines_get_run** - Gets a run for a particular pipeline.
21. **pipelines_list_runs** - Gets top 10000 runs for a particular pipeline.
22. **pipelines_run_pipeline** - Starts a new run of a pipeline.
23. **pipelines_get_build_status** - Fetches the status of a specific build.
24. **pipelines_update_build_stage** - Updates the stage of a specific build.

## Repositories (18 tools)

25. **repo_create_pull_request** - Create a new pull request.
26. **repo_create_branch** - Create a new branch in the repository.
27. **repo_update_pull_request** - Update a Pull Request by ID with specified fields, including setting autocomplete with various completion options.
28. **repo_update_pull_request_reviewers** - Add or remove reviewers for an existing pull request.
29. **repo_list_repos_by_project** - Retrieve a list of repositories for a given project
30. **repo_list_pull_requests_by_repo_or_project** - Retrieve a list of pull requests for a given repository. Either repositoryId or project must be provided.
31. **repo_list_pull_request_threads** - Retrieve a list of comment threads for a pull request.
32. **repo_list_pull_request_thread_comments** - Retrieve a list of comments in a pull request thread.
33. **repo_list_branches_by_repo** - Retrieve a list of branches for a given repository.
34. **repo_list_my_branches_by_repo** - Retrieve a list of my branches for a given repository Id.
35. **repo_get_repo_by_name_or_id** - Get the repository by project and repository name or ID.
36. **repo_get_branch_by_name** - Get a branch by its name.
37. **repo_get_pull_request_by_id** - Get a pull request by its ID.
38. **repo_reply_to_comment** - Replies to a specific comment on a pull request.
39. **repo_create_pull_request_thread** - Creates a new comment thread on a pull request.
40. **repo_resolve_comment** - Resolves a specific comment thread on a pull request.
41. **repo_search_commits** - Search for commits in a repository with comprehensive filtering capabilities. Supports searching by description/comment text, time range, author, committer, specific commit IDs, and more. This is the unified tool for all commit search operations.
42. **repo_list_pull_requests_by_commits** - Lists pull requests by commit IDs to find which pull requests contain specific commits

## Work Items (WIT) (16 tools)

43. **wit_my_work_items** - Retrieve a list of work items relevent to the authenticated user.
44. **wit_get_work_items_batch_by_ids** - Retrieve list of work items by IDs in batch.
45. **wit_get_work_item** - Get a single work item by ID.
46. **wit_list_work_item_comments** - Retrieve list of comments for a work item by ID.
47. **wit_add_work_item_comment** - Add comment to a work item by ID.
48. **wit_add_child_work_items** - Create one or many child work items from a parent by work item type and parent id.
49. **wit_link_work_item_to_pull_request** - Link a single work item to an existing pull request.
50. **wit_update_work_item** - Update a work item by ID with specified fields.
51. **wit_get_work_item_type** - Get a specific work item type.
52. **wit_create_work_item** - Create a new work item in a specified project and work item type.
53. **wit_get_query** - Get a query by its ID or path.
54. **wit_get_query_results_by_id** - Retrieve the results of a work item query given the query ID.
55. **wit_update_work_items_batch** - Update work items in batch
56. **wit_work_items_link** - Link work items together in batch.
57. **wit_work_item_unlink** - Remove one or many links from a single work item
58. **wit_add_artifact_link** - Add artifact links (repository, branch, commit, builds) to work items. You can either provide the full vstfs URI or the individual components to build it automatically.

## Test Plans (8 tools)

59. **testplan_list_test_plans** - Retrieve a paginated list of test plans from an Azure DevOps project. Allows filtering for active plans and toggling detailed information.
60. **testplan_create_test_plan** - Creates a new test plan in the project.
61. **testplan_create_test_suite** - Creates a new test suite in a test plan.
62. **testplan_add_test_cases_to_suite** - Adds existing test cases to a test suite.
63. **testplan_create_test_case** - Creates a new test case work item.
64. **testplan_update_test_case_steps** - Update an existing test case work item.
65. **testplan_list_test_cases** - Gets a list of test cases in the test plan.
66. **testplan_show_test_results_from_build_id** - Gets a list of test results for a given project and build ID.

## Search (3 tools)

67. **search_code** - Search Azure DevOps Repositories for a given search text
68. **search_wiki** - Search Azure DevOps Wiki for a given search text
69. **search_workitem** - Get Azure DevOps Work Item search results for a given search text

## Wiki (6 tools)

70. **wiki_get_wiki** - Get the wiki by wikiIdentifier
71. **wiki_list_wikis** - Retrieve a list of wikis for an organization or project.
72. **wiki_list_pages** - Retrieve a list of wiki pages for a specific wiki and project.
73. **wiki_get_page** - Retrieve wiki page metadata by path. This tool does not return page content.
74. **wiki_get_page_content** - Retrieve wiki page content. Provide either a 'url' parameter OR the combination of 'wikiIdentifier' and 'project' parameters.
75. **wiki_create_or_update_page** - Create or update a wiki page with content.

## Advanced Security (2 tools)

76. **advsec_get_alerts** - Retrieve Advanced Security alerts for a repository.
77. **advsec_get_alert_details** - Get detailed information about a specific Advanced Security alert.

## Configuration

The Azure DevOps MCP is configured in `appsettings.Development.json`:

```json
{
  "Name": "azure-devops-mcp",
  "DataConnectorType": "Mcp",
  "DataSource": "placeholder",
  "ExtendedProperties": {
    "Type": "Stdio",
    "Command": "npx",
    "Args": ["-y", "@azure-devops/mcp@latest", "msazure", "-d", "all"]
  }
}
```

Default organization: `msazure`
Default domains: `all`
