# FunctionAppExecutionFailuresPlugin Comprehensive Unit Test Documentation

This document provides complete documentation for all unit tests in the `FunctionAppExecutionFailuresPlugin` class, consolidating information from multiple test documentation files and ensuring coverage of all test methods.

## Overview

The `FunctionAppExecutionFailuresPlugin` provides diagnostic capabilities for Azure Function Apps, including execution failure analysis, call stack retrieval, exception tracking, and runtime error detection. This comprehensive test suite ensures robust functionality across all plugin methods.

### Test File Location
`src/Agent/Agent.Tests.Unit/Plugins/Implementation/FunctionAppExecutionFailuresPluginTests.cs`

### Total Test Coverage
**56 Tests** covering **9 public methods** with comprehensive scenarios including success cases, error handling, edge cases, input validation, and logging verification.

## Methods Tested and Coverage Summary

| Method | Test Count | Coverage Areas |
|--------|------------|----------------|
| `GetFunctionAppExecutionFailures` | 8 tests | HTTP responses, large response handling, error cases |
| `GetFailedFunctionInvocations` | 11 tests | Time grains, resource parsing, JSON handling, validation |
| `GetFunctionAppCallStacks` | 9 tests | Success scenarios, exception handling, logging |
| `GetTop3ExceptionsPerFunction` | 6 tests | Time ranges, resource extraction, query validation |
| `GetTop3ExceptionsWithStackTraces` | 7 tests | Stack trace handling, time grains, query structure |
| `GetHostRuntimeErrorEvents` | 3 tests | Input validation, error message formatting |
| `IsFunctionApp` | 8 tests | Resource type detection, JSON parsing, edge cases |
| `HasHostRuntimeErrors` | 2 tests | Input validation, error handling |
| `TriggerFunctionAppSync` | 2 tests | Input validation, sync operations |

---

## Test Implementation Details

### Mock Setup Architecture
- **Dependency Injection**: Uses Moq framework for all dependencies
- **Dual Plugin Instances**: 
  - `_plugin`: Without ArmHelper for methods that don't require it
  - `_pluginWithArmHelper`: With ArmHelper for methods requiring ARM operations
- **HTTP Mocking**: Complete HttpMessageHandler mocking for ARM API calls
- **Logging Verification**: Comprehensive logging behavior validation

### Key Dependencies Mocked
- `IArmPlugin` - ARM resource operations
- `IAppCodeAnalysisPlugin` - Code analysis and call stack retrieval
- `IAppInsightsPlugin` - Application Insights query execution
- `ILogger<FunctionAppExecutionFailuresPlugin>` - Logging verification
- `ArmHelper` - Direct ARM API interactions (concrete class with mocked dependencies)

---

## Detailed Test Coverage by Method

### 1. GetFunctionAppExecutionFailures Tests (8 tests)

**Purpose**: Retrieves execution failure summaries from Azure ARM detectors

#### ✅ Success Scenarios
- **`GetFunctionAppExecutionFailures_WithValidResourceId_ReturnsDetectorResponse`**
  - Tests successful detector response retrieval
  - Verifies proper HTTP request construction and response handling
  - Confirms information logging occurs

- **`GetFunctionAppExecutionFailures_WithSmallResponse_ReturnsDirectly`**
  - Tests responses under size threshold (50KB)
  - Verifies no additional processing for small responses
  - Ensures efficient handling of typical response sizes

#### ✅ Large Response Handling
- **`GetFunctionAppExecutionFailures_WithLargeResponse_ExtractsCriticalFailures`**
  - Tests extraction of critical failure data from large responses (>50KB)
  - Verifies detection of "Critical" severity failures
  - Confirms successful extraction logging

- **`GetFunctionAppExecutionFailures_WithLargeResponseNoCriticalFailures_ReturnsFullResponse`**
  - Tests large responses without critical failures
  - Verifies fallback to full response when extraction fails
  - Confirms warning logging for failed extraction

- **`GetFunctionAppExecutionFailures_WithMalformedLargeResponse_ReturnsFullResponse`**
  - Tests malformed JSON in large responses
  - Verifies graceful handling of parsing errors
  - Confirms error logging for parsing failures

#### ✅ Input Validation
- **`GetFunctionAppExecutionFailures_WithNullResourceId_ReturnsInvalidMessage`**
  - Tests null resource ID handling
  - Verifies error message: "Invalid resource ID."
  - Confirms error logging

- **`GetFunctionAppExecutionFailures_WithEmptyResourceId_ReturnsInvalidMessage`**
  - Tests empty string resource ID handling
  - Verifies consistent error message format
  - Confirms error logging

#### ✅ Error Handling
- **`GetFunctionAppExecutionFailures_WithHttpException_ReturnsErrorMessage`**
  - Tests network-level HTTP exceptions
  - Verifies error message format: "Failed to retrieve execution failures: {message}"
  - Confirms exception logging with proper context

### 2. GetFailedFunctionInvocations Tests (11 tests)

**Purpose**: Analyzes failed function invocations using Application Insights data

#### ✅ Core Functionality
- **`GetFailedFunctionInvocations_WithValidResourceId_ReturnsExpectedData`**
  - Tests main happy path with valid JSON data
  - Verifies correct parsing of function names, timestamps, and failure counts
  - Validates data sorting by timestamp
  - Confirms return of `List<FunctionInvocationsDataPoint>`

#### ✅ Time Grain Logic
- **`GetFailedFunctionInvocations_WithDefaultMinutes_Uses60Minutes`**
  - Tests default 60-minute time range when no parameter provided
  - Verifies 5-minute time grain for short ranges
  - Confirms proper query construction

- **`GetFailedFunctionInvocations_WithMediumTimeRange_Uses10MinuteGrain`**
  - Tests 12-hour time range (6-24 hour range)
  - Verifies 10-minute time grain selection
  - Confirms query optimization for medium ranges

- **`GetFailedFunctionInvocations_WithLongTimeRange_UsesCorrectTimeGrain`**
  - Tests 25-hour time range (>24 hours)
  - Verifies 1-day time grain for long ranges
  - Confirms appropriate aggregation for extended periods

#### ✅ Resource Name Parsing
- **`GetFailedFunctionInvocations_WithResourceIdContainingSlashes_ExtractsCorrectResourceName`**
  - Tests full Azure resource ID parsing
  - Verifies extraction of resource name from `/subscriptions/.../sites/name` format
  - Confirms proper query parameter substitution

- **`GetFailedFunctionInvocations_WithSimpleResourceName_UsesResourceNameAsIs`**
  - Tests simple resource name handling
  - Verifies direct usage without parsing when no slashes present
  - Confirms query construction with simple names

#### ✅ Data Processing and Edge Cases
- **`GetFailedFunctionInvocations_WithEmptyAppInsightsResponse_ReturnsEmptyList`**
  - Tests empty JSON response handling
  - Verifies return of empty list rather than null
  - Confirms graceful handling of no-data scenarios

- **`GetFailedFunctionInvocations_WithInvalidJson_ReturnsEmptyListAndLogsError`**
  - Tests malformed JSON response handling
  - Verifies error logging occurs
  - Confirms return of empty list for invalid data

- **`GetFailedFunctionInvocations_WithMissingColumns_ReturnsEmptyList`**
  - Tests responses with missing expected columns
  - Verifies graceful handling of schema mismatches
  - Confirms empty list return for incomplete data

- **`GetFailedFunctionInvocations_WithNullValues_HandlesGracefully`**
  - Tests null value handling in JSON data
  - Verifies proper null value processing
  - Confirms robustness against incomplete data

#### ✅ Query Structure Validation
- **`GetFailedFunctionInvocations_VerifyQueryStructure_ContainsExpectedElements`**
  - Tests KQL query construction
  - Verifies presence of key query elements:
    - `requests` table usage
    - `success == false` filter
    - `client_Type != "Browser"` filter
    - `summarize FailedCount=sumif(itemCount, success == false)` aggregation
    - Time grain binning

### 3. GetFunctionAppCallStacks Tests (9 tests)

**Purpose**: Retrieves call stack information for Azure Function App executions

#### ✅ Success Scenarios
- **`GetFunctionAppCallStacks_WithValidResourceId_ReturnsCallStacks`**
  - Tests successful call stack retrieval with realistic JSON data
  - Verifies exact return value matches expected call stacks
  - Confirms plugin method called with correct resource ID

- **`GetFunctionAppCallStacks_WithEmptyCallStacks_ReturnsEmptyResult`**
  - Tests handling of empty call stack results
  - Verifies empty JSON response returned correctly
  - Confirms method doesn't fail with empty data

- **`GetFunctionAppCallStacks_WithNullOrEmptyResult_ReturnsResult`**
  - Tests null or empty string result handling
  - Ensures method doesn't crash with empty responses
  - Verifies consistent behavior across edge cases

- **`GetFunctionAppCallStacks_WithComplexCallStackData_ReturnsCompleteResult`**
  - Tests realistic, complex call stack JSON
  - Includes multiple functions with detailed stack traces
  - Contains exception types, messages, and metadata
  - Ensures complete data returned unchanged

#### ✅ Exception Handling
- **`GetFunctionAppCallStacks_WhenAppCodeAnalysisPluginThrowsException_ReturnsErrorMessage`**
  - Tests generic exception handling
  - Verifies error message format: `"Failed to retrieve call stacks: {ex.Message}"`
  - Confirms error logging with correct log level and exception details

- **`GetFunctionAppCallStacks_WhenAppCodeAnalysisPluginThrowsHttpException_ReturnsErrorMessage`**
  - Tests HTTP-specific exceptions (404, network issues)
  - Verifies proper error message formatting
  - Confirms HTTP exceptions logged correctly

- **`GetFunctionAppCallStacks_WhenAppCodeAnalysisPluginThrowsTimeoutException_ReturnsErrorMessage`**
  - Tests timeout scenario handling
  - Verifies timeout exceptions return appropriate error messages
  - Confirms timeout exceptions logged with correct details

#### ✅ Logging and Validation
- **`GetFunctionAppCallStacks_LogsInformationWithResourceId`**
  - Verifies information-level logging on method invocation
  - Confirms log message format: `[get_function_app_call_stacks] Invoked with resourceId`
  - Tests logging integration

- **`GetFunctionAppCallStacks_WithDifferentResourceIdFormats_CallsPluginCorrectly`**
  - Tests various Azure resource ID formats
  - Verifies method passes through any resource ID format correctly
  - Ensures plugin called with exact resource ID provided

### 4. GetTop3ExceptionsPerFunction Tests (6 tests)

**Purpose**: Retrieves top 3 exceptions per function from Application Insights

#### ✅ Core Functionality and Time Handling
- **`GetTop3ExceptionsPerFunction_WithValidResourceId_ReturnsExceptionsData`**
  - Tests successful exception data retrieval
  - Verifies proper Application Insights plugin integration
  - Confirms data processing and return

- **`GetTop3ExceptionsPerFunction_WithCustomTimeRange_UsesProvidedTimes`**
  - Tests custom start and end time handling
  - Verifies time parameters passed correctly to query
  - Confirms flexible time range support

- **`GetTop3ExceptionsPerFunction_WithDefaultTimes_UsesCorrectDefaults`**
  - Tests default time range behavior
  - Verifies appropriate default time span
  - Confirms consistent default behavior

- **`GetTop3ExceptionsPerFunction_WithLongTimeRange_UsesCorrectTimeGrain`**
  - Tests time grain selection for long ranges
  - Verifies appropriate aggregation for extended periods
  - Confirms query optimization

#### ✅ Resource Processing and Query Validation
- **`GetTop3ExceptionsPerFunction_WithResourceIdContainingSlashes_ExtractsResourceName`**
  - Tests resource name extraction from full Azure resource IDs
  - Verifies proper parsing of complex resource identifiers
  - Confirms query parameter substitution

- **`GetTop3ExceptionsPerFunction_VerifyQueryStructure_ContainsExpectedElements`**
  - Tests KQL query construction for exception analysis
  - Verifies presence of exception-specific query elements
  - Confirms proper query structure and syntax

### 5. GetTop3ExceptionsWithStackTraces Tests (7 tests)

**Purpose**: Retrieves top 3 exceptions with detailed stack trace information

#### ✅ Comprehensive Stack Trace Handling
- **`GetTop3ExceptionsWithStackTraces_WithValidResourceId_ReturnsExceptionsWithStackTraces`**
  - Tests successful retrieval of exceptions with stack traces
  - Verifies integration with Application Insights for detailed exception data
  - Confirms complete stack trace information return

- **`GetTop3ExceptionsWithStackTraces_WithCustomTimeRange_UsesProvidedTimes`**
  - Tests custom time range handling for stack trace queries
  - Verifies time parameters correctly applied
  - Confirms flexible time range support for detailed analysis

- **`GetTop3ExceptionsWithStackTraces_WithDefaultTimes_UsesCorrectDefaults`**
  - Tests default time range behavior for stack trace analysis
  - Verifies appropriate default settings
  - Confirms consistent behavior across different query types

- **`GetTop3ExceptionsWithStackTraces_WithLongTimeRange_UsesCorrectTimeGrain`**
  - Tests time grain optimization for long-range stack trace queries
  - Verifies appropriate aggregation strategies
  - Confirms query performance optimization

#### ✅ Resource Processing and Query Structure
- **`GetTop3ExceptionsWithStackTraces_WithResourceIdContainingSlashes_ExtractsResourceName`**
  - Tests resource name extraction for stack trace queries
  - Verifies proper handling of complex Azure resource identifiers
  - Confirms query parameter processing

- **`GetTop3ExceptionsWithStackTraces_WithSimpleResourceName_UsesNameDirectly`**
  - Tests direct usage of simple resource names
  - Verifies handling when no parsing required
  - Confirms efficient processing for simple identifiers

- **`GetTop3ExceptionsWithStackTraces_VerifyQueryStructure_ContainsExpectedElements`**
  - Tests KQL query construction for stack trace analysis
  - Verifies presence of stack trace-specific query elements
  - Confirms proper query structure for detailed exception analysis

### 6. GetHostRuntimeErrorEvents Tests (3 tests)

**Purpose**: Retrieves host runtime error events from Azure ARM activity logs

#### ✅ Input Validation
- **`GetHostRuntimeErrorEvents_WithNullResourceId_ReturnsInvalidMessage`**
  - Tests null resource ID handling
  - Verifies error message: "Invalid resource ID."
  - Confirms proper input validation

- **`GetHostRuntimeErrorEvents_WithEmptyResourceId_ReturnsInvalidMessage`**
  - Tests empty string resource ID handling
  - Verifies consistent error message format
  - Confirms comprehensive input validation

- **`GetHostRuntimeErrorEvents_WithInvalidResourceIdFormat_ReturnsInvalidFormat`**
  - Tests malformed resource ID handling
  - Verifies format validation logic
  - Confirms appropriate error messaging for invalid formats

### 7. IsFunctionApp Tests (8 tests)

**Purpose**: Determines if a given Azure resource is a Function App

#### ✅ Function App Detection
- **`IsFunctionApp_WithValidFunctionAppResource_ReturnsTrue`**
  - Tests detection of standard Function App resources
  - Verifies `kind` property analysis for "functionapp" value
  - Confirms positive identification logic

- **`IsFunctionApp_WithFunctionAppLinuxResource_ReturnsTrue`**
  - Tests detection of Linux Function App resources
  - Verifies handling of "functionapp,linux" kind values
  - Confirms platform-specific detection

- **`IsFunctionApp_WithKindAsArray_ReturnsTrue`**
  - Tests `kind` property as JSON array format
  - Verifies proper JSON parsing for array-based kind values
  - Confirms flexible JSON structure handling

#### ✅ Non-Function App Resources
- **`IsFunctionApp_WithWebAppResource_ReturnsFalse`**
  - Tests detection of regular Web App resources
  - Verifies proper distinction between web apps and function apps
  - Confirms negative identification logic

- **`IsFunctionApp_WithResourceWithoutKind_ReturnsFalse`**
  - Tests resources missing `kind` property
  - Verifies graceful handling of incomplete resource data
  - Confirms conservative identification approach

#### ✅ Edge Cases and Error Handling
- **`IsFunctionApp_WithNullResourceId_ReturnsFalse`**
  - Tests null resource ID handling
  - Verifies safe fallback behavior
  - Confirms no exceptions thrown for null input

- **`IsFunctionApp_WithEmptyResourceJson_ReturnsFalse`**
  - Tests empty or invalid JSON resource data
  - Verifies graceful handling of malformed data
  - Confirms robust error handling

- **`IsFunctionApp_WithException_ReturnsFalse`**
  - Tests exception handling during resource analysis
  - Verifies safe fallback on errors
  - Confirms method resilience to unexpected failures

### 8. HasHostRuntimeErrors Tests (2 tests)

**Purpose**: Checks if a Function App has host runtime errors

#### ✅ Input Validation
- **`HasHostRuntimeErrors_WithNullResourceId_ReturnsFalse`**
  - Tests null resource ID handling
  - Verifies safe fallback behavior
  - Confirms no exceptions for null input

- **`HasHostRuntimeErrors_WithEmptyResourceId_ReturnsFalse`**
  - Tests empty string resource ID handling
  - Verifies consistent behavior across invalid inputs
  - Confirms comprehensive input validation

### 9. TriggerFunctionAppSync Tests (2 tests)

**Purpose**: Triggers synchronization of Function App host

#### ✅ Input Validation
- **`TriggerFunctionAppSync_WithNullResourceId_ReturnsInvalidMessage`**
  - Tests null resource ID handling
  - Verifies error message: "Invalid resource ID."
  - Confirms proper input validation

- **`TriggerFunctionAppSync_WithEmptyResourceId_ReturnsInvalidMessage`**
  - Tests empty string resource ID handling
  - Verifies consistent error message format
  - Confirms comprehensive input validation

---

## Test Data and Mock Patterns

### HTTP Response Mocking
```csharp
private void SetupHttpResponse(HttpStatusCode statusCode, string responseContent)
{
    var response = new HttpResponseMessage(statusCode)
    {
        Content = new StringContent(responseContent)
    };
    _mockHttpMessageHandler.Protected()
        .Setup<Task<HttpResponseMessage>>("SendAsync", ...)
        .ReturnsAsync(response);
}
```

### Application Insights Mock Data
```json
{
  "tables": [{
    "columns": [
      {"name": "name"},
      {"name": "timestamp"}, 
      {"name": "FailedCount"}
    ],
    "rows": [
      ["Function1", "2023-11-15T10:00:00Z", 5.0],
      ["Function2", "2023-11-15T10:05:00Z", 3.0]
    ]
  }]
}
```

### ARM Detector Response Mock
```json
{
  "id": "/subscriptions/.../providers/Microsoft.Web/sites/test-function-app",
  "name": "functionExecutionErrors",
  "properties": {
    "dataset": [{
      "table": {
        "tableName": "Function Execution Errors",
        "rows": [["Function1", 5], ["Function2", 3]]
      }
    }]
  }
}
```

### Call Stack Mock Data
```json
{
  "callStacks": [{
    "functionName": "HttpTriggerFunction",
    "stackTrace": "at HttpTriggerFunction.Run(...) in C:\\home\\site\\wwwroot\\HttpTriggerFunction.cs:line 23",
    "exceptionType": "System.ArgumentException",
    "exceptionMessage": "Invalid parameter value"
  }],
  "metadata": {
    "timestamp": "2023-11-15T10:30:00Z",
    "resourceId": "/subscriptions/.../test-function-app"
  }
}
```

---

## ArmHelper Integration and Testing Approach

### Challenge: Concrete Class Dependencies
The `ArmHelper` class is a concrete implementation without interface abstraction, creating testing challenges for methods that depend on it.

### Solution: Dual Plugin Architecture
- **Without ArmHelper**: For methods like `GetFailedFunctionInvocations` that don't require ARM helper
- **With ArmHelper**: For methods like `GetFunctionAppExecutionFailures` that need ARM operations

### ArmHelper Mock Setup
```csharp
// Create ArmHelper with fully mocked dependencies
_armHelper = new ArmHelper(
    mockLogger,
    mockHttpClientFactory,
    mockArmClientFactory,
    mockAuthService,
    mockAzureSettings,
    mockHostEnvironment,
    mockCrawlerTriggerService,
    mockSessionPoolService,
    mockChatClient);
```

### Testing Strategy for Non-Virtual Methods
Since ArmHelper methods like `GetCriticalErrorActivityLogs` and `SyncFunctionAppHost` are non-virtual:
- Focus on testing input validation and business logic
- Test error handling and logging behavior
- Verify proper parameter passing and return value handling
- Use HTTP mocking for underlying ARM API calls

---

## Logging Verification Patterns

### Information Logging
```csharp
_mockLogger.Verify(
    x => x.Log(
        LogLevel.Information,
        It.IsAny<EventId>(),
        It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("expected message")),
        It.IsAny<Exception>(),
        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
    Times.Once);
```

### Error Logging with Exception
```csharp
_mockLogger.Verify(
    x => x.Log(
        LogLevel.Error,
        It.IsAny<EventId>(),
        It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("error message")),
        It.Is<Exception>(ex => ex == expectedException),
        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
    Times.Once);
```

---

## Running the Tests

### Complete Test Suite
```bash
# Run all FunctionAppExecutionFailuresPlugin tests
dotnet test --filter "FunctionAppExecutionFailuresPluginTests" --no-restore

# Run with verbose output
dotnet test --filter "FunctionAppExecutionFailuresPluginTests" --no-restore --verbosity normal
```

### Method-Specific Test Execution
```bash
# Run specific method tests
dotnet test --filter "GetFunctionAppExecutionFailures" --no-restore
dotnet test --filter "GetFailedFunctionInvocations" --no-restore
dotnet test --filter "GetFunctionAppCallStacks" --no-restore
dotnet test --filter "GetTop3Exceptions" --no-restore
dotnet test --filter "IsFunctionApp" --no-restore
```

### Build and Test
```bash
# Build and run tests
dotnet build src/Agent/Agent.Tests.Unit/Agent.Tests.Unit.csproj --no-restore
dotnet test src/Agent/Agent.Tests.Unit/Agent.Tests.Unit.csproj --no-restore
```

---

## Test Quality Metrics

### Coverage Statistics
- **Total Methods**: 9 public async methods
- **Total Tests**: 56 comprehensive test cases
- **Coverage Areas**: Success scenarios, error handling, input validation, edge cases, logging verification
- **Mock Verification**: All dependency interactions verified
- **Exception Handling**: Comprehensive exception scenario coverage

### Quality Assurance Features
- **Independent Tests**: All tests can run in any order
- **Comprehensive Mocking**: All external dependencies properly mocked
- **Realistic Data**: Mock data mirrors actual Azure API responses
- **Error Resilience**: Extensive error scenario coverage
- **Logging Validation**: Complete logging behavior verification
- **Performance Considerations**: Tests account for response size optimization

---

## Benefits and Maintenance

### Regression Protection
- Prevents future changes from breaking existing functionality
- Ensures API contract consistency
- Validates error handling behavior

### Documentation Value
- Tests serve as living documentation of expected behavior
- Demonstrates proper usage patterns
- Shows error handling approaches

### Development Confidence
- Enables safe refactoring and feature additions
- Provides immediate feedback on changes
- Ensures robust handling of various input scenarios

### Code Quality
- Enforces proper error handling patterns
- Validates logging behavior
- Ensures comprehensive input validation

---

## Dependencies and Requirements

### NuGet Packages
- **xUnit** - Testing framework
- **Moq** - Mocking framework
- **Shouldly** - Assertion library (optional)
- **Microsoft.Extensions.*** - Dependency injection and logging
- **Newtonsoft.Json** - JSON processing

### Test Infrastructure
- Existing project test patterns and conventions
- Established mocking approaches
- Consistent error handling strategies
- Standardized logging verification methods

---

*This documentation consolidates information from multiple sources and provides comprehensive coverage of all 56 test methods across 9 plugin methods, ensuring complete understanding of the test suite's scope and capabilities.*