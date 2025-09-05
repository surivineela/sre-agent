import { Button } from '@fluentui/react-components';
import { Add16Regular, ArrowClockwise16Regular } from '@fluentui/react-icons';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources, SubAgentsResources } from '../../Strings/SREAgentResources';
import { useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';

export type SubAgentsToolbarProps = {
    onRefreshClick: () => void;
    onNewSubAgentClick: () => void;
    isOperationInProgress?: boolean;
};

const SubAgentsToolbar: FC<SubAgentsToolbarProps> = ({ onRefreshClick, onNewSubAgentClick, isOperationInProgress = false }) => {
    const intl = useIntl();
    const styles = useIncidentManagementStyles();

    return (
        <div className={styles.toolbar}>
            <Button
                icon={<Add16Regular />}
                appearance="transparent"
                className={styles.button}
                onClick={() => {
                    onNewSubAgentClick();
                }}
                disabled={isOperationInProgress}
            >
                {intl.formatMessage(SubAgentsResources.createSubAgent)}
            </Button>
            <Button
                icon={<ArrowClockwise16Regular />}
                appearance="transparent"
                className={styles.button}
                onClick={() => {
                    onRefreshClick();
                }}
                disabled={isOperationInProgress}
            >
                {intl.formatMessage(SreAgentResources.refresh)}
            </Button>
            <div className={styles.divider} />
        </div>
    );
};

export default SubAgentsToolbar;
