import { Button, Card, Spinner, Text, tokens } from '@fluentui/react-components';
import { CheckmarkCircle16Filled, ChevronDown20Regular, ChevronRight20Regular, DocumentSearch24Regular } from '@fluentui/react-icons';
import { Collapse } from '@fluentui/react-motion-components-preview';
import { useEffect, useState } from 'react';
import ReactMarkdown from 'react-markdown';

export interface InvestigationSummaryProps {
    messageText: string;
}

const InvestigationSummary = ({ messageText }: InvestigationSummaryProps) => {
    const [isProcessing, setIsProcessing] = useState(true);
    const [isCollapsed, setIsCollapsed] = useState(false);

    const parseContent = () => {
        try {
            const regex = /```investigation-summary\s+([\s\S]*?)```/;
            const match = messageText.match(regex);

            if (!match || !match[1]) {
                return {
                    title: 'Investigation',
                    summary: 'No content found',
                    isCollapsed: false,
                };
            }

            const content = match[1].trim();

            try {
                const jsonData = JSON.parse(content);
                return {
                    title: jsonData.title || 'Investigation',
                    summary: jsonData.summary || '',
                    isCollapsed: jsonData.isCollapsed === true,
                };
            } catch (e) {
                // If not JSON, use simple line parsing
                const lines = content.split('\n');
                if (lines.length < 2) {
                    return {
                        title: 'Investigation',
                        summary: content,
                        isCollapsed: false,
                    };
                }

                const title = lines[0];
                const summary = lines.slice(2).join('\n');

                // Check if any line contains isCollapsed:true
                const isCollapsedLine = lines.find(
                    line => line.toLowerCase().includes('iscollapsed:true') || line.toLowerCase().includes('collapsed:true')
                );

                return {
                    title,
                    summary,
                    isCollapsed: !!isCollapsedLine,
                };
            }
        } catch (error) {
            console.error('Failed to parse investigation summary:', error);
            return {
                title: 'Investigation Error',
                summary: 'Failed to parse investigation content',
                isCollapsed: false,
            };
        }
    };

    const parsedContent = parseContent();
    const { title, summary, isCollapsed: shouldBeCollapsed } = parsedContent;

    // Set initial collapsed state based on parsed content
    useEffect(() => {
        setIsCollapsed(shouldBeCollapsed);
    }, [shouldBeCollapsed]);

    // Simulate processing with spinner
    useEffect(() => {
        const processingTime = 2000 + Math.random() * 2000; // 2-4 seconds
        const timer = setTimeout(() => {
            setIsProcessing(false);
        }, processingTime);

        return () => clearTimeout(timer);
    }, []);

    // Hardcoded to completed status
    const statusColor = tokens.colorPaletteGreenForeground1;

    return (
        <Card
            style={{
                marginTop: '16px',
                marginBottom: '16px',
                border: `1px solid ${tokens.colorNeutralStroke1}`,
                borderRadius: '8px',
                padding: '0',
                boxShadow: tokens.shadow4,
            }}
        >
            <div
                style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    padding: '12px 16px',
                    borderBottom: isCollapsed ? 'none' : `1px solid ${tokens.colorNeutralStroke1}`,
                    cursor: 'pointer',
                    backgroundColor: tokens.colorNeutralBackground2,
                }}
                onClick={() => setIsCollapsed(!isCollapsed)}
            >
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <DocumentSearch24Regular />
                    <Text weight="semibold" size={400}>
                        {title}
                    </Text>
                    {isProcessing ? (
                        <Spinner size="tiny" style={{ marginLeft: '4px' }} />
                    ) : (
                        <CheckmarkCircle16Filled style={{ color: statusColor, marginLeft: '4px' }} />
                    )}
                </div>
                <div>{isCollapsed ? <ChevronRight20Regular /> : <ChevronDown20Regular />}</div>
            </div>

            <Collapse visible={!isCollapsed}>
                <div style={{ padding: '16px', backgroundColor: tokens.colorNeutralBackground1 }}>
                    <ReactMarkdown>{summary}</ReactMarkdown>
                    <Button onClick={() => setIsCollapsed(true)} appearance="subtle" style={{ marginTop: '8px' }}>
                        Collapse
                    </Button>
                </div>
            </Collapse>
        </Card>
    );
};

export default InvestigationSummary;
