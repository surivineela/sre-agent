import { makeStyles, Text, tokens } from '@fluentui/react-components';
import { DismissRegular } from '@fluentui/react-icons';
import React from 'react';
import ReactMarkdown from 'react-markdown';
import rehypeRaw from 'rehype-raw';
import remarkGfm from 'remark-gfm';
import { HypothesisStep, InitialInvestigationStep } from '../../../Common/Contracts/Azure/AgentTaskDevTypes';

const usePanelStyles = makeStyles({
    panel: {
        position: 'absolute',
        top: 0,
        right: 0,
        width: '50%',
        height: '100%',
        backgroundColor: tokens.colorNeutralBackground1,
        borderLeft: `1px solid ${tokens.colorNeutralStroke2}`,
        boxShadow: tokens.shadow16,
        display: 'flex',
        flexDirection: 'column',
        zIndex: 100,
        transform: 'translateX(100%)',
        transition: 'transform 0.3s ease-in-out',
    },
    panelOpen: {
        transform: 'translateX(0)',
    },
    header: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: '16px 20px',
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: tokens.colorNeutralBackground2,
        flexShrink: 0,
    },
    title: {
        fontSize: '16px',
        fontWeight: '600',
        color: tokens.colorNeutralForeground1,
        margin: 0,
        flex: 1,
    },
    closeButton: {
        backgroundColor: 'transparent',
        border: 'none',
        cursor: 'pointer',
        padding: '8px',
        borderRadius: tokens.borderRadiusSmall,
        color: tokens.colorNeutralForeground2,
        '&:hover': {
            backgroundColor: tokens.colorNeutralBackground3,
        },
    },
    content: {
        flex: 1,
        overflow: 'auto',
        padding: '20px',
    },
    description: {
        fontSize: '14px',
        color: tokens.colorNeutralForeground2,
        lineHeight: '1.6',
        marginBottom: '24px',
        padding: '16px',
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusSmall,
        border: `1px solid ${tokens.colorNeutralStroke1}`,
    },
    stepsSection: {
        marginTop: '20px',
    },
    stepsTitle: {
        fontSize: '16px',
        fontWeight: '600',
        color: tokens.colorNeutralForeground1,
        marginBottom: '16px',
    },
    stepItem: {
        marginBottom: '16px',
        padding: '16px',
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusSmall,
        border: `1px solid ${tokens.colorNeutralStroke1}`,
    },
    stepTitle: {
        fontSize: '14px',
        fontWeight: '600',
        color: tokens.colorNeutralForeground1,
        marginBottom: '8px',
    },
    stepSummary: {
        fontSize: '13px',
        color: tokens.colorNeutralForeground2,
        lineHeight: '1.5',
    },
    stepStatus: {
        fontSize: '11px',
        fontWeight: '500',
        padding: '4px 8px',
        borderRadius: tokens.borderRadiusSmall,
        marginTop: '8px',
        display: 'inline-block',
    },
    statusComplete: {
        backgroundColor: tokens.colorPaletteGreenBackground2,
        color: tokens.colorPaletteGreenForeground1,
        border: `1px solid ${tokens.colorPaletteGreenBorder1}`,
    },
    statusInProgress: {
        backgroundColor: tokens.colorBrandBackground2,
        color: tokens.colorBrandForeground2,
        border: `1px solid ${tokens.colorBrandStroke2}`,
    },
    statusNotStarted: {
        backgroundColor: tokens.colorNeutralBackground3,
        color: tokens.colorNeutralForeground2,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    noStepsMessage: {
        fontSize: '14px',
        color: tokens.colorNeutralForeground3,
        fontStyle: 'italic',
        textAlign: 'center',
        padding: '20px',
    },
    markdownHeading: {
        marginTop: '20px',
        marginBottom: '10px',
        color: tokens.colorNeutralForeground1,
        fontWeight: '600',
    },
    markdownH1: {
        fontSize: '24px',
        fontWeight: '700',
        marginTop: '24px',
        marginBottom: '16px',
        color: tokens.colorNeutralForeground1,
    },
    markdownH2: {
        fontSize: '20px',
        fontWeight: '600',
        marginTop: '20px',
        marginBottom: '12px',
        color: tokens.colorNeutralForeground1,
    },
    markdownH3: {
        fontSize: '18px',
        fontWeight: '600',
        marginTop: '16px',
        marginBottom: '10px',
        color: tokens.colorNeutralForeground1,
    },
    markdownH4: {
        fontSize: '16px',
        fontWeight: '600',
        marginTop: '14px',
        marginBottom: '8px',
        color: tokens.colorNeutralForeground1,
    },
    markdownH5: {
        fontSize: '14px',
        fontWeight: '600',
        marginTop: '12px',
        marginBottom: '6px',
        color: tokens.colorNeutralForeground1,
    },
    markdownH6: {
        fontSize: '13px',
        fontWeight: '600',
        marginTop: '10px',
        marginBottom: '4px',
        color: tokens.colorNeutralForeground1,
    },
    markdownList: {
        marginLeft: '20px',
        marginBottom: '10px',
    },
    markdownListItem: {
        marginBottom: '5px',
    },
    markdownCode: {
        backgroundColor: tokens.colorNeutralBackground3,
        padding: '2px 4px',
        borderRadius: tokens.borderRadiusSmall,
        fontSize: '0.9em',
    },
    markdownPre: {
        backgroundColor: tokens.colorNeutralBackground3,
        padding: '10px',
        borderRadius: tokens.borderRadiusSmall,
        overflowX: 'auto',
        fontSize: '0.9em',
    },
    markdownBlockquote: {
        borderLeft: `4px solid ${tokens.colorNeutralStroke1}`,
        paddingLeft: '10px',
        marginLeft: '0',
        marginRight: '0',
        marginBottom: '10px',
        fontStyle: 'italic',
    },
    markdownStrong: {
        fontWeight: 'bold',
    },
    markdownEmphasis: {
        fontStyle: 'italic',
    },
    markdownLink: {
        color: tokens.colorBrandForeground1,
        textDecoration: 'underline',
    },
});

interface DetailsPanelProps {
    isOpen: boolean;
    onClose: () => void;
    title: string;
    description: string;
    nodeType: 'phase' | 'hypothesis';
    steps?: HypothesisStep[] | InitialInvestigationStep[];
}

const getStepStatusClass = (status: string, styles: ReturnType<typeof usePanelStyles>) => {
    switch (status.toLowerCase()) {
        case 'complete':
        case 'completed':
            return `${styles.stepStatus} ${styles.statusComplete}`;
        case 'inprogress':
        case 'in_progress':
            return `${styles.stepStatus} ${styles.statusInProgress}`;
        default:
            return `${styles.stepStatus} ${styles.statusNotStarted}`;
    }
};

const getStepStatusText = (status: string) => {
    switch (status.toLowerCase()) {
        case 'complete':
        case 'completed':
            return 'Completed';
        case 'inprogress':
        case 'in_progress':
            return 'In Progress';
        default:
            return 'Not Started';
    }
};

export const DetailsPanel: React.FC<DetailsPanelProps> = ({ isOpen, onClose, title, description, nodeType, steps = [] }) => {
    const styles = usePanelStyles();

    const hasSteps = steps && steps.length > 0;

    return (
        <div className={`${styles.panel} ${isOpen ? styles.panelOpen : ''}`}>
            <div className={styles.header}>
                <Text className={styles.title}>{title}</Text>
                <button className={styles.closeButton} onClick={onClose}>
                    <DismissRegular />
                </button>
            </div>

            <div className={styles.content}>
                <div className={styles.description}>
                    <ReactMarkdown
                        remarkPlugins={[remarkGfm]}
                        rehypePlugins={[rehypeRaw]}
                        components={{
                            p: ({ children }) => <Text as="p">{children}</Text>,
                            h1: ({ children }) => <h1 className={styles.markdownH1}>{children}</h1>,
                            h2: ({ children }) => <h2 className={styles.markdownH2}>{children}</h2>,
                            h3: ({ children }) => <h3 className={styles.markdownH3}>{children}</h3>,
                            h4: ({ children }) => <h4 className={styles.markdownH4}>{children}</h4>,
                            h5: ({ children }) => <h5 className={styles.markdownH5}>{children}</h5>,
                            h6: ({ children }) => <h6 className={styles.markdownH6}>{children}</h6>,
                            ul: ({ children }) => <ul className={styles.markdownList}>{children}</ul>,
                            ol: ({ children }) => <ol className={styles.markdownList}>{children}</ol>,
                            li: ({ children }) => <li className={styles.markdownListItem}>{children}</li>,
                            code: ({ children, className }) => (
                                <code className={`${styles.markdownCode} ${className || ''}`}>{children}</code>
                            ),
                            pre: ({ children }) => <pre className={styles.markdownPre}>{children}</pre>,
                            blockquote: ({ children }) => <blockquote className={styles.markdownBlockquote}>{children}</blockquote>,
                            strong: ({ children }) => <strong className={styles.markdownStrong}>{children}</strong>,
                            em: ({ children }) => <em className={styles.markdownEmphasis}>{children}</em>,
                            a: ({ children, href }) => (
                                <a href={href} className={styles.markdownLink} target="_blank" rel="noopener noreferrer">
                                    {children}
                                </a>
                            ),
                        }}
                    >
                        {description}
                    </ReactMarkdown>
                </div>

                {hasSteps && (
                    <div className={styles.stepsSection}>
                        <Text className={styles.stepsTitle}>{nodeType === 'phase' ? 'Investigation Steps' : 'Validation Steps'}</Text>
                        {steps.map((step, index) => {
                            // Handle different step types
                            const isInitialInvestigationStep = 'title' in step;
                            const stepTitle = isInitialInvestigationStep
                                ? (step as InitialInvestigationStep).title
                                : (step as HypothesisStep).summary;
                            const stepSummary = isInitialInvestigationStep
                                ? (step as InitialInvestigationStep).summary
                                : (step as HypothesisStep).details;
                            const stepStatus = isInitialInvestigationStep ? (step as InitialInvestigationStep).status : undefined;

                            return (
                                <div key={index} className={styles.stepItem}>
                                    <div>
                                        <Text className={styles.stepTitle}>
                                            {index + 1}. {stepTitle}
                                        </Text>
                                    </div>
                                    <Text className={styles.stepSummary}>
                                        <ReactMarkdown
                                            remarkPlugins={[remarkGfm]}
                                            rehypePlugins={[rehypeRaw]}
                                            components={{
                                                p: ({ children }) => <span>{children}</span>,
                                                h1: ({ children }) => <h1 className={styles.markdownH1}>{children}</h1>,
                                                h2: ({ children }) => <h2 className={styles.markdownH2}>{children}</h2>,
                                                h3: ({ children }) => <h3 className={styles.markdownH3}>{children}</h3>,
                                                h4: ({ children }) => <h4 className={styles.markdownH4}>{children}</h4>,
                                                h5: ({ children }) => <h5 className={styles.markdownH5}>{children}</h5>,
                                                h6: ({ children }) => <h6 className={styles.markdownH6}>{children}</h6>,
                                                ul: ({ children }) => <ul className={styles.markdownList}>{children}</ul>,
                                                ol: ({ children }) => <ol className={styles.markdownList}>{children}</ol>,
                                                li: ({ children }) => <li className={styles.markdownListItem}>{children}</li>,
                                                code: ({ children, className }) => (
                                                    <code className={`${styles.markdownCode} ${className || ''}`}>{children}</code>
                                                ),
                                                pre: ({ children }) => <pre className={styles.markdownPre}>{children}</pre>,
                                                blockquote: ({ children }) => (
                                                    <blockquote className={styles.markdownBlockquote}>{children}</blockquote>
                                                ),
                                                strong: ({ children }) => <strong className={styles.markdownStrong}>{children}</strong>,
                                                em: ({ children }) => <em className={styles.markdownEmphasis}>{children}</em>,
                                                a: ({ children, href }) => (
                                                    <a
                                                        href={href}
                                                        className={styles.markdownLink}
                                                        target="_blank"
                                                        rel="noopener noreferrer"
                                                    >
                                                        {children}
                                                    </a>
                                                ),
                                            }}
                                        >
                                            {stepSummary}
                                        </ReactMarkdown>
                                    </Text>
                                    {stepStatus && (
                                        <div className={getStepStatusClass(stepStatus, styles)}>{getStepStatusText(stepStatus)}</div>
                                    )}
                                </div>
                            );
                        })}
                    </div>
                )}
            </div>
        </div>
    );
};
