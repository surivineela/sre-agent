# Thread Evaluations API Documentation

This document describes the API endpoints for managing thread evaluation results in the system. All endpoints are under the `/api/v1/threads/evaluations` route unless otherwise specified.

---

## ThreadEvaluateResult Properties

A `ThreadEvaluateResult` object contains the following properties:

| Property                | Type      | Description                                                                 |
|-------------------------|-----------|-----------------------------------------------------------------------------|
| Id                      | string    | Unique identifier for the evaluation result                                 |
| ThreadId                | string    | Unique identifier for the thread                                            |
| ThreadTitle             | string    | Title of the thread                                                         |
| Duration                | TimeSpan  | Duration of the thread (e.g., time spent)                                   |
| ToolCallCount           | int       | Number of tool calls made                                                   |
| ToolCallSuccessRate     | double    | Success rate of tool calls (0-1)                                            |
| AzCliCallCount          | int       | Number of Azure CLI tool calls                                              |
| AzCliSuccessRate        | double    | Success rate of Azure CLI tool calls (0-1)                                  |
| KubectlCallCount        | int       | Number of kubectl tool calls                                                |
| KubectlSuccessRate      | double    | Success rate of kubectl tool calls (0-1)                                    |
| EvaluationSummary       | string    | Summary of the evaluation                                                   |
| Category                | string    | Category of the evaluation result                                           |
| Resolved                | int       | Whether the issue was resolved (1 = yes, 0 = no)                            |
| Satisfied               | int       | Whether the user was satisfied (1 = yes, 0 = no)                            |
| Automatic               | int       | Whether the process was automatic (1 = yes, 0 = no)                         |
| Smooth                  | int       | Whether the process was smooth (1 = yes, 0 = no)                            |
| Concise                 | int       | Whether the process was concise (1 = yes, 0 = no)                           |
| Adherence               | int       | Whether the process was concise (1 = yes, 0 = no)                           |
| Priority                | string    | Priority assigned to the thread                                             |
| EvaluatedTimestamp      | DateTime  | Timestamp when the evaluation was performed                                 |

---

## Get All Thread Evaluations

**GET** `/api/v1/threads/evaluations`

- **Description:** Retrieves all thread evaluation results, with support for OData query options (pagination, filtering, sorting).
- **Query Parameters:** OData query options (e.g., `$top`, `$skip`, `$filter`, `$orderby`)
- **Response:**
  - `200 OK`: Returns a paged response of `ThreadEvaluateResult` objects.
  - `500 Internal Server Error`: On failure.

---

## Get Thread Evaluation by Evaluation ID

**GET** `/api/v1/threads/evaluations/{evaluationId}`

- **Description:** Retrieves a specific thread evaluation result by its unique evaluation ID.
- **Path Parameters:**
  - `evaluationId` (GUID): The ID of the evaluation to retrieve.
- **Response:**
  - `200 OK`: Returns the `ThreadEvaluateResult`.
  - `404 Not Found`: If the evaluation does not exist.
  - `500 Internal Server Error`: On failure.

---

## Get Thread Evaluation by Thread ID

**GET** `/api/v1/threads/evaluations/by-thread/{threadId}`

- **Description:** Retrieves the evaluation result for a specific thread.
- **Path Parameters:**
  - `threadId` (GUID): The ID of the thread.
- **Response:**
  - `200 OK`: Returns the `ThreadEvaluateResult` for the thread.
  - `404 Not Found`: If the thread or its evaluation does not exist.
  - `500 Internal Server Error`: On failure.

---

## Create a Thread Evaluation

**POST** `/api/v1/threads/evaluations`

- **Description:** Creates a new thread evaluation result.
- **Request Body:**
  - `ThreadEvaluateResult` object (JSON)
- **Response:**
  - `201 Created`: Returns the created `ThreadEvaluateResult`.
  - `400 Bad Request`: If the request is invalid.
  - `404 Not Found`: If the referenced thread does not exist.
  - `500 Internal Server Error`: On failure.

---

## Update a Thread Evaluation

**PUT** `/api/v1/threads/evaluations/{evaluationId}`

- **Description:** Updates an existing thread evaluation result.
- **Path Parameters:**
  - `evaluationId` (GUID): The ID of the evaluation to update.
- **Request Body:**
  - `ThreadEvaluateResult` object (JSON)
- **Response:**
  - `200 OK`: Returns the updated `ThreadEvaluateResult`.
  - `400 Bad Request`: If the request is invalid or IDs do not match.
  - `404 Not Found`: If the evaluation does not exist.
  - `500 Internal Server Error`: On failure.

---

## Delete a Thread Evaluation

**DELETE** `/api/v1/threads/evaluations/{evaluationId}`

- **Description:** Deletes a thread evaluation result by its ID.
- **Path Parameters:**
  - `evaluationId` (GUID): The ID of the evaluation to delete.
- **Response:**
  - `204 No Content`: On successful deletion.
  - `404 Not Found`: If the evaluation does not exist.
  - `500 Internal Server Error`: On failure.

---

**Note:** All endpoints return standard error responses with appropriate HTTP status codes and error messages. OData query options are supported where indicated for flexible querying.
