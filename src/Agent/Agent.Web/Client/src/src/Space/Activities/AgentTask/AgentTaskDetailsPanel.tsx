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
    makeStyles,
    OverlayDrawer,
    Subtitle2,
    tokens,
    useRestoreFocusSource,
} from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { memo, useState } from 'react';
import ReactMarkdownComponent from '../../../Common/Components/ReactMarkdownComponent';
import { HypothesisStep, InitialInvestigationStep, TreeNodeType } from '../../../Common/Contracts/DataPlane/AgentTask';
import { useScrollableComponentStyles } from '../../../Common/Styles/Scrollable';
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
});

const AgentTaskDetailsPanel = ({ node, isOpen, onClose }: IAgentTaskDetailsPanelProps) => {
    const styles = useStyles();
    const { scrollable } = useScrollableComponentStyles();

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

    return (
        <OverlayDrawer open={isOpen && !!node} position={'end'} modalType={'non-modal'} size={'medium'} {...restoreFocusSourceAttributes}>
            <DrawerHeader>
                <DrawerHeaderTitle action={<Button appearance="subtle" aria-label="Close" icon={<Dismiss24Regular />} onClick={onClose} />}>
                    {node?.data.title}
                </DrawerHeaderTitle>
            </DrawerHeader>
            <DrawerBody className={scrollable}>
                <div className={styles.root}>
                    <div className={styles.summaryRoot}>
                        <ReactMarkdownComponent content={node?.data.description} variant="panel" />
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
                                                <ReactMarkdownComponent content={step.description} />
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
