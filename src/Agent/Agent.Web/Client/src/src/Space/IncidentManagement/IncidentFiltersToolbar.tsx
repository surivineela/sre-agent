import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
} from '@fluentui/react-components';
import {
    Add16Regular,
    ArrowClockwise16Regular,
    Delete16Regular,
    Dismiss16Regular,
    Play16Regular,
    Settings16Regular,
} from '@fluentui/react-icons';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { IncidentManagementResources, SreAgentResources, SreAgentTabResources } from '../../Strings/SREAgentResources';
import { useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';

export type IncidentsFilterToolbarProps = {
    onRefreshClick: () => void;
    onNewIncidentFilterClick: () => void;
    onDeleteIncidentFilterClick: () => void;
    onTurnOffIncidentFilterClick: () => void;
    onSettingsClick: () => void;
    isFilterSelected: boolean;
    isFilterEnabled: boolean;
    connected: boolean;
};

const IncidentFiltersToolbar: FC<IncidentsFilterToolbarProps> = ({
    onRefreshClick,
    onNewIncidentFilterClick,
    onDeleteIncidentFilterClick,
    onTurnOffIncidentFilterClick,
    onSettingsClick,
    isFilterSelected,
    isFilterEnabled,
    connected,
}) => {
    const intl = useIntl();
    const styles = useIncidentManagementStyles();

    return (
        <div className={styles.toolbar}>
            <Button
                icon={<Add16Regular />}
                appearance="transparent"
                className={styles.button}
                onClick={() => onNewIncidentFilterClick()}
                disabled={!connected}
            >
                {intl.formatMessage(IncidentManagementResources.newIncidentHandler)}
            </Button>
            <Button icon={<ArrowClockwise16Regular />} appearance="transparent" className={styles.button} onClick={() => onRefreshClick()}>
                {intl.formatMessage(IncidentManagementResources.refresh)}
            </Button>
            <Button icon={<Settings16Regular />} appearance="transparent" className={styles.button} onClick={() => onSettingsClick()}>
                {intl.formatMessage(SreAgentTabResources.settings)}
            </Button>
            <div className={styles.divider} />
            <Dialog modalType="alert">
                <DialogTrigger disableButtonEnhancement>
                    <Button
                        icon={<Delete16Regular />}
                        appearance="transparent"
                        className={styles.button}
                        disabled={!isFilterSelected || !connected}
                    >
                        {intl.formatMessage(SreAgentResources.delete)}
                    </Button>
                </DialogTrigger>
                <DialogSurface>
                    <DialogBody>
                        <DialogTitle>{intl.formatMessage(IncidentManagementResources.filterDeleteConfirmationTitle)}</DialogTitle>
                        <DialogContent>{intl.formatMessage(IncidentManagementResources.filterDeleteConfirmationMessage)}</DialogContent>
                        <DialogActions>
                            <DialogTrigger>
                                <Button className={styles.dangerButton} onClick={() => onDeleteIncidentFilterClick()}>
                                    {intl.formatMessage(SreAgentResources.yes)}
                                </Button>
                            </DialogTrigger>
                            <DialogTrigger disableButtonEnhancement>
                                <Button appearance="secondary">{intl.formatMessage(SreAgentResources.no)}</Button>
                            </DialogTrigger>
                        </DialogActions>
                    </DialogBody>
                </DialogSurface>
            </Dialog>
            {isFilterEnabled ? (
                <Dialog modalType="alert">
                    <DialogTrigger disableButtonEnhancement>
                        <Button
                            icon={<Dismiss16Regular />}
                            appearance="transparent"
                            className={styles.button}
                            disabled={!isFilterSelected || !connected}
                        >
                            {intl.formatMessage(IncidentManagementResources.turnOff)}
                        </Button>
                    </DialogTrigger>
                    <DialogSurface>
                        <DialogBody>
                            <DialogTitle>{intl.formatMessage(IncidentManagementResources.filterDisableConfirmationTitle)}</DialogTitle>
                            <DialogContent>
                                {intl.formatMessage(IncidentManagementResources.filterDisableConfirmationMessage)}
                            </DialogContent>
                            <DialogActions>
                                <DialogTrigger>
                                    <Button appearance="primary" onClick={() => onTurnOffIncidentFilterClick()}>
                                        {intl.formatMessage(SreAgentResources.yes)}
                                    </Button>
                                </DialogTrigger>
                                <DialogTrigger disableButtonEnhancement>
                                    <Button appearance="secondary">{intl.formatMessage(SreAgentResources.no)}</Button>
                                </DialogTrigger>
                            </DialogActions>
                        </DialogBody>
                    </DialogSurface>
                </Dialog>
            ) : (
                <Button
                    icon={<Play16Regular />}
                    appearance="transparent"
                    className={styles.button}
                    onClick={() => onTurnOffIncidentFilterClick()}
                    disabled={!isFilterSelected || !connected}
                >
                    {intl.formatMessage(IncidentManagementResources.turnOn)}
                </Button>
            )}
        </div>
    );
};

export default IncidentFiltersToolbar;
