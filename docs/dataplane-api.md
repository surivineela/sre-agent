# Data plane APIs

## threads APIs

A thread is roughly equivalent to a post in a Team's channel or a conversation thread in ChatGPT UI. The main difference with ChatGPT UI is that the Agent can start a thread.

### Get threads

- `GET /api/v1/threads/?filter`

The API is pageable with ODATA filter. It is possible to get a specific thread by using this: `GET /api/v1/threads/<id>`

Response:

```yaml
value:
  - id: guid
    title: 'Welcome'
    startMessage:
        id: guid
        timeStamp: '2025-03-01'
        role: SREAgent
        text: "Hello, I am an SRE agent, blah blah blah"
    createdTimestamp: '2025-03-01' 
    modifiedTimestamp: '2024-03-11'
  - id: guid
    title: 'Updating TSL settings'
    startMessage:
        id: guid
        timeStamp: '2025-03-01'
        role: SREAgent
        text: "I have detected the following apps have TLS settings set to an older version. Do you want me to fix that?"
    createdTimestamp: '2025-03-01' 
    modifiedTimestamp: '2024-03-11'
  - id: guid
    title: 'Current status update'
    startMessage:
        id: guid
        timeStamp: '2025-03-11'
        role: User
        text: "What is going on with my apps right now?"
    createdTimestamp: '2025-03-01' 
    modifiedTimestamp: '2024-03-11'
```

### Create a thread

- `POST /api/v1/threads/`

Request:

```yaml
startMessage:
    text: "Hello, can you tell me which subscriptions I have an access to?"
```

Response:

```yaml
id: guid
title: 'Current status update'
startMessage: 
    id: guid
    timeStamp: '2025-03-12'
    role: User
    text: "Hello, can you tell me which subscriptions I have an access to?"
createdTimestamp: '2025-03-12' 
modifiedTimestamp: '2024-03-12'
```

## Messages

Messages represent the conversation history between agent and user. All messages posted by user will have the role property set to `User`. Posting a message with role `SREAgent` will result in an error.

### Send message to an existing thread

- `POST /api/v1/threads/<id>/messages`

Request:

```yaml
text: "What apps do I have in this subscription?"
```

Response:

```yaml
id: guid
timeStamp: '2025-03-12'
role: User
text: "What apps do I have in this subscription?"
```

### Get message(s) in a thread

- `GET /api/v1/threads/<id>/messages?filter`

The API is pageable with ODATA filter. It is possible to get a specific message by using this: `GET /threads/<id>/messages/<id>`

```yaml
value:
  - id: guid
    timeStamp: '2025-03-12'
    role: User
    text: "Hello, can you tell me which subscriptions I have an access to?"
  - id: guid
    timeStamp: '2025-03-12'
    role: SREAgent
    text: "You have access to the following subscriptions ..."
  - id: guid
    timeStamp: '2025-03-12'
    role: User
    text: "What apps do I have in this subscription 'My Subscription'?"
```

## Actions

Actions represent the history of what operations an agent has performed in the context of this thread. It is a read-only collection.

- `GET /api/v1/threads/<id>/actions`

Response:

```yaml
value:
  - id: guid
    title: "Applied TLS configuration change to an app service named myapservice1"
    timeStamp: '2025-03-10'
    status: Completed
  - id: guid
    title: "Applied TLS configuration change to an app service named myapservice2"
    timeStamp: '2025-03-11'
    status: Completed
```
