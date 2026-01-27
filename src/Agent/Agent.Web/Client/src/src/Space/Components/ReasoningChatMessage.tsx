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
import { ClockRegular } from '@fluentui/react-icons';
import { memo, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import useIntl from 'react-intl/src/components/useIntl';
import ReactMarkdown from 'react-markdown';
import rehypeRaw from 'rehype-raw';
import remarkGfm from 'remark-gfm';
import { getSafeDateTime } from '../../Common/Helpers/Date';
import { useScrollableComponentStyles } from '../../Common/Styles/Scrollable';
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
    rootCollapsed: {
        cursor: 'pointer',
        transition: 'background-color 0.15s ease',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1,
        },
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
        marginBottom: '14px',
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
            left: '5px', // Center of the 10px dot
            top: '18px', // Start below the dot
            bottom: '-16px', // Connect to next item
            width: '1px',
            backgroundColor: tokens.colorNeutralStroke2,
            zIndex: 0,
        },
    },
    bulletPoint: {
        width: '10px',
        height: '10px',
        borderRadius: '50%',
        backgroundColor: tokens.colorNeutralForeground1, // Fluent UI token for primary foreground
        margin: '5px 10px 5px 0px',
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
        padding: '12px 16px',
        overflowY: 'auto',
        maxHeight: '200px',
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
        color: tokens.colorNeutralForeground1,
        fontSize: tokens.fontSizeBase200,
        flexShrink: 0,
        display: 'inline-flex',
        alignItems: 'center',
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
    const { scrollable } = useScrollableComponentStyles();
    const intl = useIntl();

    // Track if accordion should be open - default to open only if currently active
    const [openItems, setOpenItems] = useState<string[]>(reasoning.active ? [THINK_MODE] : []);
    const itemCountRef = useRef(reasoning.items.length);
    const wasActiveRef = useRef(reasoning.active);
    const scrollContainerRef = useRef<HTMLDivElement>(null);
    const [autoScrollEnabled, setAutoScrollEnabled] = useState(true);

    // Extract timestamps as stable dependencies to ensure recalculation when items are updated
    const firstTimestamp = reasoning.items.length > 0 ? reasoning.items[0].timestamp : null;
    const lastTimestamp = reasoning.items.length > 0 ? reasoning.items[reasoning.items.length - 1].timestamp : null;

    // Calculate elapsed time from min/max of all reasoning item timestamps
    const elapsedTime = useMemo(() => {
        if (reasoning.active || reasoning.items.length === 0) {
            return null;
        }

        // Get all valid timestamps
        const timestamps = reasoning.items
            .map(item => (item.timestamp ? getSafeDateTime(item.timestamp).getTime() : null))
            .filter((t): t is number => t !== null);

        // Show <1s when there's only 1 timestamp or no valid timestamps
        if (timestamps.length < 2) {
            return '<1s';
        }

        const startTime = Math.min(...timestamps);
        const endTime = Math.max(...timestamps);
        const elapsed = endTime - startTime;
        const seconds = Math.round(elapsed / 1000);
        return seconds > 0 ? `${seconds}s` : '<1s';
    }, [reasoning.active, reasoning.items.length, firstTimestamp, lastTimestamp]);

    // Check if scroll is at bottom (within threshold)
    const isScrolledToBottom = useCallback(() => {
        const container = scrollContainerRef.current;
        if (!container) return true;
        const threshold = 50;
        return container.scrollHeight - container.scrollTop - container.clientHeight < threshold;
    }, []);

    // Handle scroll events to detect if user scrolled away from bottom
    const handleScroll = useCallback(() => {
        const atBottom = isScrolledToBottom();
        setAutoScrollEnabled(atBottom);
    }, [isScrolledToBottom]);

    // Auto-scroll to bottom when new items are added (if auto-scroll is enabled)
    useEffect(() => {
        if (autoScrollEnabled && scrollContainerRef.current && reasoning.active) {
            scrollContainerRef.current.scrollTop = scrollContainerRef.current.scrollHeight;
        }
    }, [reasoning.items.length, autoScrollEnabled, reasoning.active]);

    // Track state changes for accordion behavior
    useEffect(() => {
        if (reasoning.active) {
            // Thinking is active - ensure accordion is open
            setOpenItems([THINK_MODE]);
        } else {
            // Thinking is not active - collapse after a short delay for smooth transition
            const timer = setTimeout(() => {
                setOpenItems([]);
            }, 500);
            return () => clearTimeout(timer);
        }

        // Update refs
        wasActiveRef.current = reasoning.active;
        itemCountRef.current = reasoning.items.length;
    }, [reasoning.active, reasoning.items.length]);

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
        // Get the first item's header as the summary
        const firstItemHeader = items.length > 0 ? items[0].header : '';
        const lastItemHeader = items.length > 0 ? items[items.length - 1].header : '';

        if (!reasoning.active) {
            // When completed: show dynamic summary (first item's header)
            if (firstItemHeader) {
                return {
                    content: firstItemHeader,
                    isMarkdown: true,
                };
            }
            // Fallback to "Thought process" if no header available
            return {
                content: intl.formatMessage(ActivitiesResources.thoughtProcess),
                isMarkdown: false,
            };
        } else if (lastItemHeader) {
            // During active thinking: show last item's header (streaming progress)
            return {
                content: lastItemHeader,
                isMarkdown: true,
            };
        } else {
            return {
                content: intl.formatMessage(ActivitiesResources.thinking),
                isMarkdown: false,
            };
        }
    }, [items, reasoning.active, intl]);

    const isCollapsed = !openItems.includes(THINK_MODE);

    return (
        <Accordion
            multiple
            collapsible
            openItems={openItems}
            onToggle={(_, data) => setOpenItems(data.openItems as string[])}
            className={mergeClasses(styles.root, isCollapsed && styles.rootCollapsed)}
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
                        {elapsedTime && !reasoning.active && (
                            <Caption1 className={styles.timeCaption}>
                                <ClockRegular style={{ marginRight: '4px', color: tokens.colorNeutralForeground3 }} />
                                {elapsedTime}
                            </Caption1>
                        )}
                    </div>
                </AccordionHeader>
                <AccordionPanel>
                    <div ref={scrollContainerRef} onScroll={handleScroll} className={mergeClasses(styles.panelContent, scrollable)}>
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
