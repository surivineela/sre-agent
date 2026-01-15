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
import { useCallback, useState } from 'react';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../../Strings/Resources';
import { AgentSpaceAllowedAction, AllowedActionRow } from '../../Contracts/AgentSpace';
import { newShortGuid } from '../../Utilities/Guid';
import { AllowedActionsTable } from './AllowedActionsTable';

const useStyles = makeStyles({
    description: {
        marginBottom: tokens.spacingVerticalM,
        color: tokens.colorNeutralForeground2,
    },
    dialogContent: {
        minWidth: '500px',
    },
});

interface AddAllowedActionsDialogProps {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    onAdd: (actions: AgentSpaceAllowedAction[]) => void;
    isSubmitting?: boolean;
}

export const AddAllowedActionsDialog = ({ open, onOpenChange, onAdd, isSubmitting = false }: AddAllowedActionsDialogProps) => {
    const intl = useIntl();
    const styles = useStyles();

    const createEmptyRow = useCallback(
        (): AllowedActionRow => ({
            id: newShortGuid(),
            actionName: '',
            extension: '',
            approvalRequired: false,
        }),
        []
    );

    const [rows, setRows] = useState<AllowedActionRow[]>([createEmptyRow()]);

    const resetForm = useCallback(() => {
        setRows([createEmptyRow()]);
    }, [createEmptyRow]);

    const handleOpenChange = useCallback(
        (newOpen: boolean) => {
            onOpenChange(newOpen);
            if (!newOpen) {
                resetForm();
            }
        },
        [onOpenChange, resetForm]
    );

    const handleAdd = useCallback(() => {
        const validActions = rows
            .filter(row => row.actionName.trim() && row.extension.trim())
            .map(
                (row): AgentSpaceAllowedAction => ({
                    actionName: row.actionName.trim(),
                    extension: row.extension.trim(),
                    approvalRequired: row.approvalRequired,
                })
            );

        if (validActions.length > 0) {
            onAdd(validActions);
        }
    }, [rows, onAdd]);

    const hasValidActions = rows.some(row => row.actionName.trim() && row.extension.trim());

    return (
        <Dialog open={open} onOpenChange={(_, data) => handleOpenChange(!!data.open)}>
            <DialogSurface>
                <DialogBody>
                    <DialogTitle>{intl.formatMessage(PortalResources.addAllowedActions)}</DialogTitle>
                    <DialogContent className={styles.dialogContent}>
                        <Text className={styles.description}>{intl.formatMessage(PortalResources.addAllowedActionsDescription)}</Text>
                        <AllowedActionsTable rows={rows} onChange={setRows} disabled={isSubmitting} />
                    </DialogContent>
                    <DialogActions>
                        <Button appearance="secondary" onClick={() => handleOpenChange(false)} disabled={isSubmitting}>
                            {intl.formatMessage(PortalResources.cancel)}
                        </Button>
                        <Button appearance="primary" onClick={handleAdd} disabled={!hasValidActions || isSubmitting}>
                            {intl.formatMessage(PortalResources.add)}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
