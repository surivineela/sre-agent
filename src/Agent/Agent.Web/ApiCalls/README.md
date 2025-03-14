# Hurl API Testing Guide

This README provides instructions for installing and using Hurl to test the Threads API.

## What is Hurl?

[Hurl](https://hurl.dev/) is a command-line tool that runs HTTP requests defined in a simple plain text format. It's useful for API testing and can be used in CI/CD pipelines.

## Installation Instructions

### Windows Installation

#### Option 1: Using Chocolatey
```powershell
choco install hurl
```

#### Option 2: Using Scoop
```powershell
scoop install hurl
```

#### Option 3: Manual Installation
1. Download the latest Windows ZIP from [Hurl Releases](https://github.com/Orange-OpenSource/hurl/releases)
2. Extract the archive to a folder
3. Add the extracted folder to your PATH

### Linux Installation

#### Debian/Ubuntu
```bash
sudo apt-get update
sudo apt-get install -y ca-certificates curl gnupg
curl -fsSL https://www.orange-opensource.org/hurl/gpg.key | sudo gpg --dearmor -o /usr/share/keyrings/hurl.gpg
echo "deb [signed-by=/usr/share/keyrings/hurl.gpg] https://www.orange-opensource.org/hurl/debian/ stable main" | sudo tee /etc/apt/sources.list.d/hurl.list
sudo apt-get update
sudo apt-get install -y hurl
```

#### Red Hat/CentOS/Fedora
```bash
sudo dnf install -y hurl
```

#### Using Homebrew (Linux)
```bash
brew install hurl
```

## Using Hurl with Threads API

The `threads-api.hurl` file contains various API calls to interact with the Threads service.

### Basic Usage

To run all requests in the file:

```bash
hurl src/Agent/Agent.Web/ApiCalls/threads-api.hurl --variable ApiHostname=localhost:7023 --insecure --test
```

### Advanced Usage

To see detailed responses including JSON:

```bash
hurl src/Agent/Agent.Web/ApiCalls/threads-api.hurl --variable ApiHostname=localhost:7023 --insecure --very-verbose
```

### Key Features Used in Our API Tests

1. **Variable Capture and Reuse**: Our hurl file captures the thread ID from the response and uses it in subsequent requests:

```
[Captures]
threadId: jsonpath "$.value[0].id"
```

2. **Usage of Variables**: The captured threadId is used in subsequent requests:

```
GET https://{{ApiHostname}}/api/v1/threads/{{threadId}}
```

3. **Environment Variables**: Setting the API hostname as a variable:

```
--variable ApiHostname=localhost:7023
```

4. **Assertions**: Checking the response status code and JSON content:

```
assert response.status == 200
assert response.body == '{"id": {{threadId}}, "title": "Test Thread", "messages": []}'
```

Once we have our E2E pipeline set up, we can run these tests automatically on every build or make it for PR validation.

### Workflow Explained

1. First, we can create a new thread
2. Then we get a list of all threads and capture the first thread's ID
3. Using that ID, we can:
   - Get details about that specific thread
   - Get the messages in that thread
   - Add a new message to that thread
   - Get the actions for that thread

This approach enables automated end-to-end testing of the Thread API functionality.

## Additional Hurl Documentation

- [Official Documentation](https://hurl.dev/docs/index.html)
- [JSON Path Syntax](https://hurl.dev/docs/asserting-response.html#jsonpath-expressions)
- [Variable Captures](https://hurl.dev/docs/capturing-response.html)
