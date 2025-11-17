import { Button, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle } from '@fluentui/react-components';
import { memo } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { useListViewStyles } from '../ExtendedAgentTableView.Styles';

interface EntityDeleteConfirmDialogProps {
    showDialog: boolean;
    setShowDialog: (show: boolean) => void;
    numItems: number;
    handleDelete: () => Promise<void>;
}

export const EntityDeleteConfirmDialog = memo<EntityDeleteConfirmDialogProps>(({ showDialog, setShowDialog, numItems, handleDelete }) => {
    const intl = useIntl();
    const styles = useListViewStyles();

    return (
        <Dialog open={showDialog} onOpenChange={(_, data) => setShowDialog(data.open)}>
            <DialogSurface>
                <DialogBody>
                    <DialogTitle>{intl.formatMessage(ExtendedAgentsGraphResources.deleteConfirmTitle)}</DialogTitle>
                    <DialogContent>
                        {intl.formatMessage(ExtendedAgentsGraphResources.deleteConfirmMessage, { count: numItems })}
                    </DialogContent>
                    <DialogActions>
                        <Button
                            appearance="primary"
                            onClick={() => {
                                setShowDialog(false);
                                handleDelete();
                            }}
                            className={styles.dangerButton}
                        >
                            {intl.formatMessage(SreAgentResources.delete)}
                        </Button>
                        <Button appearance="secondary" onClick={() => setShowDialog(false)}>
                            {intl.formatMessage(SreAgentResources.cancel)}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
});
