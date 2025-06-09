# Contributing to Agent.Framework

## Overview

`Agent.Framework` is a general-purpose AI Agent framework designed to be reusable across different projects. It provides core functionality for building and running AI agents, managing tools, handling prompts, and orchestrating agent interactions.

## Core Principles

1. **Framework vs. Business Logic**
   - This framework should remain domain-agnostic
   - Business-specific logic should be implemented in the consuming projects
   - The framework should provide extension points for business logic, not contain it

2. **Extensibility**
   - New features should be designed with extensibility in mind
   - Use interfaces and abstract classes to allow for different implementations
   - Provide clear extension points for consumers

3. **Maintainability**
   - Keep the code clean and well-documented
   - Follow C# coding conventions
   - Write unit tests for new features

## What Belongs in the Framework

The following types of changes are appropriate for the framework:

1. **Core Agent Infrastructure**
   - Agent lifecycle management
   - Tool integration and management
   - Prompt handling and management
   - Context management
   - Handoff mechanisms

2. **Generic Utilities**
   - Text processing helpers
   - Common AI function patterns
   - Standard tool implementations
   - Generic prompt templates

3. **Framework Extensions**
   - New extension points
   - Generic interfaces
   - Abstract base classes
   - Common patterns and utilities

## What Does Not Belong in the Framework

The following should be implemented in the consuming projects:

1. **Business Logic**
   - Domain-specific rules
   - Business workflows
   - Custom business processes
   - Industry-specific implementations

2. **Project-Specific Features**
   - Custom tool implementations for specific use cases
   - Project-specific prompt templates
   - Custom handoff logic for specific scenarios

3. **Configuration**
   - Environment-specific settings
   - API keys and secrets
   - Project-specific configurations

## Making Changes

Before making changes, ask yourself:

1. Is this change truly a framework enhancement?
2. Could this be implemented in the consuming project instead?
3. Does this change maintain the framework's domain-agnostic nature?
4. Will this change benefit other projects using the framework?

## Code Review Process

1. **Initial Review**
   - Ensure the change belongs in the framework
   - Verify it follows the core principles
   - Check for proper documentation

2. **Technical Review**
   - Code quality and style
   - Test coverage
   - Performance implications
   - Backward compatibility

3. **Final Approval**
   - At least one framework maintainer must approve
   - Changes must be reviewed by someone familiar with the framework's architecture

## Testing Requirements

1. **Unit Tests**
   - All new features must have unit tests
   - Test both success and failure scenarios
   - Mock external dependencies

2. **Integration Tests**
   - Test framework components working together
   - Verify extension points work as expected

3. **Backward Compatibility**
   - Ensure changes don't break existing implementations
   - Document any breaking changes

## Getting Help

If you're unsure whether a change belongs in the framework:

1. Open a discussion issue
2. Describe the proposed change
3. Explain why you think it belongs in the framework
4. Wait for maintainer feedback before proceeding

Remember: When in doubt, implement the change in the consuming project first. It can always be moved to the framework later if it proves to be generally useful.
