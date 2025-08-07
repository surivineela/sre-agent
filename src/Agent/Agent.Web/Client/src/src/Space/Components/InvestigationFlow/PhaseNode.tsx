import { makeStyles, Spinner, Text, tokens } from '@fluentui/react-components';
import { CheckmarkCircleRegular, CircleRegular, ErrorCircleRegular, InfoRegular } from '@fluentui/react-icons';
import { Handle, NodeProps, Position } from '@xyflow/react';
import React from 'react';

const usePhaseNodeStyles = makeStyles({
    phaseNode: {
        backgroundColor: tokens.colorNeutralBackground1,
        border: `2px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: tokens.borderRadiusMedium,
        padding: '20px',
        minWidth: '400px',
        maxWidth: '600px',
        boxShadow: tokens.shadow4, // Enhanced shadow for better depth on white background
        textAlign: 'center',
        position: 'relative',
        cursor: 'grab',
        transition: 'all 0.2s ease-in-out',
        '&:hover': {
            boxShadow: tokens.shadow8,
            transform: 'translateY(-2px)',
        },
    },
    completedPhaseNode: {
        backgroundColor: tokens.colorPaletteGreenBackground1,
        border: `2px solid ${tokens.colorPaletteGreenBorder1}`,
        boxShadow: tokens.shadow8, // Stronger shadow for completed nodes
    },
    inProgressPhaseNode: {
        backgroundColor: tokens.colorNeutralBackground1,
        border: `2px solid ${tokens.colorBrandStroke1}`,
        boxShadow: tokens.shadow8, // Stronger shadow for active nodes
    },
    phaseHeader: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
        marginBottom: '12px', // Increased margin for better spacing
        justifyContent: 'center',
    },
    detailsIndicator: {
        position: 'absolute',
        top: '8px',
        right: '8px',
        color: tokens.colorBrandForeground1,
        fontSize: '12px',
        cursor: 'pointer',
        opacity: 0.7,
        '&:hover': {
            opacity: 1,
        },
    },
    phaseTitle: {
        fontSize: '18px',
        fontWeight: '600',
        color: tokens.colorNeutralForeground1,
        margin: 0,
    },
    phaseDescription: {
        fontSize: '14px',
        color: tokens.colorNeutralForeground2,
        lineHeight: '1.5',
        margin: 0,
        maxWidth: '500px',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        display: '-webkit-box',
        WebkitLineClamp: 3,
        WebkitBoxOrient: 'vertical',
    },
});

interface PhaseNodeData {
    id: string;
    title: string;
    description: string;
    status: string;
    isLoading: boolean;
    nodeType: 'phase';
    steps?: any[]; // Detailed step data for overlay
    onShowDetails?: (title: string, description: string, nodeType: 'phase' | 'hypothesis', steps?: any[]) => void;
}

const getStatusIcon = (status: string, isLoading: boolean) => {
    if (isLoading) {
        return <Spinner size="small" />;
    }

    switch (status.toLowerCase()) {
        case 'complete':
        case 'completed':
            return <CheckmarkCircleRegular style={{ color: tokens.colorPaletteGreenForeground1, fontSize: '20px' }} />;
        case 'inprogress':
        case 'in_progress':
            return <CircleRegular style={{ color: tokens.colorBrandForeground1, fontSize: '20px' }} />;
        case 'failed':
        case 'error':
            return <ErrorCircleRegular style={{ color: tokens.colorPaletteRedForeground1, fontSize: '20px' }} />;
        default:
            return <CircleRegular style={{ color: tokens.colorNeutralForeground3, fontSize: '20px' }} />;
    }
};

const getPhaseNodeClass = (status: string, styles: ReturnType<typeof usePhaseNodeStyles>) => {
    switch (status.toLowerCase()) {
        case 'complete':
        case 'completed':
            return `${styles.phaseNode} ${styles.completedPhaseNode}`;
        case 'inprogress':
        case 'in_progress':
            return `${styles.phaseNode} ${styles.inProgressPhaseNode}`;
        default:
            return styles.phaseNode;
    }
};

// const extractIncidentSummary = (description: string): string => {
//     // Extract a shorter summary for the initial investigation phase
//     if (description.length <= 150) {
//         return description;
//     }

//     // Try to find a good breaking point
//     const sentences = description.split('. ');
//     if (sentences.length > 1) {
//         return sentences.slice(0, 2).join('. ') + '.';
//     }

//     // Fallback to first 150 characters with ellipsis
//     return description.substring(0, 150) + '...';
// };

export const PhaseNode: React.FC<NodeProps> = ({ data /*, id */ }) => {
    const nodeData = data as unknown as PhaseNodeData;
    const styles = usePhaseNodeStyles();
    const isInitialInvestigation = nodeData.id.toLowerCase().includes('initial-investigation');
    const isConclusion = nodeData.id.toLowerCase().includes('conclusion');

    const handlePhaseClick = () => {
        // Show details for initial investigation (if it has steps) or conclusion nodes
        const shouldShowDetails = (isInitialInvestigation && nodeData.steps && nodeData.steps.length > 0) || isConclusion;
        if (shouldShowDetails && nodeData.onShowDetails) {
            nodeData.onShowDetails(nodeData.title, nodeData.description, 'phase', nodeData.steps);
        }
    };

    return (
        <div className={getPhaseNodeClass(nodeData.status, styles)} onClick={handlePhaseClick} style={{ cursor: 'pointer' }}>
            {/* Input handle (except for first node) */}
            {!isInitialInvestigation && <Handle type="target" position={Position.Top} style={{ background: tokens.colorNeutralStroke2 }} />}

            {/* Details indicator for initial investigation or conclusion */}
            {((isInitialInvestigation && nodeData.steps && nodeData.steps.length > 0) || isConclusion) && (
                <InfoRegular className={styles.detailsIndicator} />
            )}

            <div className={styles.phaseHeader}>
                {getStatusIcon(nodeData.status, nodeData.isLoading)}
                <Text className={styles.phaseTitle}>{nodeData.title}</Text>
            </div>
            <Text className={styles.phaseDescription}>
                {/* {isInitialInvestigation ? extractIncidentSummary(nodeData.description) : nodeData.description} */}
                {nodeData.description}
            </Text>

            {/* Output handle (except for last node) */}
            {!isConclusion && <Handle type="source" position={Position.Bottom} style={{ background: tokens.colorNeutralStroke2 }} />}
        </div>
    );
};

export type { PhaseNodeData };
