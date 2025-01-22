# Local Settings
Update the appsettings.Development.json with the following:
"OperationalRuntimeSendEventEndpoint": "http://localhost:7253/runtime/webhooks/durabletask/instances/{0}/raiseEvent/{1}?code=<runtime_code>


You can get the runtime_code when you run ProcessMesasge http trigger in OperationalAgentRuntime.

