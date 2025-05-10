import { Button, Card, Spinner, Text, tokens } from '@fluentui/react-components';
import { CheckmarkCircle16Filled, ChevronDown20Regular, ChevronRight20Regular, DocumentSearch24Regular } from '@fluentui/react-icons';
import { useEffect, useState } from 'react';
import InvestigationSummary from './InvestigationSummary';

// TypeScript interfaces to match the backend C# models
interface SummaryItem {
    title: string;
    summary: string;
    isCollapsed: boolean;
}

interface InvestigationSummaries {
    containerTitle: string;
    summaries: SummaryItem[];
}

interface Summary {
    id: string;
    title: string;
    content: string;
}

export interface InvestigationSummaryPanelProps {
    messageText: string; // Raw message text for direct parsing
}

const InvestigationSummaryPanel = ({ messageText }: InvestigationSummaryPanelProps) => {
    const [isCollapsed, setIsCollapsed] = useState(false);
    const [isProcessing, setIsProcessing] = useState(true);
    const [parsedData, setParsedData] = useState<{
        title: string;
        summaries: Summary[];
    }>({ title: 'Observations', summaries: [] });

    // Parse message text
    useEffect(() => {
        try {
            const regex = /<investigation-summaries>([\s\S]*?)<\/investigation-summaries>/;
            const match = messageText.match(regex);

            if (match && match[1]) {
                const content = match[1].trim();
                const data = JSON.parse(content) as InvestigationSummaries;

                const title = data.containerTitle || 'Observations';
                const summaries = data.summaries.map((item, index) => {
                    // Create investigation-summary blocks for individual summaries using the XML format
                    const summaryContent = JSON.stringify({
                        title: item.title,
                        summary: item.summary,
                        isCollapsed: item.isCollapsed,
                    });

                    return {
                        id: `summary-${index}`,
                        title: item.title,
                        content: `<investigation-summary>${summaryContent}</investigation-summary>`,
                    };
                });

                setParsedData({ title, summaries });
            }
        } catch (error) {
            console.error('Failed to parse investigation summaries:', error);
        }
    }, [messageText]);

    // Simulate processing with spinner
    useEffect(() => {
        const processingTime = 2000 + Math.random() * 2000; // 2-4 seconds
        const timer = setTimeout(() => {
            setIsProcessing(false);
        }, processingTime);

        return () => clearTimeout(timer);
    }, []);

    return (
        <Card
            style={{
                marginTop: '16px',
                marginBottom: '16px',
                border: `1px solid ${tokens.colorNeutralStroke1}`,
                borderRadius: '8px',
                padding: '0',
                boxShadow: tokens.shadow4,
                overflow: 'hidden',
            }}
        >
            <div
                style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    padding: '12px 16px',
                    borderBottom: isCollapsed ? 'none' : `1px solid ${tokens.colorNeutralStroke1}`,
                    backgroundColor: tokens.colorNeutralBackground2,
                    cursor: 'pointer',
                    minHeight: '40px',
                }}
                onClick={() => setIsCollapsed(!isCollapsed)}
            >
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <DocumentSearch24Regular />
                    <Text weight="semibold" size={400}>
                        {parsedData.title}
                    </Text>
                </div>

                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    {isProcessing ? (
                        <Spinner size="tiny" />
                    ) : (
                        <CheckmarkCircle16Filled style={{ color: tokens.colorPaletteGreenForeground1 }} />
                    )}
                    {isCollapsed ? <ChevronRight20Regular /> : <ChevronDown20Regular />}
                </div>
            </div>

            {!isCollapsed && (
                <div style={{ backgroundColor: tokens.colorNeutralBackground1, padding: '16px' }}>
                    {parsedData.summaries.map(summary => (
                        <div key={summary.id}>
                            <InvestigationSummary messageText={summary.content} />
                        </div>
                    ))}
                    {parsedData.summaries.length > 0 && (
                        <Button onClick={() => setIsCollapsed(true)} appearance="subtle" style={{ marginTop: '8px' }}>
                            Collapse
                        </Button>
                    )}
                </div>
            )}
        </Card>
    );
};

export default InvestigationSummaryPanel;
