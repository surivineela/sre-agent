import { makeStyles, tokens } from '@fluentui/react-components';
import { ExtendedAgentNodeSize } from '../Contracts/ExtendedAgentGraph';

export const useExtendedAgentGraphStyles = makeStyles({
    visualRoot: {
        display: 'flex',
        flexDirection: 'row',
        justifyContent: 'flex-start',
        alignItems: 'flex-start',
        width: '100%',
        height: 'calc(100% - 2rem)',
        minHeight: 0,
        overflow: 'hidden',
        position: 'relative',
    },
    reactFlow: {
        width: '100%',
        height: '100%',
        minHeight: 0,
        position: 'relative',
    },
    spinner: {
        position: 'fixed',
        left: '50%',
        top: '50%',
        transform: 'translate(-50%, -50%)',
    },
    container: {
        flex: '1 1 auto',
        padding: '1rem',
        paddingRight: 0,
        paddingTop: 0,
        backgroundColor: tokens.colorNeutralBackground1,
        borderTopLeftRadius: tokens.borderRadiusXLarge,
        boxShadow: tokens.shadow4,
        height: 'calc(100vh - 0.5rem - 1px - 50px)',
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
        minHeight: 0,
    },
    rootContainer: {
        display: 'flex',
        flexDirection: 'column',
        height: '100vh',
        paddingTop: '0.5rem',
        borderTop: '1px solid rgba(204, 204, 204, 0.8)',
        backgroundColor: tokens.colorNeutralBackground3,
        paddingLeft: '15px',
        gap: '0.25rem',
    },
    radioGroupContainer: {
        paddingRight: '1rem',
        paddingBottom: '0.25rem',
        flex: 'none',
    },
    statusMessageContainer: {
        paddingRight: '1rem',
        marginBottom: tokens.spacingVerticalS,
    },
    selectorOverlay: {
        position: 'absolute',
        top: 0,
        left: 0,
        right: 0,
        display: 'flex',
        justifyContent: 'flex-start',
        padding: '12px 16px 0 16px',
        pointerEvents: 'none',
        boxSizing: 'border-box',
        zIndex: 10,
    },
    infoPanelContainer: {
        position: 'relative',
        marginLeft: '0',
        pointerEvents: 'auto',
        zIndex: 5,
        height: '100%',
        display: 'flex',
    },
    infoPanelFloating: {
        position: 'absolute',
        top: 0,
        left: 0,
        marginLeft: 0,
        cursor: 'default',
        zIndex: 15,
        pointerEvents: 'auto',
        height: '100%',
    },
});

// Agent Node Styles
export const useExtendedAgentNodeStyles = makeStyles({
    handle: {
        opacity: 0,
        pointerEvents: 'none',
    },
    agentCard: {
        width: `${ExtendedAgentNodeSize.agentWidth}px`,
        minHeight: `${ExtendedAgentNodeSize.agentHeight}px`,
        borderRadius: tokens.borderRadiusXLarge,
        cursor: 'pointer',
        transition: 'box-shadow 0.2s ease-in-out',
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: tokens.colorNeutralBackground3,
        padding: tokens.spacingHorizontalM,
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        position: 'relative',
    },
    autonomousCard: {
        backgroundColor: tokens.colorNeutralBackground3,
    },
    orchestratorCard: {
        backgroundColor: tokens.colorNeutralBackground3,
    },
    activityCard: {
        backgroundColor: tokens.colorNeutralBackground3,
    },
    cardHighlighted: {
        boxShadow: tokens.shadow8,
    },
    cardHovered: {
        boxShadow: tokens.shadow16,
    },
    cardSelected: {
        border: `2px solid ${tokens.colorBrandStroke1}`,
        boxShadow: tokens.shadow16,
        backgroundColor: `${tokens.colorBrandBackground2} !important`,
    },
    header: {},
    headerText: {},
    description: {},
    badge: {},
    cardContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        flex: 1,
    },
    titleRow: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
        alignItems: 'center',
    },
    iconWrapper: {
        width: '36px',
        height: '36px',
        borderRadius: '50%',
        backgroundColor: tokens.colorBrandBackground2,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        color: tokens.colorBrandForegroundInverted,
        flexShrink: 0,
    },
    nameBlock: {
        flex: 1,
        minWidth: 0,
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
    },
    nameText: {
        color: tokens.colorNeutralForeground1,
        fontWeight: tokens.fontWeightSemibold,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    subtitleText: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    instructionsText: {
        color: tokens.colorNeutralForeground2,
        display: '-webkit-box',
        WebkitLineClamp: 3,
        WebkitBoxOrient: 'vertical',
        overflow: 'hidden',
        fontSize: tokens.fontSizeBase200,
    },
    emptyText: {
        color: tokens.colorNeutralForeground3,
    },
    chipRow: {
        display: 'flex',
        gap: tokens.spacingHorizontalXS,
        flexWrap: 'wrap',
    },
    quickActionButton: {
        position: 'absolute',
        bottom: tokens.spacingVerticalS,
        right: tokens.spacingHorizontalS,
        boxShadow: tokens.shadow8,
        zIndex: 2,
    },
    menuPopover: {
        boxShadow: tokens.shadow16,
        borderRadius: tokens.borderRadiusMedium,
    },
    toolsBadge: {
        position: 'absolute',
        top: tokens.spacingVerticalS,
        right: tokens.spacingHorizontalS,
        zIndex: 1,
    },
});

// Tool Node Styles
export const useToolNodeStyles = makeStyles({
    handle: {
        opacity: 0,
        pointerEvents: 'none',
    },
    toolCard: {
        width: `${ExtendedAgentNodeSize.toolWidth}px`,
        minHeight: `${ExtendedAgentNodeSize.toolHeight}px`,
        borderRadius: tokens.borderRadiusXLarge,
        cursor: 'pointer',
        transition: 'box-shadow 0.2s ease-in-out',
        backgroundColor: tokens.colorNeutralBackground3,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        padding: tokens.spacingHorizontalM,
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        position: 'relative',
    },
    kustoToolCard: {
        backgroundColor: tokens.colorNeutralBackground3,
    },
    linkToolCard: {
        backgroundColor: tokens.colorNeutralBackground3,
    },
    cardHighlighted: {
        boxShadow: tokens.shadow8,
    },
    cardHovered: {
        boxShadow: tokens.shadow16,
    },
    cardSelected: {
        border: `2px solid ${tokens.colorBrandStroke1}`,
        boxShadow: tokens.shadow16,
        backgroundColor: `${tokens.colorBrandBackground2} !important`,
    },
    header: {},
    headerText: {},
    description: {},
    connectorBadge: {},
    cardContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        flex: 1,
    },
    titleRow: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
        alignItems: 'center',
    },
    iconWrapper: {
        width: '32px',
        height: '32px',
        borderRadius: '8px',
        backgroundColor: tokens.colorPaletteBlueBackground2,
        color: tokens.colorPaletteBlueForeground2,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        flexShrink: 0,
    },
    nameBlock: {
        flex: 1,
        minWidth: 0,
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
    },
    nameText: {
        color: tokens.colorNeutralForeground1,
        fontWeight: tokens.fontWeightSemibold,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    subtitleText: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    descriptionText: {
        color: tokens.colorNeutralForeground2,
        display: '-webkit-box',
        WebkitLineClamp: 3,
        WebkitBoxOrient: 'vertical',
        overflow: 'hidden',
        fontSize: tokens.fontSizeBase200,
    },
    footerRow: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    mutedText: {
        color: tokens.colorNeutralForeground3,
    },
});

// Trigger Node Styles
export const useTriggerNodeStyles = makeStyles({
    handle: {
        opacity: 0,
        pointerEvents: 'none',
    },
    triggerCard: {
        width: `${ExtendedAgentNodeSize.triggerWidth}px`,
        minHeight: `${ExtendedAgentNodeSize.triggerHeight}px`,
        borderRadius: tokens.borderRadiusXLarge,
        cursor: 'pointer',
        transition: 'box-shadow 0.2s ease-in-out',
        backgroundColor: tokens.colorNeutralBackground3,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        padding: tokens.spacingHorizontalM,
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        position: 'relative',
    },
    incidentTriggerCard: {
        backgroundColor: tokens.colorNeutralBackground3,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    scheduledTriggerCard: {
        backgroundColor: tokens.colorNeutralBackground3,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    cardHighlighted: {
        boxShadow: tokens.shadow8,
    },
    cardHovered: {
        boxShadow: tokens.shadow16,
        transform: 'translateY(-2px)',
    },
    cardSelected: {
        border: `2px solid ${tokens.colorBrandStroke1}`,
        boxShadow: tokens.shadow16,
        backgroundColor: `${tokens.colorBrandBackground2} !important`,
    },
    cardContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        flex: 1,
    },
    titleRow: {
        display: 'flex',
        alignItems: 'flex-start',
        gap: tokens.spacingHorizontalS,
    },
    iconWrapper: {
        width: '24px',
        height: '24px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        color: tokens.colorNeutralForeground1,
        flexShrink: 0,
    },
    nameBlock: {
        flex: 1,
        minWidth: 0,
        display: 'flex',
        flexDirection: 'column',
    },
    nameText: {
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        wordBreak: 'break-word',
        fontSize: tokens.fontSizeBase300,
        lineHeight: tokens.lineHeightBase300,
    },
    subtitleText: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
        lineHeight: tokens.lineHeightBase200,
        wordBreak: 'break-word',
    },
    descriptionText: {
        color: tokens.colorNeutralForeground2,
        display: '-webkit-box',
        WebkitLineClamp: 2,
        WebkitBoxOrient: 'vertical',
        overflow: 'hidden',
        fontSize: tokens.fontSizeBase200,
    },
    mutedText: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
        fontStyle: 'italic',
    },
    statusBadge: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
    },
    footerRow: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        marginTop: 'auto',
    },
});

// Connector Node Styles
export const useConnectorNodeStyles = makeStyles({
    handle: {
        opacity: 0,
        pointerEvents: 'none',
    },
    connectorCard: {
        width: `${ExtendedAgentNodeSize.connectorWidth}px`,
        minHeight: `${ExtendedAgentNodeSize.connectorHeight}px`,
        borderRadius: tokens.borderRadiusXLarge,
        cursor: 'pointer',
        transition: 'box-shadow 0.2s ease-in-out',
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: tokens.colorNeutralBackground3,
        padding: tokens.spacingHorizontalM,
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        position: 'relative',
    },
    connectorEnabledCard: {
        backgroundColor: tokens.colorNeutralBackground3,
    },
    connectorDisabledCard: {
        backgroundColor: tokens.colorNeutralBackground3,
    },
    cardHighlighted: {
        boxShadow: tokens.shadow8,
    },
    cardHovered: {
        boxShadow: tokens.shadow16,
    },
    cardSelected: {
        border: `2px solid ${tokens.colorBrandStroke1}`,
        boxShadow: tokens.shadow16,
        backgroundColor: `${tokens.colorBrandBackground2} !important`,
    },
    header: {},
    headerText: {},
    description: {},
    cardContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        flex: 1,
    },
    titleRow: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
        alignItems: 'center',
    },
    iconWrapper: {
        width: '32px',
        height: '32px',
        borderRadius: '8px',
        backgroundColor: tokens.colorPaletteGreenBackground2,
        color: tokens.colorPaletteGreenForeground2,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        flexShrink: 0,
    },
    nameBlock: {
        flex: 1,
        minWidth: 0,
    },
    nameText: {
        color: tokens.colorNeutralForeground1,
        fontWeight: tokens.fontWeightSemibold,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    subtitleText: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    descriptionText: {
        color: tokens.colorNeutralForeground2,
        display: '-webkit-box',
        WebkitLineClamp: 3,
        WebkitBoxOrient: 'vertical',
        overflow: 'hidden',
        fontSize: tokens.fontSizeBase200,
    },
    mutedText: {
        color: tokens.colorNeutralForeground3,
    },
    statusBadge: {
        position: 'absolute',
        top: tokens.spacingVerticalS,
        right: tokens.spacingHorizontalS,
        zIndex: 1,
    },
});

// Edge Styles
export const useExtendedAgentEdgeStyles = makeStyles({
    highlightedEdge: {
        stroke: `${tokens.colorBrandForegroundLinkHover} !important`,
        strokeWidth: '2px !important',
    },
    usesToolEdge: {
        stroke: tokens.colorPaletteBlueForeground2,
    },
    systemToolEdge: {
        stroke: tokens.colorPaletteGoldForeground2,
    },
    connectorEdge: {
        stroke: tokens.colorPaletteGreenForeground2,
    },
    agentAsToolEdge: {
        stroke: tokens.colorPalettePurpleForeground2,
    },
    handoffEdge: {
        stroke: tokens.colorPaletteDarkOrangeForeground2,
    },
});

// Selector Panel Styles
export const useExtendedAgentSelectorStyles = makeStyles({
    overlayCard: {
        pointerEvents: 'auto',
        backgroundColor: tokens.colorNeutralBackground1,
        borderRadius: tokens.borderRadiusLarge,
        boxShadow: tokens.shadow16,
        padding: '16px',
        minWidth: 'min(720px, 100%)',
        maxWidth: 'min(960px, 100%)',
        border: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    inputsRow: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalM,
        alignItems: 'flex-end',
    },
    field: {
        flex: 1,
        minWidth: '220px',
    },
    option: {
        display: 'flex',
        flexDirection: 'column',
        gap: '2px',
        alignItems: 'flex-start',
        textAlign: 'left',
    },
    optionText: {
        wordBreak: 'break-word',
    },
    optionSubtext: {
        color: tokens.colorNeutralForeground3,
        wordBreak: 'break-word',
    },
    searchBox: {
        width: '100%',
    },
    actionColumn: {
        display: 'flex',
        alignItems: 'flex-end',
        paddingBottom: tokens.spacingVerticalXXS,
    },
    statsRow: {
        display: 'flex',
        alignItems: 'center',
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalM,
    },
    statsItem: {
        color: tokens.colorNeutralForeground2,
    },
    badgeGroup: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
        flexWrap: 'wrap',
    },
    emptyNotice: {
        color: tokens.colorNeutralForeground3,
        paddingInlineStart: tokens.spacingHorizontalXS,
    },
});

// Info Panel Styles
export const useExtendedAgentInfoStyles = makeStyles({
    root: {
        height: '100%',
        backgroundColor: tokens.colorNeutralBackground1,
        display: 'flex',
        flexDirection: 'row',
        overflow: 'hidden',
        boxSizing: 'border-box',
        minWidth: '280px',
        boxShadow: tokens.shadow4,
    },
    panel: {
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
    },
    resizeHandle: {
        width: '12px',
        cursor: 'col-resize',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: '0 2px',
        touchAction: 'none',
        backgroundColor: 'transparent',
        transition: 'background-color 0.2s ease',
    },
    resizeHandleHovered: {
        backgroundColor: tokens.colorNeutralBackground3,
    },
    resizeHandleGrip: {
        width: '2px',
        height: '60%',
        borderRadius: tokens.borderRadiusCircular,
        backgroundColor: 'transparent',
        transition: 'background-color 0.2s ease',
    },
    resizeHandleGripVisible: {
        backgroundColor: tokens.colorNeutralStroke2,
    },
    header: {
        padding: '16px',
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
        gap: tokens.spacingHorizontalM,
    },
    headerInfo: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
        cursor: 'grab',
        userSelect: 'none',
    },
    tabList: {
        marginTop: tokens.spacingVerticalS,
    },
    content: {
        flex: 1,
        overflowY: 'auto',
        padding: '16px',
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
    },
    tabPanel: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
        flex: 1,
    },
    section: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        marginBottom: tokens.spacingVerticalL,
    },
    sectionTitle: {
        fontWeight: tokens.fontWeightSemibold,
        marginBottom: tokens.spacingVerticalXS,
    },
    subSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
        marginTop: tokens.spacingVerticalS,
    },
    subtitle: {
        color: tokens.colorNeutralForeground2,
    },
    instructions: {
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
        color: tokens.colorNeutralForeground2,
    },
    list: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
    },
    pillGroup: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalS,
    },
    pillTag: {
        fontWeight: tokens.fontWeightSemibold,
        backgroundColor: tokens.colorNeutralBackground1,
    },
    listItem: {
        padding: '8px 12px',
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusSmall,
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
    },
    listItemHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    listItemBadges: {
        display: 'flex',
        gap: tokens.spacingHorizontalXS,
        flexWrap: 'wrap',
    },
    statusBadge: {
        fontWeight: tokens.fontWeightSemibold,
    },
    badgeRow: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
        flexWrap: 'wrap',
    },
    metadataRow: {
        display: 'flex',
        justifyContent: 'space-between',
        gap: tokens.spacingHorizontalS,
        color: tokens.colorNeutralForeground2,
        wordBreak: 'break-word',
    },
    metadataKey: {
        fontWeight: tokens.fontWeightSemibold,
    },
    emptyState: {
        color: tokens.colorNeutralForeground3,
    },
    summary: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
    },
    toolGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
        gap: tokens.spacingHorizontalS,
    },
    toolCard: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
        padding: '14px 16px',
        borderRadius: tokens.borderRadiusMedium,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: tokens.colorNeutralBackground2,
        boxShadow: tokens.shadow4,
        transition: 'transform 0.2s ease, box-shadow 0.2s ease',
        ':hover': {
            transform: 'translateY(-2px)',
            boxShadow: tokens.shadow16,
        },
    },
    toolCardHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    toolCardDescription: {
        color: tokens.colorNeutralForeground3,
    },
    toolCardMeta: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalXS,
        marginTop: tokens.spacingVerticalXXS,
    },
    toolTag: {
        fontWeight: tokens.fontWeightSemibold,
    },
    accordion: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
    },
    accordionPanel: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
        paddingTop: tokens.spacingVerticalS,
    },
    yamlEditorContainer: {
        flex: 1,
        minHeight: '360px',
        borderRadius: tokens.borderRadiusMedium,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        overflow: 'hidden',
    },
    yamlActions: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: tokens.spacingHorizontalM,
    },
    yamlButtons: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
    },
    yamlButton: {
        alignSelf: 'flex-start',
    },
    yamlDescription: {
        color: tokens.colorNeutralForeground3,
    },
});
