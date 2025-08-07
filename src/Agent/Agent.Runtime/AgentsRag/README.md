This folder contains agents with per-agent memory feature(RAG) enabled, that are feature gated.
They will overwrite agents with the same name when `AgentMemory.Enabled` is true and at least one of (`AgentMemory.TrajectoryRetrievalEnabled` || `AgentMemory.DocumentRetrievalEnabled` || `AgentMemory.UserMemoryRetrievalEnabled`) is true.
