### Create a new thread
POST https://{{ApiHostname}}/api/v1/threads
Content-Type: application/json
{
  "startMessage": {
    "text": "Hello, this is a new thread",
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
  "text": "Hello, can you tell me which subscriptions I have access to?"
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