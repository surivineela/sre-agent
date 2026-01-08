import cloneDeep from 'lodash/cloneDeep';
import { ExtendedAgent } from '../../../Contracts/ExtendedAgentGraph';
import { AgentPlaygroundFormValues, QualityFinding } from './Contracts';

const applyUnifiedDiff = (originalText: string, patch: string): string => {
    const lines = originalText.split('\n');
    const patchLines = patch.split('\n');

    let lineIndex = 0;
    let i = 0;

    while (i < patchLines.length) {
        const line = patchLines[i];

        // Parse hunk header: @@ -startLine,count +startLine,count @@
        const hunkMatch = line.match(/^@@\s+-(\d+)(?:,(\d+))?\s+\+(\d+)(?:,(\d+))?\s+@@/);
        if (hunkMatch) {
            const oldStart = parseInt(hunkMatch[1], 10) - 1; // Convert to 0-based
            lineIndex = oldStart;
            i++;
            continue;
        }

        // Context line (starts with space or no prefix)
        if (line.startsWith(' ') || (!line.startsWith('+') && !line.startsWith('-') && !line.startsWith('@'))) {
            lineIndex++;
            i++;
            continue;
        }

        // Deletion (starts with -)
        if (line.startsWith('-') && !line.startsWith('---')) {
            lines.splice(lineIndex, 1);
            i++;
            continue;
        }

        // Addition (starts with +)
        if (line.startsWith('+') && !line.startsWith('+++')) {
            lines.splice(lineIndex, 0, line.substring(1));
            lineIndex++;
            i++;
            continue;
        }

        i++;
    }

    return lines.join('\n');
};

export const getAgentWithFindingsApplied = (findings: QualityFinding[], values: AgentPlaygroundFormValues, base: ExtendedAgent) => {
    const clonedValues = cloneDeep(values);
    const clonedAgent = cloneDeep(base);

    const nextAgent: ExtendedAgent = {
        ...clonedAgent,
        name: clonedValues.agentName,
        instructions: clonedValues.instructions || '',
        handoffDescription: clonedValues.handoffInstructions,
        handoffs: clonedValues.handoffSubagents || [],
        tools: clonedValues.tools || [],
        mcpTools: clonedValues.mcpTools || [],
        enableMemory: clonedValues.enableMemory,
        enableVanillaMode: clonedValues.enableVanillaMode,
    };

    findings.forEach(finding => {
        if (!finding.payload) {
            return;
        }

        if (finding.payload.type === 'promptPatch') {
            // Apply unified diff patch
            const currentInstructions = nextAgent.instructions ?? '';
            try {
                nextAgent.instructions = applyUnifiedDiff(currentInstructions, finding.payload.patch);
            } catch (error) {
                console.error('Failed to apply patch:', error);
                // Fallback: treat as addition
                const trimmed = currentInstructions.trimEnd();
                nextAgent.instructions = trimmed ? `${trimmed}\n\n${finding.payload.patch}` : finding.payload.patch;
            }
        } else if (finding.payload.type === 'instructions') {
            const addition = finding.payload.addition.trim();
            const existing = nextAgent.instructions ?? '';
            if (!existing.includes(addition)) {
                const trimmed = existing.trimEnd();
                nextAgent.instructions = trimmed ? `${trimmed}\n\n${addition}` : addition;
            }
        } else if (finding.payload.type === 'prompt-rewrite') {
            // Complete prompt rewrite - replace entire instructions
            const newPrompt = finding.payload.fullPromptRewrite?.trim() ?? '';
            // Extract the actual prompt from diff format if present
            const promptMatch = newPrompt.match(/^\+(.+)$/m);
            if (promptMatch) {
                // Parse diff format: extract lines starting with +
                const lines = newPrompt
                    .split('\n')
                    .filter(line => line.startsWith('+') && !line.startsWith('+++'))
                    .map(line => line.substring(1));
                nextAgent.instructions = lines.join('\n').trim();
            } else {
                // Use as-is if not in diff format
                nextAgent.instructions = newPrompt;
            }
        } else if (finding.payload.type === 'tool') {
            const updatedTools = Array.isArray(nextAgent.tools) ? [...nextAgent.tools] : [];
            if (!updatedTools.includes(finding.payload.toolName)) {
                updatedTools.push(finding.payload.toolName);
                nextAgent.tools = updatedTools;
            }
        } else if (finding.payload.type === 'newTool') {
            const placeholder = `${finding.payload.toolName}-stub`;
            const updatedTools = Array.isArray(nextAgent.tools) ? [...nextAgent.tools] : [];
            if (!updatedTools.includes(placeholder)) {
                updatedTools.push(placeholder);
                nextAgent.tools = updatedTools;
            }
        }
    });

    return nextAgent;
};
