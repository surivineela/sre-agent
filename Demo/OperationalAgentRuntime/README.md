# Local Settings
Update the local.settings.json with the following:

```json
{
    "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "Environment": "Development",
    "OpenAIEndpoint": "<your_endpoint>/openai/deployments/gpt-4o/chat/completions?api-version=2024-02-15-preview",
    "OpenAIAPI_KEY": "<your_openai_key>",
    "TeamsPostMessageEndpoint": "<logic app http trigger endpoint>",
    "ApprovalUrl": "https://localhost:7268/?action_name={0}"
  }
}
```


## Logic App Teams Integration
To get the message flow to teams chat, a logic app (standard, preferably) is needed.

1. Add a new workflow
2. Add a trigger : When a HTTP request is received
3. Update the Method as POST and Request body json as 
```json
{
    "type": "object",
    "properties": {
        "message": {
            "type": "object",
            "properties": {
                "content": {
                    "type": "string"
                },
                "image": {
                    "type": "string"
                }
            }
        }
    }
}
```

4. Add a action as 'Condition'
5. In the condition, add a Dynamic Content : empty(triggerBody()?['message']?['image']), and 'is not equal to' 'true'
6. In 'true', add a action 'Post card in a chat or channel'. Post as 'Flow Bot', Post in 'Group Chat' and select the group chat.
7. In Adaptive card, paste the following:
```json
{  
  "type": "AdaptiveCard",  
  "body": [  
    {  
      "type": "TextBlock",  
      "text": "@{triggerBody()?['message']?['content']}",
      "wrap": true
    },  
    {  
      "type": "Image",  
      "url": "@{triggerBody()?['message']?['image']}"
    }  
  ],  
  "actions": [  
    {  
      "type": "Action.OpenUrl",  
      "title": "Open Azure Portal",  
      "url": "https://www.example.com"  
    }  
  ],  
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",  
  "version": "1.2"  
}
```

8. In 'false', add a action 'Post message in a chat or channel', and Message as '@{triggerBody()?['message']?['content']}'
9. Save the workflow and get the HTTP endpoint from the trigger.


## To send a mesage
POST http://localhost:7253/api/ProcessMessageFunction_HttpStart
Request Body : 
{
  "content": "Hi, please start monitoring subscription your_sub_name"
}