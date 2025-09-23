import {
    Accordion,
    AccordionHeader,
    AccordionItem,
    AccordionPanel,
    AccordionToggleEventHandler,
    Button,
    makeStyles,
    Subtitle2,
    tokens,
} from '@fluentui/react-components';
import { Dialog, DialogBody, DialogContent, DialogSurface, DialogTitle } from '@fluentui/react-dialog';
import { ChevronDownUpRegular, ChevronUpDownRegular, Dismiss24Regular } from '@fluentui/react-icons';
import { memo, useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import ReactMarkdownComponent from '../../../Common/Components/ReactMarkdownComponent';
import { HypothesisStep, InitialInvestigationStep, TreeNodeType } from '../../../Common/Contracts/DataPlane/AgentTask';
import { useScrollableComponentStyles } from '../../../Common/Styles/Scrollable';
import { SreAgentResources as SREAgentResources } from '../../../Strings/SREAgentResources';
import { GraphFlowNode } from '../../Contracts/Activities';

interface IAgentTaskDetailsPanelProps {
    node: GraphFlowNode | null;
    isOpen: boolean;
    onClose: () => void;
}

const useStyles = makeStyles({
    dialogSurface: {
        width: '90vw',
        maxWidth: '775px',
        height: '90vh',
        maxHeight: '800px',
        display: 'flex',
        flexDirection: 'column',
    },
    dialogBody: {
        display: 'flex',
        flexDirection: 'column',
        flexGrow: 1,
        minHeight: 0,
        overflow: 'hidden',
    },
    dialogContent: {
        flexGrow: 1,
        minHeight: 0,
        overflowY: 'auto',
        display: 'flex',
        flexDirection: 'column',
    },
    dialogTitle: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        width: '100%',
        gap: tokens.spacingHorizontalM,
    },
    root: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'flex-start',
        gap: '5px',
        paddingRight: tokens.spacingHorizontalM,
    },
    summaryRoot: {
        margin: `${tokens.spacingVerticalL} 0px 0px 0px`,
    },
    stepsRoot: {
        width: '100%',
        flexGrow: 1,
        minHeight: 0,
    },
    insightsTitle: {
        padding: `10px ${tokens.spacingHorizontalM}`,
        lineHeight: tokens.lineHeightBase400,
        display: 'flex',
        alignItems: 'center',
        paddingLeft: '0px',
        paddingTop: '0px',
    },
    stepsTitle: {
        padding: `10px ${tokens.spacingHorizontalM}`,
        lineHeight: tokens.lineHeightBase400,
        display: 'flex',
        alignItems: 'center',
        paddingLeft: '0px',
        paddingTop: '20px',
    },
    accordionDescription: {
        padding: '0px 0px 20px 28px',
    },
    actionRow: {
        display: 'flex',
        gap: tokens.spacingHorizontalXS,
        paddingLeft: '0px',
        marginTop: tokens.spacingVerticalXS,
        marginBottom: tokens.spacingVerticalS,
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
        fontWeight: 600,
    },
    incidentDescriptionBubble: {
        backgroundColor: tokens.colorBrandBackground2,
        border: `1px solid ${tokens.colorBrandStroke2}`,
        borderRadius: tokens.borderRadiusMedium,
        padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
        marginBottom: '15px',
        width: '100%',
        boxSizing: 'border-box',
    },
    incidentDescriptionText: {
        margin: 0,
        lineHeight: tokens.lineHeightBase300,
    },
});

const AgentTaskDetailsPanel = ({ node, isOpen, onClose }: IAgentTaskDetailsPanelProps) => {
    const styles = useStyles();
    const { scrollable } = useScrollableComponentStyles();
    const intl = useIntl();
    const [stepsOpenItems, setStepsOpenItems] = useState<number[]>([]);
    const [propertyAccordionOpenItems, setPropertyAccordionOpenItems] = useState<number[]>([]);

    const expandAllProperties = useCallback((count: number) => {
        setPropertyAccordionOpenItems(Array.from({ length: count }, (_, i) => i));
    }, []);
    const collapseAllProperties = useCallback(() => setPropertyAccordionOpenItems([]), []);

    const toggleStep: AccordionToggleEventHandler<number> = (_, item) => {
        setStepsOpenItems(item.openItems);
    };

    const isInitialInvestigation = useMemo(() => {
        return node?.data?.nodeType === TreeNodeType.InitialInvestigation;
    }, [node]);

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

    type PropertyAccordionItem = { label: string; content: string };
    const { propertyItems, propertyAccordionCount } = useMemo(() => {
        if (!node) return { propertyItems: [], propertyAccordionCount: 0 };
        const items: PropertyAccordionItem[] = [];
        const pushIfContent = (label: string, value?: string | string[]) => {
            if (!value) return;
            if (Array.isArray(value)) {
                const cleaned = value.map(v => v.trim()).filter(v => v.length > 0);
                if (cleaned.length === 0) return;
                const list = cleaned.map(v => `- ${v}`).join('\n');
                items.push({ label, content: list });
                return;
            }
            const trimmed = value.trim();
            if (trimmed.length === 0) return;
            items.push({ label, content: trimmed });
        };
        pushIfContent(intl.formatMessage(SREAgentResources.incidentDescriptionLabel), node.data.incidentDescription);
        pushIfContent(intl.formatMessage(SREAgentResources.timeFrameLabel), node.data.timeFrame);
        pushIfContent(intl.formatMessage(SREAgentResources.affectedResourcesLabel), node.data.affectedResources);
        pushIfContent(intl.formatMessage(SREAgentResources.keyFindingsLabel), node.data.keyFindings);
        pushIfContent(intl.formatMessage(SREAgentResources.detailsLabel), node.data.details);
        return { propertyItems: items, propertyAccordionCount: items.length };
    }, [intl, node]);

    if (!isOpen || !node) return null;

    return (
        <Dialog
            open
            modalType="modal"
            onOpenChange={(_, data) => {
                if (!data.open) onClose();
            }}
        >
            <DialogSurface aria-label={node.data.title} className={styles.dialogSurface}>
                <DialogTitle className={styles.dialogTitle}>
                    {node.data.title}
                    <Button
                        appearance="subtle"
                        aria-label={intl.formatMessage(SREAgentResources.close)}
                        icon={<Dismiss24Regular />}
                        onClick={onClose}
                    />
                </DialogTitle>
                <DialogBody className={styles.dialogBody}>
                    <DialogContent className={`${scrollable} ${styles.dialogContent}`}>
                        <div className={styles.root}>
                            <div className={styles.summaryRoot}>
                                <div className={styles.insightsTitle}>
                                    <Subtitle2>{intl.formatMessage(SREAgentResources.insights)}</Subtitle2>
                                </div>
                                <div className={isInitialInvestigation ? styles.incidentDescriptionBubble : undefined}>
                                    <ReactMarkdownComponent content={node.data.description} variant="panel" />
                                </div>
                                {propertyAccordionCount > 0 && (
                                    <>
                                        <div className={styles.actionRow}>
                                            <Button
                                                size="small"
                                                appearance="subtle"
                                                icon={<ChevronUpDownRegular />}
                                                onClick={() => expandAllProperties(propertyAccordionCount)}
                                                disabled={propertyAccordionOpenItems.length === propertyAccordionCount}
                                            >
                                                {intl.formatMessage((SREAgentResources as any).expandAll)}
                                            </Button>
                                            <Button
                                                size="small"
                                                appearance="subtle"
                                                icon={<ChevronDownUpRegular />}
                                                onClick={collapseAllProperties}
                                                disabled={propertyAccordionOpenItems.length === 0}
                                            >
                                                {intl.formatMessage((SREAgentResources as any).collapseAll)}
                                            </Button>
                                        </div>
                                        <Accordion
                                            openItems={propertyAccordionOpenItems}
                                            onToggle={(_, data) => setPropertyAccordionOpenItems(data.openItems as number[])}
                                            multiple
                                            collapsible
                                            className={styles.accordion}
                                        >
                                            {propertyItems.map((item, index) => (
                                                <AccordionItem value={index} key={index} className={styles.accordionItem}>
                                                    <AccordionHeader>
                                                        <div className={styles.accordionHeader}>{item.label}</div>
                                                    </AccordionHeader>
                                                    <AccordionPanel className={styles.accordionDescription}>
                                                        <ReactMarkdownComponent content={item.content} variant="panel" />
                                                    </AccordionPanel>
                                                </AccordionItem>
                                            ))}
                                        </Accordion>
                                    </>
                                )}
                            </div>
                            {steps.length > 0 && (
                                <div className={styles.stepsRoot}>
                                    <div className={styles.stepsTitle}>
                                        <Subtitle2>
                                            {node.type === TreeNodeType.Hypothesis
                                                ? intl.formatMessage(SREAgentResources.validationSteps)
                                                : intl.formatMessage(SREAgentResources.investigationSteps)}
                                        </Subtitle2>
                                    </div>
                                    <div className={styles.actionRow}>
                                        <Button
                                            size="small"
                                            appearance="subtle"
                                            icon={<ChevronUpDownRegular />}
                                            onClick={() => setStepsOpenItems(steps.map((_, i) => i))}
                                            disabled={stepsOpenItems.length === steps.length}
                                        >
                                            {intl.formatMessage((SREAgentResources as any).expandAll)}
                                        </Button>
                                        <Button
                                            size="small"
                                            appearance="subtle"
                                            icon={<ChevronDownUpRegular />}
                                            onClick={() => setStepsOpenItems([])}
                                            disabled={stepsOpenItems.length === 0}
                                        >
                                            {intl.formatMessage((SREAgentResources as any).collapseAll)}
                                        </Button>
                                    </div>
                                    <Accordion
                                        openItems={stepsOpenItems}
                                        onToggle={toggleStep}
                                        multiple
                                        collapsible
                                        className={styles.accordion}
                                    >
                                        {steps.map((step, index) => (
                                            <AccordionItem value={index} key={index} className={styles.accordionItem}>
                                                <AccordionHeader>
                                                    <div className={styles.accordionHeader}>{step.title}</div>
                                                </AccordionHeader>
                                                <AccordionPanel className={styles.accordionDescription}>
                                                    <ReactMarkdownComponent content={step.description} />
                                                </AccordionPanel>
                                            </AccordionItem>
                                        ))}
                                    </Accordion>
                                </div>
                            )}
                        </div>
                    </DialogContent>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};

export default memo(AgentTaskDetailsPanel);
