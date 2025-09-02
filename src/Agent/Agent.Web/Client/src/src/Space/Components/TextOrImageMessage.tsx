import mermaid from 'mermaid';
import { memo, useMemo } from 'react';
import IncidentAlert from '../../Common/Components/IncidentAlert';
import InvestigationSummary from '../../Common/Components/InvestigationSummary';
import InvestigationSummaryPanel from '../../Common/Components/InvestigationSummaryPanel';
import ReactMarkdownComponent from '../../Common/Components/ReactMarkdownComponent';
import { AgentMessageRegex } from '../Contracts/Activities';
import ChangeDiffMessage from './ChangeDiffMessage';
import AgentChart from './Charts';
import MermaidChart from './Mermaid';

// Initialize mermaid with default configuration
mermaid.initialize({
    startOnLoad: false,
    theme: 'neutral',
    flowchart: { useMaxWidth: false },
    securityLevel: 'loose',
});

// Helper function to parse and render markdown with images and mermaid diagrams
const processMessageText = (text: string) => {
    if (!text) return text;

    if (
        !AgentMessageRegex.imageRegex.test(text) &&
        !AgentMessageRegex.mermaidRegex.test(text) &&
        !AgentMessageRegex.chartRegex.test(text) &&
        !AgentMessageRegex.changeDiffRegex.test(text)
    ) {
        return text; // No special content, return original text
    }

    // Reset regex lastIndex properties to ensure we start from the beginning
    AgentMessageRegex.imageRegex.lastIndex = 0;
    AgentMessageRegex.mermaidRegex.lastIndex = 0;
    AgentMessageRegex.chartRegex.lastIndex = 0;
    AgentMessageRegex.changeDiffRegex.lastIndex = 0;

    // Split images, mermaid blocks, and text
    const parts: (string | { type: string; [key: string]: any })[] = [];
    let lastIndex = 0;

    // Function to process a match and add it to the parts array
    const processMatch = (match: RegExpExecArray, type: string) => {
        if (match.index > lastIndex) {
            parts.push(text.substring(lastIndex, match.index));
        }

        if (type === 'image') {
            parts.push({
                type: 'image',
                alt: match[1],
                src: match[2],
            });
        } else if (type === 'mermaid') {
            parts.push({
                type: 'mermaid',
                content: match[1],
            });
        } else if (type === 'chart-data') {
            parts.push({
                type: 'chart-data',
                content: match[0], // Include the entire match with the markers
            });
        } else if (type === 'change-diff') {
            parts.push({
                type: 'change-diff',
                content: match[1],
            });
        }

        lastIndex = match.index + match[0].length;
    };

    // Find all matches and process them in order of appearance
    let imageMatch: RegExpExecArray | null;
    let mermaidMatch: RegExpExecArray | null;
    let chartMatch: RegExpExecArray | null;
    let changeDiffMatch: RegExpExecArray | null;

    // Initialize the first matches
    imageMatch = AgentMessageRegex.imageRegex.exec(text);
    mermaidMatch = AgentMessageRegex.mermaidRegex.exec(text);
    chartMatch = AgentMessageRegex.chartRegex.exec(text);
    changeDiffMatch = AgentMessageRegex.changeDiffRegex.exec(text);

    while (imageMatch || mermaidMatch || chartMatch || changeDiffMatch) {
        // Find the match that appears first in the text
        let firstMatch: RegExpExecArray | null = null;
        let matchType = '';

        if (
            imageMatch &&
            (!mermaidMatch || imageMatch.index < mermaidMatch.index) &&
            (!chartMatch || imageMatch.index < chartMatch.index)
        ) {
            firstMatch = imageMatch;
            matchType = 'image';
            imageMatch = AgentMessageRegex.imageRegex.exec(text);
        } else if (mermaidMatch && (!chartMatch || mermaidMatch.index < chartMatch.index)) {
            firstMatch = mermaidMatch;
            matchType = 'mermaid';
            mermaidMatch = AgentMessageRegex.mermaidRegex.exec(text);
        } else if (chartMatch) {
            firstMatch = chartMatch;
            matchType = 'chart-data';
            chartMatch = AgentMessageRegex.chartRegex.exec(text);
        } else if (changeDiffMatch) {
            firstMatch = changeDiffMatch;
            matchType = 'change-diff';
            changeDiffMatch = AgentMessageRegex.changeDiffRegex.exec(text);
        }

        if (firstMatch) {
            processMatch(firstMatch, matchType);
        }
    }

    // Add any remaining text
    if (lastIndex < text.length) {
        parts.push(text.substring(lastIndex));
    }

    return parts;
};

const TextOrImageMessage = ({ text }: { text: string }) => {
    const regularMessageContent = useMemo(() => {
        const content = processMessageText(text);
        return Array.isArray(content) ? content : text;
    }, [text]);

    const isIncidentAlert = useMemo(() => {
        if (!text) return false;

        const incidentMatch = text.match(AgentMessageRegex.incidentAlertRegex);
        return !!incidentMatch && !!incidentMatch[1];
    }, [text]);

    const isInvestigationSummaries = useMemo(() => {
        if (!text) return false;

        const summariesMatch = text.match(AgentMessageRegex.investigationSummariesRegex);
        if (summariesMatch && summariesMatch[1]) {
            try {
                JSON.parse(summariesMatch[1].trim());
                return true;
            } catch (error) {
                return false;
            }
        }

        return false;
    }, [text]);

    const isInvestigationSummary = useMemo(() => {
        if (!text) return false;

        return !!text.match(AgentMessageRegex.investigationSummaryRegex);
    }, [text]);

    const isAgentChart = useMemo(() => {
        if (!text) return false;
        return (
            AgentMessageRegex.chartRegex.test(text) &&
            text.trim().replace(/\s+/g, ' ').match(AgentMessageRegex.chartRegex)?.[0].length === text.trim().length
        );
    }, [text]);

    // Helper function to extract title from mermaid content
    const extractMermaidTitle = (content: string): string => {
        const lines = content.trim().split('\n');
        if (lines.length === 0) return 'Diagram';

        const firstLine = lines[0];

        if (firstLine.startsWith('%%')) {
            return firstLine.substring(2).trim();
        }

        if (firstLine.startsWith('title:')) {
            return firstLine.substring(6).trim();
        }

        if (firstLine.length < 50 && !firstLine.includes('->') && !firstLine.includes('--')) {
            return firstLine.trim();
        }

        return 'Diagram';
    };

    // Render specific content types
    const RegularMessagePart = ({ part, index }: { part: any; index: number }) => {
        // Plain text markdown
        if (typeof part === 'string') {
            return <ReactMarkdownComponent key={index} content={part} variant="chat" />;
        }

        // Handle different content types
        switch (part.type) {
            case 'image':
                return (
                    <div key={index} style={{ margin: '10px 0' }}>
                        <img src={part.src} alt={part.alt || 'Embedded image'} style={{ maxWidth: '100%', borderRadius: '4px' }} />
                        {part.alt && <div style={{ textAlign: 'center', fontSize: '12px', color: '#666' }}>{part.alt}</div>}
                    </div>
                );

            case 'mermaid':
                return <MermaidChart key={index} chart={part.content} title={extractMermaidTitle(part.content)} />;

            case 'chart-data':
                return <AgentChart key={index} messageText={part.content} />;
            case 'change-diff': {
                const parsedData = parseChangeDiffData(part.content);
                return <ChangeDiffMessage key={index} changeDiffData={parsedData} />;
            }
            default:
                return null;
        }
    };

    if (isIncidentAlert) {
        // Special case: if the whole message is an incident alert, render it directly
        return <IncidentAlert messageText={text} />;
    } else if (isInvestigationSummaries) {
        // Special case: Check for investigation-summaries format (multiple summaries in one container)
        return <InvestigationSummaryPanel messageText={text} />;
    } else if (isInvestigationSummary) {
        // Special case: Check for a single investigation-summary block
        return <InvestigationSummary messageText={text} />;
    } else if (isAgentChart) {
        return <AgentChart messageText={text} />;
    } else if (regularMessageContent) {
        if (!Array.isArray(regularMessageContent)) {
            return <ReactMarkdownComponent content={regularMessageContent} variant="chat" />;
        }

        // Mixed content with special blocks
        return regularMessageContent.map((part, index) => {
            return <RegularMessagePart key={index} part={part} index={index} />;
        });
    }
};

const parseChangeDiffData = (jsonString: string): any => {
    try {
        return JSON.parse(jsonString);
    } catch (error) {
        console.error('Failed to parse change diff data:', error);
        // Return a fallback object
        return {
            id: 'unknown',
            title: 'Invalid Change Diff Data',
            description: 'Failed to parse change diff information',
            correlationId: 'unknown',
            resourceId: 'unknown',
            changes: [],
        };
    }
};

export default memo(TextOrImageMessage);
