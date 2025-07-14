package model

// TaskResult represents the result of evaluating a single task
type TaskResult struct {
    // Task is the identifier of the task that was evaluated
    Task string `json:"task" yaml:"name"`
    
    // AgentConfig contains the configuration used for the agent during evaluation
    AgentConfig AgentConfig `json:"agentConfig" yaml:"agentConfig"`
    
    // Result indicates the outcome of the task ("success", "fail", or empty if not determined)
    Result string `json:"result" yaml:"result"`
    
    // Error contains any error message if the task execution failed unexpectedly
    Error string `json:"error,omitempty" yaml:"error,omitempty"`
}

// AgentConfig represents the configuration for an agent
type AgentConfig struct {
    // Name is the identifier for this agent configuration
    Name string `json:"name" yaml:"name"`
    
    // AgentURL is the URL where the agent API is accessible
    AgentURL string `json:"agentURL" yaml:"agentURL"`
}