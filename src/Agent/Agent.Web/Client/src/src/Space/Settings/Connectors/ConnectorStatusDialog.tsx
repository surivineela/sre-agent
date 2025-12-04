import { Button, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, Text, tokens } from '@fluentui/react-components';
import { useIntl } from 'react-intl';
import { ConnectorStatus } from '../../../Common/Contracts/Azure/SreAgent';
import { ConnectorsResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { useConnectorsStyles } from './Connectors.styles';
import { getStatusIcon } from './ConnectorStatusUtils';

interface ConnectorStatusDialogProps {
    isOpen: boolean;
    onOpenChange: (open: boolean) => void;
    connectorStatus: ConnectorStatus | null;
}

export const ConnectorStatusDialog = ({ isOpen, onOpenChange, connectorStatus }: ConnectorStatusDialogProps) => {
    const intl = useIntl();
    const styles = useConnectorsStyles();

    if (!connectorStatus) {
        return null;
    }

    const { icon } = getStatusIcon(connectorStatus.status);

    return (
        <Dialog open={isOpen} onOpenChange={(_, data) => onOpenChange(data.open)}>
            <DialogSurface>
                <DialogBody>
                    <DialogContent>
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
                            <div className={styles.iconAndTextContainer}>
                                {icon}
                                <Text weight="semibold" size={400}>
                                    {connectorStatus.status}
                                </Text>
                            </div>
                            <div>
                                <Text>{connectorStatus.message}</Text>
                            </div>
                            {connectorStatus.details && (
                                <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                                    {connectorStatus.details.tools !== undefined && (
                                        <Text size={200}>
                                            {intl.formatMessage(ConnectorsResources.toolCount, { count: connectorStatus.details.tools })}
                                        </Text>
                                    )}
                                    {connectorStatus.details.lastHeartbeat && (
                                        <Text size={200}>
                                            {intl.formatMessage(ConnectorsResources.lastHeartbeat, {
                                                time: new Date(connectorStatus.details.lastHeartbeat).toLocaleString(),
                                            })}
                                        </Text>
                                    )}
                                    {connectorStatus.details.error && (
                                        <Text size={200} style={{ color: tokens.colorPaletteRedForeground1 }}>
                                            {intl.formatMessage(ConnectorsResources.error)}: {connectorStatus.details.error}
                                        </Text>
                                    )}
                                </div>
                            )}
                        </div>
                    </DialogContent>
                    <DialogActions>
                        <Button appearance="primary" onClick={() => onOpenChange(false)}>
                            {intl.formatMessage(SreAgentResources.close)}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
