# Teams Resources for SRE Agent

This directory contains resources needed to deploy the SRE Agent as a Microsoft Teams app.

## Understanding App IDs

When deploying this Teams bot, you need to be aware of two different IDs:

### TEAMS_APP_ID

The TEAMS_APP_ID is the unique identifier for your app in the Microsoft Teams ecosystem. This ID is generated when you create a new app in the [Teams Developer Portal](https://dev.teams.microsoft.com/home).

To get a TEAMS_APP_ID:
1. Go to [Teams Developer Portal](https://dev.teams.microsoft.com/home)
2. Create a new app or select an existing one
3. The app ID is shown in your app's dashboard and in the URL (format: https://dev.teams.microsoft.com/apps/{app-id})

### BOT_ID

The BOT_ID is the Microsoft Azure Active Directory (AAD) App ID that binds to your bot service. This is the identity of your bot in the Microsoft identity platform.

To get a BOT_ID:
1. Register an application in the [Azure portal](https://portal.azure.com/#blade/Microsoft_AAD_RegisteredApps/ApplicationsListBlade)
2. The Application (client) ID is your BOT_ID
3. This ID must also be configured in your Bot Framework registration

## Using the build script

The `build.sh` script prepares your manifest.json file by replacing placeholders with actual values.

### Prerequisites

- Bash shell environment
- The TEAMS_APP_ID and BOT_ID environment variables must be set

### Running the script

1. Set your environment variables:
   ```bash
   export TEAMS_APP_ID="your-teams-app-id"
   export BOT_ID="your-bot-id"
   ```
2. Run the script:
   ```bash
   ./build.sh
   ```
3. The script will update the manifest.json file with your app's specific IDs

## Deploying your Teams app

After running the build script, you can package and deploy your Teams app using the updated manifest.json file and your app's icons.

1. Zip the contents of the appPackage directory
2. Upload the package to Teams or distribute it according to your deployment strategy
