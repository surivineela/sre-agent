import {
    Accordion,
    AccordionHeader,
    AccordionItem,
    AccordionPanel,
    AccordionToggleEventHandler,
    Button,
    DrawerBody,
    DrawerHeader,
    DrawerHeaderTitle,
    Link,
    makeStyles,
    OverlayDrawer,
    Subtitle1,
    Subtitle2,
    Text,
    Title3,
    tokens,
    useRestoreFocusSource,
} from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { memo, useState } from 'react';
import ReactMarkdown from 'react-markdown';
import rehypeRaw from 'rehype-raw';
import remarkGfm from 'remark-gfm';
import { HypothesisStep, InitialInvestigationStep, TreeNodeType } from '../../../Common/Contracts/DataPlane/AgentTask';
import NodeStatusPill from '../../Components/AgentTask/NodeStatusPill';
import { GraphFlowNode } from '../../Contracts/Activities';

interface IAgentTaskDetailsPanelProps {
    node: GraphFlowNode | null;
    isOpen: boolean;
    onClose: () => void;
}

const useStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'flex-start',
        gap: '40px',
    },
    summaryRoot: {
        margin: `${tokens.spacingVerticalL} 0px 0px 0px`,
        padding: `0px ${tokens.spacingHorizontalM}`,
        borderLeft: `1px solid ${tokens.colorNeutralStroke1}`,
    },
    stepsRoot: {
        width: '100%',
        height: '100%',
    },
    stepsTitle: {
        padding: `10px ${tokens.spacingHorizontalM}`,
        lineHeight: tokens.lineHeightBase400,
        display: 'flex',
        alignItems: 'center',
    },
    stepsDescription: {
        padding: '10px 0px 20px 28px',
    },
    accordion: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        marginTop: '10px',
    },
    accordionItem: {
        border: `1px solid ${tokens.colorNeutralStroke1}`,
        borderRadius: tokens.borderRadiusMedium,
    },
    accordionHeader: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'flex-start',
        gap: tokens.spacingHorizontalS,
    },
    codeBlock: {
        backgroundColor: tokens.colorNeutralBackground6,
        fontFamily: tokens.fontFamilyMonospace,
        fontSize: tokens.fontSizeBase200,
        display: 'inline-block',
        padding: '2px 4px',
        borderRadius: tokens.borderRadiusSmall,
    },
    codeBlockInPre: {
        backgroundColor: tokens.colorTransparentBackground,
        fontFamily: tokens.fontFamilyMonospace,
        fontSize: tokens.fontSizeBase200,
        display: 'block',
    },
    preBlock: {
        overflowX: 'auto',
        overflowY: 'hidden',
        backgroundColor: tokens.colorNeutralBackground6,
        borderRadius: tokens.borderRadiusSmall,
        padding: '15px',
    },
    markdownBlockquote: {
        borderLeft: `4px solid ${tokens.colorNeutralStroke1}`,
        paddingLeft: '10px',
        marginLeft: '0',
        marginRight: '0',
        marginBottom: '10px',
        fontStyle: 'italic',
    },
});

const AgentTaskDetailsPanel = ({ node, isOpen, onClose }: IAgentTaskDetailsPanelProps) => {
    const styles = useStyles();

    const [stepsOpenItems, setStepsOpenItems] = useState<number[]>([]);

    const toggleStep: AccordionToggleEventHandler<number> = (_, item) => {
        setStepsOpenItems(item.openItems);
    };

    const getSteps = (): { title: string; description: string; status?: string; showStatus: boolean }[] => {
        const gatheringContextSteps: InitialInvestigationStep[] = node?.data.gatheringContextSteps || [];
        const steps: HypothesisStep[] | InitialInvestigationStep[] = node?.data.steps || [];

        if (gatheringContextSteps.length > 0) {
            return gatheringContextSteps.map(step => ({
                title: step.title,
                description: step.summary,
                status: step.status,
                showStatus: true,
            }));
        } else if (steps.length > 0) {
            return steps.map(step => ({
                title: step.summary,
                description: (step as HypothesisStep).details,
                status: undefined,
                showStatus: false,
            }));
        }

        return [];
    };

    const steps = getSteps();

    const restoreFocusSourceAttributes = useRestoreFocusSource();

    const ReactMarkdownComponent = ({ text }: { text?: string | null }) => {
        return (
            <ReactMarkdown
                remarkPlugins={[remarkGfm]}
                rehypePlugins={[rehypeRaw]}
                components={{
                    p: ({ children }) => <Text as="p">{children}</Text>,
                    h1: ({ children }) => (
                        <Title3 as={'h1'} block>
                            {children}
                        </Title3>
                    ),
                    h2: ({ children }) => (
                        <Subtitle1 as={'h2'} block>
                            {children}
                        </Subtitle1>
                    ),
                    h3: ({ children }) => (
                        <Subtitle2 as={'h3'} block>
                            {children}
                        </Subtitle2>
                    ),
                    h4: ({ children }) => (
                        <Subtitle2 as={'h4'} block>
                            {children}
                        </Subtitle2>
                    ),
                    h5: ({ children }) => (
                        <Subtitle2 as={'h5'} block>
                            {children}
                        </Subtitle2>
                    ),
                    h6: ({ children }) => (
                        <Subtitle2 as={'h6'} block>
                            {children}
                        </Subtitle2>
                    ),
                    code: (props: any) => {
                        // Check if this code element is inside a pre element (code block)
                        const isInPre = props.node?.parent?.tagName === 'pre';
                        const className = isInPre ? styles.codeBlockInPre : styles.codeBlock;
                        return <code className={className}>{props.children}</code>;
                    },
                    pre: (props: any) => {
                        return <pre className={styles.preBlock}>{props.children}</pre>;
                    },
                    blockquote: ({ children }) => <blockquote className={styles.markdownBlockquote}>{children}</blockquote>,
                    strong: ({ children }) => (
                        <Text as={'strong'} weight={'bold'}>
                            {children}
                        </Text>
                    ),
                    em: ({ children }) => (
                        <Text as={'em'} italic>
                            {children}
                        </Text>
                    ),
                    a: ({ children, href }) => (
                        <Link href={href} target="_blank" rel="noopener noreferrer">
                            {children}
                        </Link>
                    ),
                }}
            >
                {text}
            </ReactMarkdown>
        );
    };

    return (
        <OverlayDrawer open={isOpen && !!node} position={'end'} modalType={'non-modal'} size={'medium'} {...restoreFocusSourceAttributes}>
            <DrawerHeader>
                <DrawerHeaderTitle action={<Button appearance="subtle" aria-label="Close" icon={<Dismiss24Regular />} onClick={onClose} />}>
                    {node?.data.title}
                </DrawerHeaderTitle>
            </DrawerHeader>
            <DrawerBody>
                <div className={styles.root}>
                    <div className={styles.summaryRoot}>
                        <ReactMarkdownComponent text={node?.data.description} />
                    </div>
                    {steps.length > 0 ? (
                        <div className={styles.stepsRoot}>
                            <div className={styles.stepsTitle}>
                                <Subtitle2>{node?.type === TreeNodeType.Hypothesis ? 'Validation Steps' : 'Investigation Steps'}</Subtitle2>
                            </div>
                            <Accordion openItems={stepsOpenItems} onToggle={toggleStep} multiple collapsible className={styles.accordion}>
                                {steps.map((step, index) => {
                                    return (
                                        <AccordionItem value={index} key={index} className={styles.accordionItem}>
                                            <AccordionHeader>
                                                <div className={styles.accordionHeader}>
                                                    {step.showStatus && <NodeStatusPill status={step.status} showIcon={false} />}
                                                    {step.title}
                                                </div>
                                            </AccordionHeader>
                                            <AccordionPanel className={styles.stepsDescription}>
                                                <ReactMarkdownComponent text={step.description} />
                                            </AccordionPanel>
                                        </AccordionItem>
                                    );
                                })}
                            </Accordion>
                        </div>
                    ) : null}
                </div>
            </DrawerBody>
        </OverlayDrawer>
    );
};

export default memo(AgentTaskDetailsPanel);
