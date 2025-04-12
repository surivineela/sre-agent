import { FontWeights } from '@fluentui/react';
import { CSSProperties } from 'react';
import { tokens } from '@fluentui/react-components';

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

const navContainer: CSSProperties = {
  display: 'flex',
  height: '100vh',
  paddingTop: '0.5rem',
  borderTop: '1px solid rgba(204, 204, 204, 0.8)',
  backgroundColor: tokens.colorNeutralBackground3,
};

const navPivotContainer: CSSProperties = {
  flex: 1,
  padding: '2rem',
  backgroundColor: tokens.colorNeutralBackground1,
  borderTopLeftRadius: tokens.borderRadiusXLarge,
  marginLeft: 30,
};

const incidentManagementDescriptionStyle: CSSProperties = { marginTop: 20, marginBottom: 20 };

const pagerDutyLogoStyle: CSSProperties = { display: "block", height: 20, marginTop: 20, marginBottom: 20 };

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
    navContainer,
    navPivotContainer,
    incidentManagementDescriptionStyle,
    pagerDutyLogoStyle,
  };
};

export const commandBarStyles = {
  root: {
    borderBottom: 'none',
  },
};

export const navStyles = {
  root: {
    width: 300,
    marginLeft: 30,
  },
  link: {
    paddingLeft: 5,
    selectors: {
      '&:after': {
        borderLeftWidth: 3,
        inset: '5px 0',
      },
    },
  },
};

export const incidentManagementTextFieldStyles = {
  wrapper: {
      width: 700,
      display: 'flex',
      marginTop: 20,
      marginBottom: 20,
  },
  subComponentStyles: {
      label: {
          root: {
              width: 240,
          },
      }
  },
  fieldGroup: {
      width: 460,
      borderRadius: tokens.borderRadiusLarge,
  },
};

export const incidentManagementDropdownStyles = {
  root: {
      width: 700,
      display: 'flex',
      marginTop: 20,
      marginBottom: 20,
  },
  label: {
      width: 240,
  },
  title: {
    borderRadius: tokens.borderRadiusLarge,
  },
  dropdown: {
      width: 460
  },
};
