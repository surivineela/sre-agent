### Create a new thread
POST https://{{ApiHostname}}/api/v1/threads
Content-Type: application/json
{
  "startMessage": {
    "text": "Hello, please start TlsBestPractice Agent to apply the TLS best practices for all my apps, resource ID: /xxxs1, location: West US, TLS1.2",
    "userId": "ruslany",
    "displayName": "Ruslan Y"
  }
}
HTTP 201
[Captures]
threadId: jsonpath "$.id"

### Get threads
GET https://{{ApiHostname}}/api/v1/threads
Content-Type: application/json
HTTP 200
[Asserts]
jsonpath "$.value[*].id" includes "{{threadId}}"
 
### Get a thread
GET https://{{ApiHostname}}/api/v1/threads/{{threadId}}
Content-Type: application/json
HTTP 200

### Get thread messages
GET https://{{ApiHostname}}/api/v1/threads/{{threadId}}/messages
Content-Type: application/json
HTTP 200
[Captures]
messageId: jsonpath "$.value[0].id"

### Get a thread message
GET https://{{ApiHostname}}/api/v1/threads/{{threadId}}/messages/{{messageId}}
Content-Type: application/json
HTTP 200 

### Add a message to a thread
POST https://{{ApiHostname}}/api/v1/threads/{{threadId}}/messages
Content-Type: application/json
{
  "UserId": "ruslany",
  "DisplayName": "Ruslan Y",
  "text": "I confirm all message are accurate, please proceed with the TLS best practices for my app xx"
}
HTTP 201

### Add a message to a thread
POST https://{{ApiHostname}}/api/v1/threads/{{threadId}}/messages
Content-Type: application/json
{
  "UserId": "ruslany",
  "DisplayName": "Ruslan Y",
  "text": "Please report the status of the TLS best practices workflow result"
}
HTTP 201

### Get thread actions
GET https://{{ApiHostname}}/api/v1/threads/{{threadId}}/actions
Content-Type: application/json
HTTP 200

### Delete a thread
DELETE https://{{ApiHostname}}/api/v1/threads/{{threadId}}
HTTP 204

### LIST ALL THREADS and check if the thread is deleted
GET https://{{ApiHostname}}/api/v1/threads
Content-Type: application/json
HTTP 200
[Asserts]
jsonpath "$.value[*].id" not includes "{{threadId}}"