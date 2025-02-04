# HOW TO RUN LOCALLY

1. Add `appsettings.development.json` file

```json
{
  "Azure": {
    "OpenAI": {
      "Endpoint": "https://ruslany-openai.openai.azure.com",
      "ApiKey": "<Put your OPEN AI Key here>"
    },
    "TeamsEndpoint": "<Put the logicapps http endpoint here>",
    "ApprovalUrl": "https://localhost:5088/?action_name={0}"
  }
}
```

1. Add `local.settings.json` file to the project with the following content:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet",
    "AzureWebJobsSecretStorageType": "files"
  }
}
```

1. Run F5 on `OperationalAgentRuntimeSK` project
2. Send the message to Entrypoint, (e.g. `http://localhost:7123/api/DurableFunctionEntrypoint_HttpStart`). You can use [Visual Studio http file](../OperationalAgent.Tests.End2End/HttpRequests/PostChatMessage.http) to send the message.
3. Receive your reply in Teams
4. Continue #4-#5 to move the chat

If you want to clean up the chat history, and start a new conversation context delete this file --> `.\bin\Debug\net8.0\chathistory.txt`
