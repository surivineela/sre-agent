import { Body1, Body1Strong, Caption1, tokens } from '@fluentui-copilot/react-copilot';
import {
    Accordion,
    AccordionHeader,
    AccordionItem,
    AccordionPanel,
    createMotionComponent,
    makeStyles,
    mergeClasses,
} from '@fluentui/react-components';
import { memo, useEffect, useMemo, useRef, useState } from 'react';
import useIntl from 'react-intl/src/components/useIntl';
import ReactMarkdown from 'react-markdown';
import rehypeRaw from 'rehype-raw';
import remarkGfm from 'remark-gfm';
import { ActivitiesResources } from '../../Strings/SREAgentResources';
import { Reasoning } from '../Contracts/Activities';

interface IReasoningChatMessageProps {
    reasoning: Reasoning;
}

const PulsingBulletPoint = createMotionComponent({
    keyframes: [
        {
            transform: 'scale(1)',
            opacity: 0.7,
        },
        {
            transform: 'scale(1.5)',
            opacity: 1,
        },
        {
            transform: 'scale(1)',
            opacity: 0.7,
        },
    ],
    duration: 1500,
    iterations: Infinity,
    reducedMotion: {
        iterations: 1,
    },
});

const useStyles = makeStyles({
    root: {
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: tokens.borderRadius2XL,
        padding: '5px',
    },
    thinking: {
        background: `linear-gradient(90deg, ${tokens.colorNeutralForeground3}, ${tokens.colorNeutralBackground6}, ${tokens.colorNeutralForeground1})`,
        backgroundSize: '200% 100%',
        backgroundClip: 'text',
        color: 'transparent',
        animation: 'shimmer 2s infinite linear',
        marginBottom: `${tokens.spacingVerticalS}px`,
    },
    item: {
        marginBottom: '15px',
    },
    textEllipsis: {
        textOverflow: 'ellipsis',
    },
    detailsContent: {
        padding: '5px 20px 20px 18px',
        borderRadius: tokens.borderRadius2XL,
    },
    reasoningStep: {
        display: 'flex',
        alignItems: 'flex-start',
        marginBottom: '20px',
        position: 'relative',
        '&:last-child': {
            marginBottom: '0px',
            '&::after': {
                display: 'none', // Hide line after last item
            },
        },
        // Vertical connecting line
        '&:not(:last-child)::after': {
            content: '""',
            position: 'absolute',
            left: '6px', // Center of the 12px dot
            top: '20px', // Start below the dot
            bottom: '-20px', // Connect to next item
            width: '1px',
            backgroundColor: tokens.colorNeutralStroke2,
            zIndex: 0,
        },
    },
    bulletPoint: {
        width: '12px',
        height: '12px',
        borderRadius: '50%',
        backgroundColor: tokens.colorNeutralForeground1, // Fluent UI token for primary foreground
        marginTop: '6px',
        marginRight: '12px',
        flexShrink: 0,
        zIndex: 1,
        position: 'relative',
    },
    stepContent: {
        flex: 1,
    },
    stepTitle: {
        marginBottom: '8px',
        fontWeight: 600,
    },
    stepDescription: {
        color: tokens.colorNeutralForeground3,
        lineHeight: '1.5',
        fontSize: '14px',
        marginLeft: '0px',
    },
    panelContent: {
        padding: '16px 20px',
    },
    headerWithTime: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        width: '100%',
    },
    headerContent: {
        flex: 1,
        minWidth: 0,
    },
    timeCaption: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
        flexShrink: 0,
    },
});

const HeaderComponent = memo(
    ({ children, active, bold, ellipsis }: { children: string | JSX.Element; active: boolean; bold: boolean; ellipsis?: boolean }) => {
        const styles = useStyles();

        const keyframeStyles = `
            @keyframes shimmer {
                0% {
                    background-position: 100% 0;
                }
                100% {
                    background-position: -100% 0;
                }
            }
        `;

        return (
            <>
                <style>{keyframeStyles}</style>
                {bold ? (
                    <Body1Strong
                        wrap={!ellipsis}
                        className={mergeClasses(active ? styles.thinking : undefined, ellipsis ? styles.textEllipsis : undefined)}
                    >
                        {children}
                    </Body1Strong>
                ) : (
                    <Body1
                        wrap={!ellipsis}
                        className={mergeClasses(active ? styles.thinking : undefined, ellipsis ? styles.textEllipsis : undefined)}
                    >
                        {children}
                    </Body1>
                )}
            </>
        );
    }
);

const Header = memo(
    ({
        isMarkdown,
        content,
        active,
        bold,
        ellipsis,
    }: {
        isMarkdown: boolean;
        content: string;
        active: boolean;
        bold: boolean;
        ellipsis?: boolean;
    }) => {
        if (isMarkdown) {
            return <ReactMarkdownComponent content={content} active={active} bold={bold} ellipsis={ellipsis} />;
        } else {
            return (
                <HeaderComponent active={active} bold={bold} ellipsis={ellipsis}>
                    {content}
                </HeaderComponent>
            );
        }
    }
);

const ReactMarkdownComponent = memo(
    ({
        content,
        active,
        bold,
        ellipsis,
        isDetails,
    }: {
        content: string;
        active: boolean;
        bold: boolean;
        ellipsis?: boolean;
        isDetails?: boolean;
    }) => {
        const renderHeader = ({ children }: any) => (
            <HeaderComponent active={active} bold={bold} ellipsis={ellipsis}>
                {children}
            </HeaderComponent>
        );

        const renderParagraph = ({ children }: any) => (
            <Body1 wrap={!ellipsis} style={{ textOverflow: 'ellipsis', color: isDetails ? tokens.colorNeutralForeground4 : undefined }}>
                {children}
            </Body1>
        );

        return (
            <ReactMarkdown
                remarkPlugins={[remarkGfm]}
                rehypePlugins={[rehypeRaw]}
                components={{
                    h1: renderHeader,
                    h2: renderHeader,
                    h3: renderHeader,
                    strong: renderHeader,
                    p: renderParagraph,
                    span: renderParagraph,
                }}
            >
                {content}
            </ReactMarkdown>
        );
    }
);

const THINK_MODE = 'think';

const ReasoningChatMessage = ({ reasoning }: IReasoningChatMessageProps) => {
    const styles = useStyles();
    const intl = useIntl();

    // Track if accordion should be open - default to open only if currently active
    const [openItems, setOpenItems] = useState<string[]>(reasoning.active ? [THINK_MODE] : []);
    const [startTime, setStartTime] = useState<number | null>(reasoning.active ? Date.now() : null);
    const [elapsedTime, setElapsedTime] = useState<string | null>(null);
    const itemCountRef = useRef(reasoning.items.length);

    // Track time and collapse when new messages appear
    useEffect(() => {
        if (reasoning.active) {
            // Thinking is active - ensure accordion is open and track start time
            if (!startTime) {
                setStartTime(Date.now());
            }
            setOpenItems([THINK_MODE]);
        } else if (startTime && !elapsedTime) {
            // Thinking just completed - calculate elapsed time and keep open
            const elapsed = Date.now() - startTime;
            const seconds = Math.round(elapsed / 1000);
            setElapsedTime(seconds > 0 ? `${seconds}s` : '<1s');
            setOpenItems([THINK_MODE]);
        }

        // Detect if new items appeared after this reasoning (indicating a new message)
        // This happens when reasoning is done and the items array grows with new content
        if (!reasoning.active && reasoning.items.length > itemCountRef.current) {
            // New message appeared - collapse the accordion
            setOpenItems([]);
        }

        // Update item count reference
        itemCountRef.current = reasoning.items.length;
    }, [reasoning.active, reasoning.items.length, startTime, elapsedTime]);

    const items = useMemo(() => {
        const getHeaderAndContent = (content: string) => {
            let header: string = '';
            let details: string = '';

            if (content.startsWith('**')) {
                const endIndex = content.substring(2).indexOf('**');
                if (endIndex === -1) {
                    header = content;
                    details = '';
                } else {
                    header = content.substring(0, endIndex + 4);
                    details = content.substring(endIndex + 5);
                }
            } else {
                header = '';
                details = content;
            }
            return { header, details };
        };

        return reasoning.items.map(item => ({ ...getHeaderAndContent(item.content), messageId: item.messageId }));
    }, [reasoning]);

    const reasoningHeader = useMemo(() => {
        const lastMessage = items.length > 0 ? items[items.length - 1].header : items[items.length - 1].details;
        if (!reasoning.active) {
            return {
                content: intl.formatMessage(ActivitiesResources.thoughtProcess),
                isMarkdown: false,
            };
        } else if (lastMessage) {
            return {
                content: lastMessage,
                isMarkdown: true,
            };
        } else {
            return {
                content: intl.formatMessage(ActivitiesResources.thinking),
                isMarkdown: false,
            };
        }
    }, [items, reasoning.active, intl]);

    return (
        <Accordion
            multiple
            collapsible
            openItems={openItems}
            onToggle={(_, data) => setOpenItems(data.openItems as string[])}
            className={styles.root}
        >
            <AccordionItem value={THINK_MODE} key={THINK_MODE}>
                <AccordionHeader expandIconPosition={'end'} size={'small'}>
                    <div className={styles.headerWithTime}>
                        <div className={styles.headerContent}>
                            <Header
                                isMarkdown={reasoningHeader.isMarkdown}
                                content={reasoningHeader.content}
                                active={reasoning.active}
                                bold={true}
                                ellipsis={true}
                            />
                        </div>
                        {elapsedTime && !reasoning.active && <Caption1 className={styles.timeCaption}>{elapsedTime}</Caption1>}
                    </div>
                </AccordionHeader>
                <AccordionPanel>
                    <div className={styles.panelContent}>
                        {items.map((item, index) => (
                            <div key={item.messageId} className={styles.reasoningStep}>
                                {index === items.length - 1 && reasoning.active ? (
                                    <PulsingBulletPoint>
                                        <div className={styles.bulletPoint}></div>
                                    </PulsingBulletPoint>
                                ) : (
                                    <div className={styles.bulletPoint}></div>
                                )}
                                <div className={styles.stepContent}>
                                    <div className={styles.stepTitle}>
                                        <Header
                                            isMarkdown={!!item.header}
                                            content={item.header || intl.formatMessage(ActivitiesResources.reasoning)}
                                            active={false}
                                            bold={false}
                                        />
                                    </div>
                                    {item.details && (
                                        <div className={styles.stepDescription}>
                                            <ReactMarkdownComponent content={item.details} active={false} bold={false} isDetails={true} />
                                        </div>
                                    )}
                                </div>
                            </div>
                        ))}
                    </div>
                </AccordionPanel>
            </AccordionItem>
        </Accordion>
    );
};

export default memo(ReasoningChatMessage);
