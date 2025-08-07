import { Button, makeStyles, Spinner, Text, tokens } from '@fluentui/react-components';
import { InfoRegular } from '@fluentui/react-icons';
import { Handle, NodeProps, Position } from '@xyflow/react';
import React, { useContext } from 'react';
import { InvestigationTreeContext } from '../../Contexts/InvestigationTreeContext';

const useHypothesisNodeStyles = makeStyles({
    hypothesisNode: {
        backgroundColor: tokens.colorNeutralBackground2,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: tokens.borderRadiusSmall,
        padding: '16px',
        minHeight: '140px',
        minWidth: '280px',
        maxWidth: '320px',
        display: 'flex',
        flexDirection: 'column',
        position: 'relative',
        boxShadow: tokens.shadow4, // Enhanced shadow for better depth
        cursor: 'grab',
        transition: 'all 0.2s ease-in-out',
        '&:hover': {
            boxShadow: tokens.shadow8,
            transform: 'translateY(-2px)',
        },
    },
    childHypothesis: {
        backgroundColor: tokens.colorNeutralBackground3,
        border: `1px solid ${tokens.colorNeutralStroke3}`,
        borderRadius: tokens.borderRadiusSmall,
        padding: '12px',
        minHeight: '120px',
        minWidth: '240px',
        maxWidth: '280px',
        display: 'flex',
        flexDirection: 'column',
        position: 'relative',
        boxShadow: tokens.shadow2, // Lighter shadow for child nodes
        cursor: 'grab',
        transition: 'all 0.2s ease-in-out',
        '&:hover': {
            boxShadow: tokens.shadow4,
            transform: 'translateY(-1px)',
        },
    },
    hypothesisHeader: {
        display: 'flex',
        alignItems: 'flex-start',
        justifyContent: 'space-between',
        marginBottom: '10px', // Increased spacing
        gap: '8px',
    },
    hypothesisTitle: {
        fontSize: '14px',
        fontWeight: '600',
        color: tokens.colorNeutralForeground1,
        margin: 0,
        lineHeight: '1.3',
        flex: 1,
    },
    hypothesisDescription: {
        fontSize: '12px',
        color: tokens.colorNeutralForeground2,
        lineHeight: '1.4',
        margin: 0,
        marginBottom: '12px',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        display: '-webkit-box',
        WebkitLineClamp: 3,
        WebkitBoxOrient: 'vertical',
    },
    expandButton: {
        marginTop: 'auto',
        fontSize: '11px',
        padding: '4px 8px',
        minHeight: '24px',
        alignSelf: 'flex-start',
    },
    statusBadge: {
        fontSize: '10px',
        fontWeight: '500',
        padding: '2px 6px',
        borderRadius: tokens.borderRadiusSmall,
        whiteSpace: 'nowrap',
        flexShrink: 0,
    },
    statusPending: {
        backgroundColor: tokens.colorNeutralBackground3,
        color: tokens.colorNeutralForeground2,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    statusValidating: {
        backgroundColor: tokens.colorBrandBackground2,
        color: tokens.colorBrandForeground2,
        border: `1px solid ${tokens.colorBrandStroke2}`,
    },
    statusValidated: {
        backgroundColor: tokens.colorPaletteGreenBackground2,
        color: tokens.colorPaletteGreenForeground1,
        border: `1px solid ${tokens.colorPaletteGreenBorder1}`,
    },
    statusInvalidated: {
        backgroundColor: tokens.colorPaletteRedBackground2,
        color: tokens.colorPaletteRedForeground1,
        border: `1px solid ${tokens.colorPaletteRedBorder1}`,
    },
    statusInconclusive: {
        backgroundColor: tokens.colorPaletteYellowBackground2,
        color: tokens.colorPaletteYellowForeground1,
        border: `1px solid ${tokens.colorPaletteYellowBorder1}`,
    },
    detailsIndicator: {
        position: 'absolute',
        top: '4px',
        right: '4px',
        color: tokens.colorBrandForeground1,
        fontSize: '10px',
        cursor: 'pointer',
        opacity: 0.7,
        '&:hover': {
            opacity: 1,
        },
    },
});

interface HypothesisNodeData {
    id: string;
    title: string;
    description: string;
    status: string;
    isValidating: boolean;
    isChild: boolean;
    hasChildren: boolean;
    expanded: boolean;
    index?: number;
    nodeType: 'hypothesis';
    steps?: any[]; // Detailed step data for overlay
    onShowDetails?: (title: string, description: string, nodeType: 'phase' | 'hypothesis', steps?: any[]) => void;
}

const getHypothesisStatusBadge = (status: string, styles: ReturnType<typeof useHypothesisNodeStyles>) => {
    switch (status.toLowerCase()) {
        case 'validating':
            return <div className={`${styles.statusBadge} ${styles.statusValidating}`}>Validating</div>;
        case 'validated':
            return <div className={`${styles.statusBadge} ${styles.statusValidated}`}>Validated</div>;
        case 'invalidated':
            return <div className={`${styles.statusBadge} ${styles.statusInvalidated}`}>Invalidated</div>;
        case 'inconclusive':
            return <div className={`${styles.statusBadge} ${styles.statusInconclusive}`}>Inconclusive</div>;
        default:
            return <div className={`${styles.statusBadge} ${styles.statusPending}`}>Pending</div>;
    }
};

export const HypothesisNode: React.FC<NodeProps> = ({ data }) => {
    const nodeData = data as unknown as HypothesisNodeData;
    const { toggleNodeExpanded } = useContext(InvestigationTreeContext);
    const styles = useHypothesisNodeStyles();

    const handleToggle = () => {
        if (nodeData.hasChildren) {
            toggleNodeExpanded(nodeData.id);
        }
    };

    const handleNodeClick = (e: React.MouseEvent) => {
        // Don't show details if clicking on the expand/collapse button
        if ((e.target as HTMLElement).closest('button')) {
            return;
        }

        // Show details if hypothesis has detailed steps
        if (nodeData.steps && nodeData.steps.length > 0 && nodeData.onShowDetails) {
            nodeData.onShowDetails(nodeData.title, nodeData.description, 'hypothesis', nodeData.steps);
        }
    };

    const nodeClass = nodeData.isChild ? styles.childHypothesis : styles.hypothesisNode;

    return (
        <div className={nodeClass} onClick={handleNodeClick} style={{ cursor: 'pointer' }}>
            {/* Input handle */}
            <Handle type="target" position={Position.Top} style={{ background: tokens.colorNeutralStroke2 }} />

            {/* Details indicator for hypotheses with steps */}
            {nodeData.steps && nodeData.steps.length > 0 && <InfoRegular className={styles.detailsIndicator} />}

            <div className={styles.hypothesisHeader}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flex: 1 }}>
                    {nodeData.status === 'validating' && <Spinner size="extra-small" />}
                    <Text className={styles.hypothesisTitle}>
                        {nodeData.index && `${nodeData.index}. `}
                        {nodeData.title}
                    </Text>
                </div>
                {getHypothesisStatusBadge(nodeData.status, styles)}
            </div>
            <Text className={styles.hypothesisDescription}>{nodeData.description}</Text>

            {nodeData.hasChildren && (
                <Button appearance="transparent" size="small" onClick={handleToggle} className={styles.expandButton}>
                    {nodeData.expanded ? 'Collapse' : 'Expand'}
                </Button>
            )}

            {/* Output handle (only if has children and expanded) */}
            {nodeData.hasChildren && nodeData.expanded && (
                <Handle type="source" position={Position.Bottom} style={{ background: tokens.colorNeutralStroke2 }} />
            )}
        </div>
    );
};

export type { HypothesisNodeData };
