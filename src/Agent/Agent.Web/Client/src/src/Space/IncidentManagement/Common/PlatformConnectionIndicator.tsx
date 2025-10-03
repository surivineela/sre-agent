import { Shimmer } from '@fluentui/react';
import { Spinner } from '@fluentui/react-components';
import { CheckmarkCircle16Filled, Warning16Filled } from '@fluentui/react-icons';
import { tokens } from '@fluentui/react-theme';
import { FC, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { IncidentManagementType } from '../../../Common/Contracts/Azure/SreAgent';
import {
    AzMonitorResources,
    IcMResources,
    IncidentManagementResources,
    PagerDutyResources,
    ServiceNowResources,
} from '../../../Strings/SREAgentResources';
import { SreAgentContext } from '../../Contracts/Context';

const styles = {
    root: {
        display: 'flex',
        alignItems: 'center',
        gap: '4px',
    },
    checkmarkCircle: { height: '16px', width: '16px', color: tokens.colorPaletteGreenForeground1 },
    warning: { height: '16px', width: '16px', color: tokens.colorPaletteGreenForeground1 },
    spinner: { height: '16px', width: '16px' },
};

export interface PlatformConnectionIndicatorProps {
    style?: React.CSSProperties | undefined;
    includeHandlersMessage?: boolean;
}

export const PlatformConnectionIndicator: FC<PlatformConnectionIndicatorProps> = ({ style, includeHandlersMessage }) => {
    const intl = useIntl();
    const {
        incidentManagement: { incidentPlatformType, incidentManagementConnectionState, checkingConnectivity, hasFilters },
    } = useContext(SreAgentContext);

    const { notConnectedMessage, waitingForConnectivityMessage, connectedMessage } = useMemo(() => {
        switch (incidentPlatformType) {
            case IncidentManagementType.PagerDuty:
                return {
                    notConnectedMessage: PagerDutyResources.notConnectedMessage,
                    waitingForConnectivityMessage: PagerDutyResources.connectingMessage,
                    connectedMessage:
                        !hasFilters && includeHandlersMessage
                            ? PagerDutyResources.connectedMessageWithoutHandlers
                            : PagerDutyResources.connectedMessage,
                };
            case IncidentManagementType.Icm:
                return {
                    notConnectedMessage: IcMResources.notConnectedMessage,
                    waitingForConnectivityMessage: IcMResources.connectingMessage,
                    connectedMessage:
                        !hasFilters && includeHandlersMessage
                            ? IcMResources.connectedMessageWithoutHandlers
                            : IcMResources.connectedMessage,
                };
            case IncidentManagementType.ServiceNow:
                return {
                    notConnectedMessage: ServiceNowResources.notConnectedMessage,
                    waitingForConnectivityMessage: ServiceNowResources.connectingMessage,
                    connectedMessage:
                        !hasFilters && includeHandlersMessage
                            ? ServiceNowResources.connectedMessageWithoutHandlers
                            : ServiceNowResources.connectedMessage,
                };
            case IncidentManagementType.AzMonitor:
                return {
                    notConnectedMessage: AzMonitorResources.notConnectedMessage,
                    waitingForConnectivityMessage: AzMonitorResources.connectingMessage,
                    connectedMessage:
                        !hasFilters && includeHandlersMessage
                            ? AzMonitorResources.connectedMessageWithoutHandlers
                            : AzMonitorResources.connectedMessage,
                };
            default:
                return {
                    notConnectedMessage: undefined,
                    waitingForConnectivityMessage: undefined,
                    connectedMessage: undefined,
                };
        }
    }, [incidentPlatformType, hasFilters, includeHandlersMessage]);

    if (!incidentManagementConnectionState || !notConnectedMessage || !waitingForConnectivityMessage || !connectedMessage) {
        return null;
    }

    return (
        <div style={{ ...styles.root, ...style }}>
            {checkingConnectivity ? (
                <Shimmer width={160} />
            ) : incidentManagementConnectionState === 'connected' ? (
                <>
                    <CheckmarkCircle16Filled
                        style={styles.checkmarkCircle}
                        aria-label={intl.formatMessage(IncidentManagementResources.connected)}
                    />
                    <div>{intl.formatMessage(connectedMessage)}</div>
                </>
            ) : incidentManagementConnectionState === 'waiting' ? (
                <>
                    <Spinner
                        size="tiny"
                        style={styles.spinner}
                        spinner={{ style: styles.spinner }}
                        aria-label={intl.formatMessage(IncidentManagementResources.waitingForConnectivity)}
                    />
                    <div>{intl.formatMessage(waitingForConnectivityMessage)}</div>
                </>
            ) : (
                <>
                    <Warning16Filled style={styles.warning} aria-label={intl.formatMessage(IncidentManagementResources.notConnected)} />
                    <div>{intl.formatMessage(notConnectedMessage)}</div>
                </>
            )}
        </div>
    );
};
