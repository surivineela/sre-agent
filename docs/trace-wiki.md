# Agent Trace

The SRE Agent is instrumented with comprehensive trace data to support various scenarios, including debugging. This document provides an overview of the tracing capabilities within the SRE Agent.

## Span Type

A trace consists of multiple spans that capture different aspects of execution. Currently, the system supports the following span types:

- invoke.agent
- tool.call
- handoff
- user.message: The span indicates the input from user, which contains two cases, user input message and user approve request.

### invoke.agent

Invoke an agent to handle the user message.

### tool.call

Execute a function tool

### handoff

Handoff to another new agent

### user.message

The span indicates the input from user, which contains two cases, user input message and user approve request.

### user.approval

## Span Relationship

Spans maintain hierarchical relationships within the trace structure, primarily categorized as parent-child relationships or sibling relationships.

