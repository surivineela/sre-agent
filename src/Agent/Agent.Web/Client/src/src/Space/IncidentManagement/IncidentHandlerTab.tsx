import { Button, Dropdown, Input } from '@fluentui/react-components';
import { Add16Regular, Delete16Regular } from '@fluentui/react-icons';
import { FC, useCallback } from 'react';
import { useIntl } from 'react-intl';
import { IncidentManagementResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';
import IncidentHandlerDetailsList from './IncidentHandlerDetailsList';

const IncidentHandlerTab: FC = () => {
    const intl = useIntl();
    const styles = useIncidentManagementStyles();

    const onNewIncidentHandlerClick = useCallback(async () => {
        //TODO: new incident handler
    }, []);

    return (
        <div className={styles.tabRoot}>
            <div className={styles.toolbar}>
                <div style={{ display: 'flex', gap: '8px' }}>
                    <Button icon={<Add16Regular />} appearance="transparent" className={styles.button} onClick={onNewIncidentHandlerClick}>
                        {intl.formatMessage(IncidentManagementResources.newIncidentHandler)}
                    </Button>
                    <div className={styles.divider} />
                    <Button icon={<Delete16Regular />} appearance="transparent" className={styles.button}>
                        {intl.formatMessage(SreAgentResources.delete)}
                    </Button>
                </div>
            </div>
            <div className={styles.filters}>
                <Input placeholder="Search" className={styles.input} />
                <Dropdown placeholder="All severity" className={styles.dropdown}></Dropdown>
            </div>
            <div>
                <IncidentHandlerDetailsList incidentHandlers={[]} incidentHandlersLoading={false} />
            </div>
        </div>
    );
};

export default IncidentHandlerTab;
