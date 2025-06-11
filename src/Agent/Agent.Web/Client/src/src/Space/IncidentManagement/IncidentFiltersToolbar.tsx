import { Button } from '@fluentui/react-components';
import { Add16Regular, ArrowClockwise16Regular, Delete16Regular, Dismiss16Regular, Play16Regular } from '@fluentui/react-icons';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { IncidentManagementResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';

export type IncidentsFilterToolbarProps = {
    onRefreshClick: () => void;
    onNewIncidentFilterClick: () => void;
    onDeleteIncidentFilterClick: () => void;
    onTurnOffIncidentFilterClick: () => void;
    isFilterSelected: boolean;
    isFilterEnabled: boolean;
};

const IncidentFiltersToolbar: FC<IncidentsFilterToolbarProps> = ({
    onRefreshClick,
    onNewIncidentFilterClick,
    onDeleteIncidentFilterClick,
    onTurnOffIncidentFilterClick,
    isFilterSelected,
    isFilterEnabled,
}) => {
    const intl = useIntl();
    const styles = useIncidentManagementStyles();

    return (
        <div className={styles.toolbar}>
            <Button
                icon={<Add16Regular />}
                appearance="transparent"
                className={styles.button}
                onClick={() => {
                    onNewIncidentFilterClick();
                }}
            >
                {intl.formatMessage(IncidentManagementResources.newIncidentHandler)}
            </Button>
            <Button
                icon={<ArrowClockwise16Regular />}
                appearance="transparent"
                className={styles.button}
                onClick={() => {
                    onRefreshClick();
                }}
            >
                {intl.formatMessage(IncidentManagementResources.refresh)}
            </Button>
            <div className={styles.divider} />
            <Button
                icon={<Delete16Regular />}
                appearance="transparent"
                className={styles.button}
                onClick={() => {
                    onDeleteIncidentFilterClick();
                }}
                disabled={!isFilterSelected}
            >
                {intl.formatMessage(SreAgentResources.delete)}
            </Button>
            <Button
                icon={isFilterEnabled ? <Dismiss16Regular /> : <Play16Regular />}
                appearance="transparent"
                className={styles.button}
                onClick={() => {
                    onTurnOffIncidentFilterClick();
                }}
                disabled={!isFilterSelected}
            >
                {isFilterEnabled
                    ? intl.formatMessage(IncidentManagementResources.turnOff)
                    : intl.formatMessage(IncidentManagementResources.turnOn)}
            </Button>
        </div>
    );
};

export default IncidentFiltersToolbar;
