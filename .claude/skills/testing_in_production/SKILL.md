---
name: testing_in_production
description: Use this skill when the user asks to test their changes in production, or asks to deploy a custom image to their production agent. This skill describes the process for building a custom agent image, and how to deploy that image to a production agent
---

# Testing in Production

Follow the below steps to build and deploy a custom agent image to a production agent for testing.

## 1. Gather Required information

You need to know the following information. This may already be present in your context. Otherwise,
ask the user to provide it:

1. The name of the ACR (azure container registry) instance to push the image.
2. The subscription, resource group, region, and name of the SRE Agent instance where the image will be deployed.

## 2. Build the Image

1. Publish the Agent.Web project:

   (all commands run from the root project directory)

```bash
dotnet publish src/Agent/Agent.Web/Agent.Web.csproj -o out/web/publish
```

2. Build and push the container image

   The image name is formatted as: `[acr-name].azurecr.io/sre-agent-test/agent:[tag]`
   where `[acr-name]` is the name of the container registry, and `[tag]` is a brief and descriptive tag
   used for the image

   Build the image from the publish output directory and then push

```bash
az acr login --name "[acr-name]"
docker build -t "[acr-name].azurecr.io/sre-agent-test/agent:[tag]" out/web/publish -f src/Deployment/Agent.Web/Dockerfile
docker push "[acr-name].azurecr.io/sre-agent-test/agent:[tag]"
```

## 3. Deploy the Image to Production

1. Fetch the ACR registry password. This command should output the registry username and
   configured passwords. If no username is returned, use the name of the ACR as the username.

```bash
az acr credential show -n "[acr-name]"
```

2. Form the request body using the image and registry details. Write this content to a file in the `TestPlayground` directory.

```json
{
    "location": "[azure region of agent]",
    "properties": {
       "firstPartyConfiguration": {
          "agentImageConfiguration": {
                "imageName":"[acr-name].azurecr.io/sre-agent-test/agent:[tag]",
                "registryUserName":"[acr-username]",
                "registryPassword":"[acr-password]"
            }
        }
    }
}
```

3. Patch the agent. Fill in the correct details into the url path and the correct filepath to the JSON file you wrote.

```bash
az rest -m PATCH --url /subscriptions/[sub-id]/resourceGroups/[rg]/providers/Microsoft.App/agents/[agent-name]?api-version=2025-05-01-preview --headers "Content-Type=application/json" --body "@path\to\body.json"
```

4. Monitor the update operation

   GET the agent resource until the provisioning state is in a terminal state (Succeeded/Failed)
