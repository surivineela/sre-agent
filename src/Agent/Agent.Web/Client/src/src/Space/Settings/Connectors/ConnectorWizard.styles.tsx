import { makeStyles, tokens } from '@fluentui/react-components';

export const useConnectorWizardStyles = makeStyles({
    searchBox: {
        width: '350px',
    },
    cardContainer: {
        padding: '10px 0',
        height: '380px',
        overflow: 'auto',
    },
    cardGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(3, 1fr)',
        gap: tokens.spacingHorizontalL,
        padding: '3px',
    },
    cardContent: {
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'space-between',
    },
    image: {
        width: '32px',
        height: '32px',
    },
    serviceDescription: {
        color: '#666',
        display: 'block',
    },
    serviceMoreInfoText: {
        marginTop: tokens.spacingVerticalS,
    },
    wizardContentContainer: {
        padding: tokens.spacingVerticalXXL,
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
    reviewAndAddSectionValue: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightRegular,
    },
});
