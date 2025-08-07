import { FontWeights } from '@fluentui/react';
import { makeStyles, tokens } from '@fluentui/react-components';
import { CSSProperties } from 'react';

const noMcpServersContainer: CSSProperties = {
    display: 'flex',
    flexDirection: 'column',
    gap: '15px',
    alignItems: 'center',
    textAlign: 'center',
    justifyContent: 'center',
    height: '100vh',
};

const noMcpServersTitle: CSSProperties = { fontSize: 16, fontWeight: FontWeights.semibold };

const panelMainContainer: CSSProperties = { marginTop: '53px' };

const panelFooterContainer: CSSProperties = { padding: '12px' };

const panelFooterButton: CSSProperties = { marginRight: 8 };

const panelNavigationContainer: CSSProperties = { display: 'flex', alignItems: 'center', padding: '10px 12px' };

const panelNavigationTitle: CSSProperties = { margin: 0, marginLeft: 8 };

const createFormContainer: CSSProperties = {
    display: 'flex',
    flexDirection: 'column',
    gap: '15px',
    marginTop: '20px',
    maxWidth: '100%',
    overflowX: 'hidden',
    height: '100%',
};

const subscriptionDropdownContainer: CSSProperties = {
    width: '100%',
    display: 'block',
};

const subscriptionDropdownStyles: CSSProperties = {
    width: '250px',
    display: 'block',
};

const gridStyle: React.CSSProperties = {
    display: 'grid',
    gridTemplateColumns: '150px auto',
    rowGap: '15px',
    columnGap: '100px',
    alignItems: 'center',
    maxWidth: '600px',
};

const generalSettingsHeader: CSSProperties = { marginBottom: '20px', fontSize: '18px', fontWeight: 600 };

const accessControlSettingsContainer: CSSProperties = { display: 'flex', flexDirection: 'column', gap: '10px' };

const accessControlSettingsButton: CSSProperties = { width: 'fit-content' };

const navContainerStyles: CSSProperties = {
    display: 'flex',
    height: 'calc(100vh - 52px)',
    paddingTop: '8px',
    borderTop: '1px solid rgba(204, 204, 204, 0.8)',
    backgroundColor: tokens.colorNeutralBackground3,
};

const navPivotContainer: CSSProperties = {
    flex: 'none',
    padding: '32px',
    backgroundColor: tokens.colorNeutralBackground1,
    borderTopLeftRadius: tokens.borderRadiusXLarge,
    boxShadow: tokens.shadow4,
    width: 'calc(-405px + 100vw)',
    overflowY: 'auto',
};

const incidentManagementDescriptionStyle: CSSProperties = { marginTop: 20, marginBottom: 20 };

const pagerDutyWrapperStyle: CSSProperties = { display: 'flex', alignItems: 'center', marginTop: 20, marginBottom: 20 };
const pagerDutyLogoStyle: CSSProperties = { display: 'block', height: 20, marginTop: 5, marginBottom: 5 };

const azMonitorWrapperStyle: CSSProperties = { display: 'flex', alignItems: 'center', marginTop: 20, marginBottom: 20 };
const azMonitorLogoStyle: CSSProperties = { display: 'block', height: 30, marginRight: 10 };
const azMonitorNameStyle: CSSProperties = { fontWeight: tokens.fontWeightSemibold };

const connectedWrapperStyle: CSSProperties = { display: 'flex', alignItems: 'center', marginTop: 20, marginBottom: 20 };
const connectedImageStyle: CSSProperties = { display: 'block', height: 15, marginRight: 10 };

const buttonsWrapperStyle: CSSProperties = { marginTop: 30 };

const deleteButtonStyle: CSSProperties = { marginTop: '20px' };

const controlStyles = {
    width: 350,
    bordeRadius: tokens.borderRadiusLarge,
};

const dropdownStyles = {
    ...controlStyles,
};

const secureTextFieldStyles = {
    ...controlStyles,
    fontFamily: 'monospace',
    WebkitTextSecurity: 'disc', // Safari-specific obfuscation
    textSecurity: 'disc', // Obfuscates text in supported browsers
};

const plainTextFieldStyles = {
    ...controlStyles,
    fontFamily: 'monospace',
};

export const useSettingsStyles = () => {
    return {
        noMcpServersContainer,
        noMcpServersTitle,
        panelMainContainer,
        panelFooterContainer,
        panelFooterButton,
        panelNavigationContainer,
        panelNavigationTitle,
        createFormContainer,
        subscriptionDropdownContainer,
        subscriptionDropdownStyles,
        gridStyle,
        generalSettingsHeader,
        accessControlSettingsContainer,
        accessControlSettingsButton,
        navContainerStyles,
        navPivotContainer,
        incidentManagementDescriptionStyle,
        pagerDutyWrapperStyle,
        pagerDutyLogoStyle,
        azMonitorWrapperStyle,
        azMonitorLogoStyle,
        azMonitorNameStyle,
        connectedWrapperStyle,
        connectedImageStyle,
        buttonsWrapperStyle,
        deleteButtonStyle,
        controlStyles,
        dropdownStyles,
        secureTextFieldStyles,
        plainTextFieldStyles,
    };
};

export const usePagerDutyStyles = makeStyles({
    greenCheckIcon: { height: '16px', width: '16px', color: tokens.colorPaletteGreenForeground1 },
    iconContainer: {
        display: 'flex',
        alignItems: 'center',
        gap: '4px',
        paddingBottom: '20px',
    },
    messageBar: {
        display: 'flex',
        alignItems: 'flex-start',
        padding: '10px',
        maxWidth: '800px',
        width: '100%',
        marginBottom: '20px',
        backgroundColor: tokens.colorNeutralBackground2,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    messageBarBody: { display: 'flex', flexDirection: 'column', justifyContent: 'space-between', gap: '10px', width: '100%' },
    messageBarActionsContainer: { display: 'flex', justifyContent: 'flex-end', width: '100%' },
});

export const useDialogStyles = makeStyles({
    dangerButton: {
        backgroundColor: tokens.colorStatusDangerBackground3,
        color: `${tokens.colorNeutralForegroundInverted} !important`,
        ':hover': {
            backgroundColor: tokens.colorStatusDangerBackground3Hover,
        },
        ':active': {
            backgroundColor: tokens.colorStatusDangerBackground3Pressed,
        },
    },
});

export const commandBarStyles = {
    root: {
        borderBottom: 'none',
    },
};

export const navStyles = {
    root: {
        width: 300,
        marginLeft: 20,
        marginRight: 20,
        marginTop: 20,
    },
    compositeLink: {
        backgroundColor: 'transparent',
        selectors: {
            '&.is-selected': {
                backgroundColor: tokens.colorNeutralBackground3Selected,
            },
            '&:hover': {
                backgroundColor: tokens.colorNeutralBackground3Hover,
            },
        },
        height: 32,
        borderRadius: 4,
    },
    link: {
        paddingLeft: 5,
        backgroundColor: 'transparent !important',
        selectors: {
            '&:after': {
                borderLeftWidth: 3,
                inset: '5px 0',
                height: 20,
                width: 4,
                borderRadius: 2,
            },
        },
        height: 32,
    },
};
