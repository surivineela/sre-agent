import { makeStyles, tokens } from '@fluentui/react-components';

export const useConnectorWizardStyles = makeStyles({
    searchBarContainer: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: tokens.spacingHorizontalMNudge,
    },
    searchBox: {
        maxWidth: '100%',
        flexGrow: 5,
    },
    cardContainer: {
        padding: '10px 0',
    },
    cardGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
        gap: tokens.spacingHorizontalL,
        padding: '3px',
        alignItems: 'stretch',
        gridAutoRows: '1fr',
    },
    cardHeader: {
        marginBottom: 'auto',
    },
    cardContent: {
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'flex-start',
        height: '100%',
    },
    image: {
        width: '32px',
        height: '32px',
    },
    serviceDescription: {
        color: '#666',
        display: 'block',
    },
    cardDescription: {
        marginTop: 'auto',
    },
    wizardContentContainer: {
        padding: tokens.spacingVerticalXXL,
        paddingBottom: '0px',
        overflow: 'visible',
    },
    form: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
        maxWidth: '460px',
    },
    identityLink: {
        width: 'fit-content',
    },
    title: {
        margin: '0px',
        fontWeight: tokens.fontWeightSemibold,
        fontSize: tokens.fontSizeBase400,
    },
    connectorPickerTitle: {
        marginBottom: tokens.spacingVerticalL,
    },
    reviewAndAddContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXL,
    },
    reviewAndAddSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    reviewAndAddSectionTitle: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightSemibold,
    },
    reviewAndAddCardContent: {
        display: 'flex',
        flexDirection: 'column',
    },
    reviewAndAddCardImage: {
        maxWidth: '32px',
        maxHeight: '32px',
    },
    reviewAndAddSectionValue: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightRegular,
        wordBreak: 'break-all',
        overflowWrap: 'break-word',
    },
    outlookTeamsButton: {
        maxWidth: 'fit-content',
    },
    accountCard: {
        display: 'flex',
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: `${tokens.spacingVerticalM} ${tokens.spacingHorizontalM}`,
    },
    accountInfo: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalM,
    },
    accountText: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
    },
    connectedLabel: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground2,
    },
    accountEmail: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
    },
    checkmark: {
        color: tokens.colorPaletteGreenBackground3,
    },
    signInDifferent: {
        marginTop: tokens.spacingVerticalS,
    },
    teamsInfoContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
        marginTop: tokens.spacingVerticalM,
        padding: tokens.spacingVerticalM,
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusMedium,
    },
    readOnlyValue: {
        fontSize: tokens.fontSizeBase300,
        color: tokens.colorNeutralForeground1,
        padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
        backgroundColor: tokens.colorNeutralBackground1,
        borderRadius: tokens.borderRadiusMedium,
        border: `1px solid ${tokens.colorNeutralStroke1}`,
        wordBreak: 'break-all',
        overflowWrap: 'break-word',
        height: '16px',
    },
});
