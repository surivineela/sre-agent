import { MessageBar, MessageBarBody, MessageBarGroup } from '@fluentui/react-components';
import { FC, useContext, useMemo } from 'react';
import useIntl from 'react-intl/src/components/useIntl';
import { IncidentManagementType } from '../../../Common/Contracts/Azure/SreAgent';
import { AzMonitorResources, IcMResources, PagerDutyResources, ServiceNowResources } from '../../../Strings/SREAgentResources';
import { SreAgentContext } from '../../Contracts/Context';

export const PlatformConnectionMessageBar: FC = () => {
    const intl = useIntl();
    const {
        incidentManagement: { incidentPlatformType, incidentManagementConnectionState, checkingConnectivity },
    } = useContext(SreAgentContext);

    const connectionFailureMessage = useMemo(() => {
        switch (incidentPlatformType) {
            case IncidentManagementType.PagerDuty:
                return intl.formatMessage(PagerDutyResources.connectionFailureMessage);
            case IncidentManagementType.Icm:
                return intl.formatMessage(IcMResources.connectionFailureMessage);
            case IncidentManagementType.ServiceNow:
                return intl.formatMessage(ServiceNowResources.connectionFailureMessage);
            case IncidentManagementType.AzMonitor:
                return intl.formatMessage(AzMonitorResources.connectionFailureMessage);
            default:
                return undefined;
        }
    }, [incidentPlatformType, intl]);

    const showMessageBar = useMemo(() => {
        return !checkingConnectivity && incidentManagementConnectionState === 'notConnected' && connectionFailureMessage;
    }, [checkingConnectivity, incidentManagementConnectionState, connectionFailureMessage]);

    return showMessageBar ? (
        <MessageBarGroup
            animate={'exit-only'}
            style={{
                width: '100%',
                maxWidth: '100%',
                marginBottom: '16px',
            }}
        >
            <MessageBar
                style={{
                    padding: '10px',
                    whiteSpace: 'normal',
                    wordBreak: 'break-word',
                    overflow: 'hidden',
                    overflowWrap: 'break-word',
                }}
                intent={'error'}
            >
                <MessageBarBody
                    style={{
                        wordBreak: 'break-word',
                        overflowWrap: 'break-word',
                    }}
                >
                    {connectionFailureMessage}
                </MessageBarBody>
            </MessageBar>
        </MessageBarGroup>
    ) : null;
};
