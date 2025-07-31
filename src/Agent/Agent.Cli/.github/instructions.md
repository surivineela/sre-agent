## Instructions for working on the Agent.Cli project

### Housekeeping rules:

- Read the features.md file for checking out the list of features to work upon.
- Refer to the feature status and progress in the features.md file.
- features.md is the source of truth for features and their status.
- Always create feature specific branches for new features where branch doesn't exist.
- Update the features.md file with the new feature, its branch name, and status as "Not Started", "Ongoing" or "Completed".
- Update the DetailedStatus field in the features.md file with a brief description of what has been implemented and what remains.
- Use the following format:
  ```markdown
  ## Feature Name
  - Branch: <user_alias>/branch-name
  - Status: Not Started / Ongoing / Completed
  - Description: Brief description of the feature.
  - DetailedStatus: What has been implemented, what remains to be done.
  ```
- Ensure proper documentation is added in CodeReadme.md and Readme.md files upon adding new features or updating the syntax/definition of existing features.
- Ensure that the tests in srectl_tests.bat are updated to cover the new features or changes made.
- Ensure that the tests are passing before committing any changes.
- Use appropriate commit messages that reflect the changes made, especially when adding new features or fixing bugs.
- Once committed, create a pull request with a clear description of the changes made, linking to the feature in features.md.

### Coding rules:

- Follow the existing coding standards and conventions used in the project. Read CodeReadme.md file for specific details.
- Ensure that you leverage the existing models and definition from the Agent.Framework/Agent.Plugins/Agent.Runtime/Agent.Core projects for agent/tool/connector etc. definitions.
- Ensure consistency in naming conventions, and command syntax.
- When adding new features to an existing command, **YOU MUST ENSURE BACKWARD COMPATIBILITY**. This means that existing commands should still work as expected without breaking changes.
- Always ask the user for confirmation before making any changes that could affect the BACKWARD COMPATIBILITY of existing agents or tools.

## Instructions for working on other projects like Agent.Framework, Agent.Core, Agent.Plugins, Agent.Runtime

- Follow the existing coding standards and conventions used in the project.
- Ensure that the Agent.Framework project remains domain agnostic and all specializations are done in other projects.