To onboard a new Incident Provider (such as Azure Monitor), you’ll need to implement several key classes and register them for dependency injection. Below is a step-by-step onboarding guide and a checklist of required implementations and changes, based on the provided code.

---

# Onboarding a New Incident Provider

This guide explains how to onboard a new incident provider (e.g., Azure Monitor) into the SRE Agent Runtime platform. The incident provider is responsible for integrating with an external incident management system and enabling incident handling, filtering, and management.

## 1. **Define Data Models**

**Location:** `src/Agent/Agent.Data/DataModels/IncidentFilterDocument/{YourProvider}IncidentFilterDocument.cs`

- **Incident Filter Document:**  
  Create a record for the filter document that inherits from `IncidentFilterDocument`.  
  Example:
  ```csharp
  public record AzMonitorIncidentFilterDocument : IncidentFilterDocument { ... }
  public class AzMonitorIncidentFilterDocumentPayload : IncidentFilterDocumentPayload { }
  ```

- **Incident Document:**  
  Create your incident document model (e.g., `AzMonitorIncidentDocument`) that represents a single incident.

Example:
```csharp
public record AzMonitorIncidentDocument : IIncidentDocument {}
```
---

## 2. **Implement Service Classes**

You need to implement three main service types:


### a. **Incident Filter Management Service**

**Location:** `src/Agent/Agent.Runtime/Services/IncidentFilterManagementService/`

- Create `{YourProvider}IncidentFilterManagementService.cs` inheriting from  
  `IncidentFilterManagementServiceBase<{YourProvider}IncidentFilterDocument>`.
- Implement required methods, such as:
  - `CheckConnectivity()`
  - `ListIncidentFilterFieldOptions()`

**Example:**
```csharp
public class AzMonitorIncidentFilterManagementService : IncidentFilterManagementServiceBase<AzMonitorIncidentFilterDocument> { ... }
```

---

### b. **Incident Management Service**

**Location:** `src/Agent/Agent.Runtime/Services/IncidentManagementService/`

- Create `{YourProvider}IncidentManagementService.cs` inheriting from  
  `IncidentManagementServiceBase<{YourProvider}IncidentDocument, {YourProvider}IncidentFilterDocument>`.
- Implement methods like:
  - `GetIncidentDetails(string incidentId)`
  - `QueryIncidents(IncidentQueryRequest request)`

---

### c. **Incident Handling Service**

**Location:** `src/Agent/Agent.Runtime/Services/IncidentHandlingService/`

- Create `{YourProvider}IncidentHandlingService.cs` inheriting from  
  `IncidentHandlingServiceBase<{YourProvider}IncidentDocument, {YourProvider}IncidentFilterDocument, {YourProvider}IncidentFilterDocumentPayload>`.
- Implement methods such as:
  - `GetIncidentAsync(string incidentId)`
  - `CreateIncidentHandlerAgentThreadAsync(...)`
  - `GetDefaultIncidentFilter(...)`

---

## 3. **Register Services for Dependency Injection**

**Location:**  
`src/Agent/Agent.Runtime/Services/IncidentHandlingServiceCollectionExtensions.cs`

- In the `AddIncidentRelatedServices` extension method, add your provider to the switch statement:
  Providers that need to add are `IAzMonitorAPIClient`,`IIncidentScanner`, `IIncidentHandlingService`, `IIncidentManagementService`, `IIncidentFilterManagementService`
  ```csharp
  case IncidentManagementType.AzMonitor:
      services.AddSingleton<IIncidentHandlingService<AzMonitorIncidentFilterDocumentPayload>, AzMonitorIncidentHandlingService>();
      services.AddSingleton<IIncidentManagementService<AzMonitorIncidentDocument>, AzMonitorIncidentManagementService>();
      services.AddSingleton<IIncidentFilterManagementService<AzMonitorIncidentFilterDocument>, AzMonitorIncidentFilterManagementService>();
      break;
  ```

---

## 4. **Update Factories**

If your incident provider has a new `IncidentManagementType`, ensure it’s added to:

- Enum: `IncidentManagementType`
- Switch statements in the following factories:
  - `IncidentFilterManagementServiceFactory`
  - `IncidentManagementServiceFactory`
  - `IncidentHandlingServiceFactory`

These factories are in `IncidentServiceCollectionExtensions.cs` and route requests to your new service implementations.

---

## 5. **(Optional) Extend Data/Graph/External Service Interfaces**

If your provider interacts with specific APIs (like Azure Monitor REST), implement the required interfaces (e.g., `IAzMonitorService`) and inject them into your service classes.

# Example File/Type Names for Azure Monitor

| Component Type                | Example Name/Location                                             |
|-------------------------------|-------------------------------------------------------------------|
| Filter Document               | `AzMonitorIncidentFilterDocument`                                 |
| Filter Payload                | `AzMonitorIncidentFilterDocumentPayload`                          |
| Incident Document             | `AzMonitorIncidentDocument`                                       |
| Incident API Client           | `AzMonitorAPIClient`                                              |
| Filter Management Service     | `AzMonitorIncidentFilterManagementService`                        |
| Incident Management Service   | `AzMonitorIncidentManagementService`                              |
| Incident Handling Service     | `AzMonitorIncidentHandlingService`                                |
| DI Registration               | In `IncidentServiceCollectionExtensions.cs`                       |
| Factory Switches              | In respective factory classes                                     |