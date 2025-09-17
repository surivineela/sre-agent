import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
    Tooltip,
} from '@fluentui/react-components';
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
    connected: boolean;
    canWriteIncidentManagement?: boolean; // optional to keep backward compatibility
    canDeleteIncidentManagement?: boolean; // optional separate delete permission
};

const IncidentFiltersToolbar: FC<IncidentsFilterToolbarProps> = ({
    onRefreshClick,
    onNewIncidentFilterClick,
    onDeleteIncidentFilterClick,
    onTurnOffIncidentFilterClick,
    isFilterSelected,
    isFilterEnabled,
    connected,
    canWriteIncidentManagement = true,
    canDeleteIncidentManagement = true,
}) => {
    const intl = useIntl();
    const styles = useIncidentManagementStyles();

    return (
        <div className={styles.toolbar}>
            {(() => {
                const tooltipMsg = !connected
                    ? null
                    : !canWriteIncidentManagement
                      ? intl.formatMessage(IncidentManagementResources.noPermissionNewIncidentHandler)
                      : null;
                const btn = (
                    <Button
                        icon={<Add16Regular />}
                        appearance="transparent"
                        className={styles.button}
                        onClick={() => onNewIncidentFilterClick()}
                        disabled={!connected || !canWriteIncidentManagement}
                    >
                        {intl.formatMessage(IncidentManagementResources.newIncidentHandler)}
                    </Button>
                );
                return tooltipMsg ? (
                    <Tooltip relationship="label" content={tooltipMsg}>
                        {btn}
                    </Tooltip>
                ) : (
                    btn
                );
            })()}
            <Button icon={<ArrowClockwise16Regular />} appearance="transparent" className={styles.button} onClick={() => onRefreshClick()}>
                {intl.formatMessage(IncidentManagementResources.refresh)}
            </Button>
            <div className={styles.divider} />
            <Dialog modalType="alert">
                <DialogTrigger disableButtonEnhancement>
                    {(() => {
                        const tooltipMsg =
                            !connected || !isFilterSelected
                                ? null
                                : !canDeleteIncidentManagement
                                  ? intl.formatMessage(IncidentManagementResources.noPermissionDeleteIncidentHandler)
                                  : null;
                        const btn = (
                            <Button
                                icon={<Delete16Regular />}
                                appearance="transparent"
                                className={styles.button}
                                disabled={!isFilterSelected || !connected || !canDeleteIncidentManagement}
                            >
                                {intl.formatMessage(SreAgentResources.delete)}
                            </Button>
                        );
                        return tooltipMsg ? (
                            <Tooltip relationship="label" content={tooltipMsg}>
                                {btn}
                            </Tooltip>
                        ) : (
                            btn
                        );
                    })()}
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
                        {(() => {
                            const tooltipMsg =
                                !connected || !isFilterSelected
                                    ? null
                                    : !canWriteIncidentManagement
                                      ? intl.formatMessage(IncidentManagementResources.noPermissionTurnOffIncidentHandler)
                                      : null;
                            const btn = (
                                <Button
                                    icon={<Dismiss16Regular />}
                                    appearance="transparent"
                                    className={styles.button}
                                    disabled={!isFilterSelected || !connected || !canWriteIncidentManagement}
                                >
                                    {intl.formatMessage(IncidentManagementResources.turnOff)}
                                </Button>
                            );
                            return tooltipMsg ? (
                                <Tooltip relationship="label" content={tooltipMsg}>
                                    {btn}
                                </Tooltip>
                            ) : (
                                btn
                            );
                        })()}
                    </DialogTrigger>
                    <DialogSurface>
                        <DialogBody>
                            <DialogTitle>{intl.formatMessage(IncidentManagementResources.filterDisableConfirmationTitle)}</DialogTitle>
                            <DialogContent>
                                {intl.formatMessage(IncidentManagementResources.filterDisableConfirmationMessage)}
                            </DialogContent>
                            <DialogActions>
                                <DialogTrigger>
                                    <Button
                                        appearance="primary"
                                        onClick={() => onTurnOffIncidentFilterClick()}
                                        disabled={!canWriteIncidentManagement}
                                    >
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
                (() => {
                    const tooltipMsg =
                        !connected || !isFilterSelected
                            ? null
                            : !canWriteIncidentManagement
                              ? intl.formatMessage(IncidentManagementResources.noPermissionTurnOnIncidentHandler)
                              : null;
                    const btn = (
                        <Button
                            icon={<Play16Regular />}
                            appearance="transparent"
                            className={styles.button}
                            onClick={() => onTurnOffIncidentFilterClick()}
                            disabled={!isFilterSelected || !connected || !canWriteIncidentManagement}
                        >
                            {intl.formatMessage(IncidentManagementResources.turnOn)}
                        </Button>
                    );
                    return tooltipMsg ? (
                        <Tooltip relationship="label" content={tooltipMsg}>
                            {btn}
                        </Tooltip>
                    ) : (
                        btn
                    );
                })()
            )}
        </div>
    );
};

export default IncidentFiltersToolbar;
