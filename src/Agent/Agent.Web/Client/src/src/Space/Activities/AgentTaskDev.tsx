import { Button, DrawerHeader, DrawerHeaderTitle, makeStyles, tokens } from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { memo, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import Fade from '../Components/Fade';
import { InvestigationTreeFlow } from '../Components/InvestigationFlow/Core/InvestigationTreeFlow';
import { DetailsPanel } from '../Components/InvestigationFlow/DetailsPanel';
import { InvestigationTreeContext } from '../Contexts/InvestigationTreeContext';
import { Resizable } from './Resizable';

interface AgentTaskDevProps {
    collapseResizables?: () => void;
}

interface DetailsPanelState {
    isOpen: boolean;
    title: string;
    description: string;
    nodeType: 'phase' | 'hypothesis';
    steps: any[];
}

const useAgentTaskDevStyles = makeStyles({
    root: {
        backgroundColor: tokens.colorNeutralBackground1,
        height: '100%',
        borderRadius: tokens.borderRadiusXLarge,
        position: 'relative', // Required for absolutely positioned DetailsPanel
    },
});

const AgentTaskDev = memo(({ collapseResizables }: AgentTaskDevProps) => {
    const investigationTreeContext = useContext(InvestigationTreeContext);
    const { root } = useAgentTaskDevStyles();
    const [collapsed, setCollapsed] = useState(true);
    const [detailsPanel, setDetailsPanel] = useState<DetailsPanelState>({
        isOpen: false,
        title: '',
        description: '',
        nodeType: 'phase',
        steps: [],
    });

    // Handler for showing node details
    const handleShowDetails = useCallback((title: string, description: string, nodeType: 'phase' | 'hypothesis', steps?: any[]) => {
        setDetailsPanel({
            isOpen: true,
            title,
            description,
            nodeType,
            steps: steps || [],
        });
    }, []);

    // Handler for closing details panel
    const handleCloseDetails = useCallback(() => {
        setDetailsPanel(prev => ({
            ...prev,
            isOpen: false,
        }));
    }, []);

    // Safely extract values from contexts with useMemo to avoid ESLint warnings
    const treeState = useMemo(() => investigationTreeContext?.treeState || { isVisible: false }, [investigationTreeContext?.treeState]);

    // Auto-expand when tree becomes visible AND has actual investigation content
    // Auto-collapse when tree becomes invisible OR has no investigation content
    useEffect(() => {
        const hasInvestigationContent = treeState?.rootNodes && treeState.rootNodes.length > 0;

        if (treeState?.isVisible && collapsed && hasInvestigationContent) {
            setCollapsed(false);
            collapseResizables?.();
        } else if (!treeState?.isVisible || !hasInvestigationContent) {
            // Auto-collapse when tree is not visible OR has no investigation content
            setCollapsed(true);
        }
    }, [treeState?.isVisible, treeState?.rootNodes?.length, collapsed, collapseResizables]);

    return (
        <Resizable
            position="right"
            initialWidth="75%"
            minWidthPixels={300}
            minWidthPercent={20}
            maxWidthPercent={75}
            collapsedWidthPixels={collapsed ? 0 : 50}
            collapsed={collapsed}
            setCollapsed={setCollapsed}
            style={{ height: 'calc(100vh - 100px)', width: '100%' }}
        >
            {() => (
                <Fade visible={!collapsed} appear={true} unmountOnExit={true}>
                    <div className={root}>
                        <DrawerHeader>
                            <DrawerHeaderTitle
                                action={
                                    <Button
                                        appearance="subtle"
                                        aria-label="Close"
                                        icon={<Dismiss24Regular />}
                                        onClick={() => setCollapsed(true)}
                                    />
                                }
                            >
                                {'Deep investigation (Dev)'}
                            </DrawerHeaderTitle>
                        </DrawerHeader>
                        <InvestigationTreeFlow onShowDetails={handleShowDetails} />
                        <DetailsPanel
                            isOpen={detailsPanel.isOpen}
                            onClose={handleCloseDetails}
                            title={detailsPanel.title}
                            description={detailsPanel.description}
                            nodeType={detailsPanel.nodeType}
                            steps={detailsPanel.steps}
                        />
                    </div>
                </Fade>
            )}
        </Resizable>
    );
});

AgentTaskDev.displayName = 'AgentTaskDev';

export default AgentTaskDev;
