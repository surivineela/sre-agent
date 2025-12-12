import { makeStyles, tokens } from '@fluentui/react-components';
import { ExtendedAgentNodeSize } from '../../../Contracts/ExtendedAgentGraph';

export const useToolboxCardStyles = makeStyles({
    groupContainer: {
        border: `2px dashed ${tokens.colorNeutralStroke2}`,
        borderRadius: '16px',
        padding: `${ExtendedAgentNodeSize.toolsBasePadding}px`,
        backgroundColor: tokens.colorNeutralBackground1,
        width: '100%',
        boxSizing: 'border-box',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },
    toolsContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: `${ExtendedAgentNodeSize.toolsRowGap}px`,
    },
    collapseLinkContainer: {
        display: 'flex',
        justifyContent: 'flex-start',
        marginTop: tokens.spacingVerticalS,
    },
    collapseLink: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground3,
        cursor: 'pointer',
        ':hover': {
            color: tokens.colorNeutralForeground1,
        },
    },
    // Compact tool card styles for use inside the toolbox
    compactToolCard: {
        padding: tokens.spacingVerticalS + ' ' + tokens.spacingHorizontalM,
        minHeight: 'unset',
        cursor: 'pointer',
    },
});
