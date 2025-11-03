package main

import (
    "bytes"
    "encoding/json"
    "fmt"
    "net/http"
    "strings"
    "time"
)

type LLMVerifier struct {
    apiKey       string
    endpoint     string
    client       *http.Client
    deploymentID string // Azure OpenAI deployment name
}

type LLMVerificationResponse struct {
    Passed bool   `json:"passed"`
    Reason string `json:"reason"`
}

func NewLLMVerifier(apiKey, endpoint string) *LLMVerifier {
    // Extract deployment ID from endpoint if it's an Azure OpenAI endpoint
    deploymentID := extractDeploymentID(endpoint)

    return &LLMVerifier{
        apiKey:       apiKey,
        endpoint:     endpoint,
        deploymentID: deploymentID,
        client: &http.Client{
            Timeout: 30 * time.Second,
        },
    }
}

// extractDeploymentID extracts the deployment name from Azure OpenAI endpoint
func extractDeploymentID(endpoint string) string {
    // Azure OpenAI endpoints typically look like:
    // https://{resource-name}.openai.azure.com/openai/deployments/{deployment-id}/chat/completions?api-version=2024-02-01
    parts := strings.Split(endpoint, "/")
    for i, part := range parts {
        if part == "deployments" && i+1 < len(parts) {
            return parts[i+1]
        }
    }
    return ""
}

func (v *LLMVerifier) Verify(conversation, verificationPrompt string) (bool, string) {
    prompt := fmt.Sprintf(`You are evaluating an Azure SRE Agent's response to a user query.

Here is the conversation:
%s

Evaluation criteria: %s

Based on the conversation above, determine if the agent's response meets the evaluation criteria.
Respond with a JSON object in this exact format:
{
  "passed": true/false,
  "reason": "Brief explanation of your decision"
}`, conversation, verificationPrompt)

    // Construct the request for Azure OpenAI
    requestBody := map[string]interface{}{
        "messages": []map[string]string{
            {
                "role":    "system",
                "content": "You are an expert at evaluating AI agent responses. Always respond with valid JSON.",
            },
            {
                "role":    "user",
                "content": prompt,
            },
        },
        "temperature": 0.0, // Use 0 for consistent evaluation
        "max_completion_tokens":  10000,
    }

    jsonData, err := json.Marshal(requestBody)
    if err != nil {
        return false, fmt.Sprintf("Failed to marshal request: %v", err)
    }

    req, err := http.NewRequest("POST", v.endpoint, bytes.NewBuffer(jsonData))
    if err != nil {
        return false, fmt.Sprintf("Failed to create request: %v", err)
    }

    // Set headers for Azure OpenAI
    if strings.Contains(v.endpoint, "openai.azure.com") {
        // Azure OpenAI uses api-key header
        req.Header.Set("api-key", v.apiKey)
    } else {
        // OpenAI uses Authorization header
        req.Header.Set("Authorization", "Bearer "+v.apiKey)
    }
    req.Header.Set("Content-Type", "application/json")

    resp, err := v.client.Do(req)
    if err != nil {
        return false, fmt.Sprintf("Failed to send request: %v", err)
    }
    defer resp.Body.Close()

    if resp.StatusCode != http.StatusOK {
        return false, fmt.Sprintf("LLM API returned status %d", resp.StatusCode)
    }

    // Parse the response - Azure OpenAI has the same response format as OpenAI
    var llmResp struct {
        Choices []struct {
            Message struct {
                Content string `json:"content"`
            } `json:"message"`
        } `json:"choices"`
    }

    if err := json.NewDecoder(resp.Body).Decode(&llmResp); err != nil {
        return false, fmt.Sprintf("Failed to decode response: %v", err)
    }

    if len(llmResp.Choices) == 0 {
        return false, "No response from LLM"
    }

    // Parse the JSON response from the LLM
    var verificationResult LLMVerificationResponse
    if err := json.Unmarshal([]byte(llmResp.Choices[0].Message.Content), &verificationResult); err != nil {
        return false, fmt.Sprintf("Failed to parse LLM response as JSON: %v", err)
    }

    return verificationResult.Passed, verificationResult.Reason
}