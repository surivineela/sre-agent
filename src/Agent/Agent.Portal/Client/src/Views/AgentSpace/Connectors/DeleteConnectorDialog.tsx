import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    makeStyles,
    Text,
    tokens,
} from '@fluentui/react-components';
import { useMemo } from 'react';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../../Strings/Resources';

const useStyles = makeStyles({
    content: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    connectorList: {
        marginTop: tokens.spacingVerticalS,
        paddingLeft: tokens.spacingHorizontalL,
    },
    connectorItem: {
        marginBottom: tokens.spacingVerticalXS,
    },
});

interface DeleteConnectorDialogProps {
    isOpen: boolean;
    onClose: () => void;
    connectorNames: string[];
    onConfirm: () => void;
    isDeleting?: boolean;
}

const MAX_DISPLAYED_NAMES = 5;

export const DeleteConnectorDialog = ({ isOpen, onClose, connectorNames, onConfirm, isDeleting = false }: DeleteConnectorDialogProps) => {
    const intl = useIntl();
    const styles = useStyles();

    const displayedNames = useMemo(() => connectorNames.slice(0, MAX_DISPLAYED_NAMES), [connectorNames]);
    const remainingCount = useMemo(() => Math.max(0, connectorNames.length - MAX_DISPLAYED_NAMES), [connectorNames]);

    const title = useMemo(() => {
        if (connectorNames.length === 1) {
            return intl.formatMessage(PortalResources.deleteConnector);
        }
        return intl.formatMessage(PortalResources.deleteConnectors, { count: connectorNames.length });
    }, [connectorNames.length, intl]);

    return (
        <Dialog open={isOpen} onOpenChange={(_, data) => !data.open && onClose()}>
            <DialogSurface>
                <DialogBody>
                    <DialogTitle>{title}</DialogTitle>
                    <DialogContent>
                        <div className={styles.content}>
                            <Text>{intl.formatMessage(PortalResources.deleteConnectorConfirmation)}</Text>
                            <ul className={styles.connectorList}>
                                {displayedNames.map(name => (
                                    <li key={name} className={styles.connectorItem}>
                                        <Text weight="semibold">{name}</Text>
                                    </li>
                                ))}
                                {remainingCount > 0 && (
                                    <li className={styles.connectorItem}>
                                        <Text italic>{intl.formatMessage(PortalResources.andMoreItems, { count: remainingCount })}</Text>
                                    </li>
                                )}
                            </ul>
                        </div>
                    </DialogContent>
                    <DialogActions>
                        <Button appearance="secondary" onClick={onClose} disabled={isDeleting}>
                            {intl.formatMessage(PortalResources.cancel)}
                        </Button>
                        <Button appearance="primary" onClick={onConfirm} disabled={isDeleting}>
                            {intl.formatMessage(PortalResources.delete)}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
