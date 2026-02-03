import { makeStyles, tokens } from '@fluentui/react-components';

export const useSreAgentSpaceStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'stretch',
        alignItems: 'flex-start',
        height: '100dvh',
        overflow: 'hidden',
    },
    content: {
        flex: '1 1 auto',
        display: 'flex',
        flexDirection: 'row',
        alignItems: 'stretch',
        width: '100dvw',
        overflow: 'hidden',
        position: 'relative',
    },
    lineIconStyle: { transform: 'rotate(90deg)', marginRight: '5px', marginLeft: '5px', marginTop: '12px' },
    logsMenuItemContainer: {
        display: 'flex',
        flexDirection: 'row',
        gap: '5px',
        alignItems: 'center',
    },
    overlayComponentContainer: {
        height: '100%',
    },
    overlayComponentFlexBox: {
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'center',
        alignItems: 'center',
        gap: tokens.spacingVerticalM,
        height: '100%',
    },
    navBody: {
        overflow: 'hidden',
        padding: `0px ${tokens.spacingHorizontalS}`,
        '&:first-child': {
            paddingTop: tokens.spacingHorizontalM,
        },
        '&:last-child': {
            paddingBottom: '0',
        },
    },
    collapsedNavBody: {
        padding: `0px 0px 0px ${tokens.spacingHorizontalS}`,
    },
    outletRoot: {
        flex: '1 1 auto',
        flexDirection: 'column',
        overflow: 'auto',
        backgroundColor: tokens.colorNeutralBackground3,
        display: 'flex',
        padding: `0px ${tokens.spacingHorizontalS} ${tokens.spacingHorizontalS} 0px`,
    },
    outletRootWithNoNavBar: {
        paddingLeft: tokens.spacingHorizontalS,
    },
});
