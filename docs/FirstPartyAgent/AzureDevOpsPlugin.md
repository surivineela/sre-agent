# Azure DevOps Plugin

## Main Functions

### ListFilesAsync

Lists all files in a repository path, up to a maximum of `topN` files.

**Input Parameters:**
- `pathInRepo` (string): Path in the repository to list files from.
- `topN` (int): Maximum number of files to list.

---

### ReadFileAsync

Reads the content of a file at a given path and branch.

**Input Parameters:**
- `filePath` (string): Path to the file in the repository.
- `branch` (string): Branch name.

---

### GetCommitHistoryAsync

Gets the commit history of the repository up to `topN` commits.

**Input Parameters:**
- `topN` (int): Maximum number of commits to retrieve.

---

### CreateBranchAsync

Creates a new branch in the repository from the main branch.

**Input Parameters:**
- `newBranchName` (string): Name of the new branch to create.

---

### CreateCommitAsync

Creates a commit in the repository for a given branch, file, and commit message.

**Input Parameters:**
- `branchName` (string): Name of the branch to commit to.
- `filePath` (string): Path to the file to commit.
- `fileContent` (string): Content of the file.
- `commitMessage` (string): Commit message.

---

### CreatePullRequestAsync

Creates a pull request from a source branch to a target branch with a title and optional description.

**Input Parameters:**
- `sourceBranchName` (string): Name of the source branch.
- `targetBranchName` (string): Name of the target branch.
- `title` (string): Title of the pull request.
- `description` (string): Description of the pull request.

---

### AbandonPullRequestAsync

Abandons (closes) a pull request given its pull request ID.

**Input Parameters:**
- `pullRequestId` (int): ID of the pull request to abandon.

---

### SearchCodeAsync

Searches code in the repository using a search string and returns up to `topN` results.

**Input Parameters:**
- `searchText` (string): Search string.
- `topN` (int): Maximum number of results to return.

---

## Usage Notes

- All major actions are logged and can be integrated with Teams notifications and session messages.
- All links returned to the user should be user-friendly and not API links.
- The plugin is designed to be used in automated agents and can be extended for additional Azure DevOps operations.

---