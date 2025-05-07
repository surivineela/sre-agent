import { mergeStyleSets } from '@fluentui/react';
import { CSSProperties } from 'react';

const container: CSSProperties = {
    display: 'flex',
    flexDirection: 'column',
    gap: '20px',
};

const titleText: CSSProperties = {
    fontSize: '16px',
    fontWeight: '500',
    marginLeft: '-5px',
};

const linkedContainer: CSSProperties = {
    display: 'flex',
    flexDirection: 'column',
    gap: '15px',
};

const rowCenterAlign: CSSProperties = {
    display: 'flex',
    flexDirection: 'row',
    alignItems: 'center',
    width: '75%',
};

const rbacContainer: CSSProperties = {
    display: 'flex',
    flexDirection: 'column',
    gap: '20px',
};

const stepRow: CSSProperties = {
    display: 'flex',
    flexDirection: 'row',
    gap: '5px',
};

const nowrapBold: CSSProperties = {
    whiteSpace: 'nowrap',
};

const apiKeyRow: CSSProperties = {
    display: 'flex',
    flexDirection: 'row',
    gap: '50px',
    maxWidth: '500px',
};

const inputFieldLabel: CSSProperties = { height: '50px', width: '600px', columnGap: '40px' };

const displayFieldLabel: CSSProperties = { marginRight: '100px' };

const inputTextField: CSSProperties = {
    width: '400px',
};

const apiKeyInput: CSSProperties = {
    width: '400px',
};

const buttonRow: CSSProperties = {
    display: 'flex',
    flexDirection: 'row',
    gap: '8px',
    marginTop: '10px',
};

const grafanaLogo: CSSProperties = {
    display: 'flex',
    flexDirection: 'row',
    gap: '5px',
    alignItems: 'center',
};

const createContainer: CSSProperties = {
    display: 'flex',
    flexDirection: 'column',
    gap: '15px',
};

const gridStyle: React.CSSProperties = {
    display: 'grid',
    gridTemplateColumns: '150px auto',
    rowGap: '15px',
    columnGap: '100px',
    alignItems: 'center',
    maxWidth: '750px',
};

const grafanaUrlContainer: CSSProperties = {
    maxWidth: '1000px',
    marginTop: '10px',
};

const grafanaUrlLinkContainer: CSSProperties = {
    marginLeft: '75px',
};

const grafanaUrlLabelContainer: CSSProperties = {
    display: 'flex',
    flexDirection: 'row',
    gap: '5px',
    alignItems: 'center',
    marginTop: '-4px',
};

const messageBar: CSSProperties = {
    width: '75%',
};

export const useGrafanaDashboardStyles = () =>
    mergeStyleSets({
        container,
        titleText,
        linkedContainer,
        rowCenterAlign,
        rbacContainer,
        stepRow,
        nowrapBold,
        apiKeyRow,
        apiKeyInput,
        buttonRow,
        grafanaLogo,
        createContainer,
        gridStyle,
        displayFieldLabel,
        inputFieldLabel,
        inputTextField,
        grafanaUrlContainer,
        grafanaUrlLabelContainer,
        messageBar,
        grafanaUrlLinkContainer,
    });
