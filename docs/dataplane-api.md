# Data plane APIs

## Threads APIs

A thread is roughly equivalent to a post in a Team's channel or a conversation thread in ChatGPT UI. The main difference with ChatGPT UI is that the Agent can start a thread.

### Get threads

- `GET /api/v1/threads/?filter`

The API is pageable with ODATA filter. It is possible to get a specific thread by using this: `GET /api/v1/threads/<id>`

Response:

```yaml
value:
  - id: id
    title: 'Welcome'
    startMessage:
        id: id
        timestamp: '2025-03-01'
        author:
            role: SREAgent
            userId: 'agent-123'
            displayName: 'SRE Agent'
        text: "Hello, I am an SRE agent, blah blah blah"
    createdTimestamp: '2025-03-01' 
    modifiedTimestamp: '2025-03-11'
  - id: id
    title: 'Updating TLS settings'
    startMessage:
        id: id
        timestamp: '2025-03-01'
        author:
            role: SREAgent
            userId: 'agent-456'
            displayName: 'SRE Agent'
        text: "I have detected the following apps have TLS settings set to an older version. Do you want me to fix that?"
    createdTimestamp: '2025-03-01' 
    modifiedTimestamp: '2025-03-11'
  - id: id
    title: 'Current status update'
    startMessage:
        id: id
        timestamp: '2025-03-11'
        author:
            role: User
            userId: 'user-789'
            displayName: 'Paul'
        text: "What is going on with my apps right now?"
    createdTimestamp: '2025-03-01' 
    modifiedTimestamp: '2025-03-11'
```

### Create a thread

- `POST /api/v1/threads/`

Request:

```yaml
startMessage:
    text: "Hello, can you tell me which subscriptions I have access to?"
```

Response:

```yaml
id: id
title: 'Current status update'
startMessage: 
    id: id
    timestamp: '2025-03-12'
    author:
        role: User
        userId: 'user-789'
        displayName: 'Paul'
    text: "Hello, can you tell me which subscriptions I have access to?"
createdTimestamp: '2025-03-12' 
modifiedTimestamp: '2025-03-12'
```

## Messages

Messages represent the conversation history between the agent and user. All messages posted by a user will have an `author` object with role `User`. Posting a message with role `SREAgent` will result in an error.

### Send message to an existing thread

- `POST /api/v1/threads/<id>/messages`

Request:

```yaml
text: "What apps do I have in this subscription?"
```

Response:

```yaml
id: id
timestamp: '2025-03-12'
author:
    role: User
    userId: 'user-789'
    displayName: 'Paul'
text: "What apps do I have in this subscription?"
```

### Get message(s) in a thread

- `GET /api/v1/threads/<id>/messages?filter`

The API is pageable with ODATA filter. It is possible to get a specific message by using this: `GET /threads/<id>/messages/<id>`

```yaml
value:
  - id: id
    timestamp: '2025-03-12'
    author:
        role: User
        userId: 'user-789'
        displayName: 'Paul'
    text: "Hello, can you tell me which subscriptions I have access to?"
  - id: id
    timestamp: '2025-03-12'
    author:
        role: SREAgent
        userId: 'agent-123'
        displayName: 'SRE Agent'
    text: "You have access to the following subscriptions ..."
  - id: id
    timestamp: '2025-03-12'
    author:
        role: User
        userId: 'user-789'
        displayName: 'Paul'
    text: "What apps do I have in this subscription 'My Subscription'?"
```

## Actions

Actions represent the history of what operations an agent has performed in the context of this thread. It is a read-only collection.

- `GET /api/v1/threads/<id>/actions`

Response:

```yaml
value:
  - id: id
    title: "Applied TLS configuration change to an app service named myappservice1"
    timestamp: '2025-03-10'
    status: Completed
  - id: id
    title: "Applied TLS configuration change to an app service named myappservice2"
    timestamp: '2025-03-11'
    status: Completed
