import { Button, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle, Text } from '@fluentui/react-components';
import { Dismiss20Regular, DocumentText16Regular } from '@fluentui/react-icons';
import * as React from 'react';
import { useIntl } from 'react-intl';
import { DeleteConfirmationDialogResources } from '../../../Strings/SREAgentResources';
import { connectorTypeOptions as getConnectorTypeOptions } from '../Connectors/Wizard/Common/ConnectorType';
import { useDataKnowledgeSpaceStyles, useDeleteConfirmationDialogStyles } from '../Styles/DataKnowledgeSpace.styles';
interface DeleteConfirmationDialogProps {
    isOpen: boolean;
    onOpenChange: (open: boolean) => void;
    onConfirmDelete: () => void;
    onCancelDelete: () => void;
    itemType: string;
    actionVerb: string;
    title?: string;
    message?: string;
    actionPositive?: string;
    actionNegative?: string;
    isOperationInProgress?: boolean;
    selectedItems?: string[];
    connectorTypes?: string[];
}

export const DeleteConfirmationDialog: React.FC<DeleteConfirmationDialogProps> = ({
    isOpen,
    onOpenChange,
    onConfirmDelete,
    onCancelDelete,
    isOperationInProgress = false,
    title,
    message,
    actionPositive,
    actionNegative,
    itemType,
    actionVerb,
    selectedItems = [],
    connectorTypes = [],
}) => {
    const intl = useIntl();
    const styles = useDataKnowledgeSpaceStyles();
    const dialogStyles = useDeleteConfirmationDialogStyles();

    const itemCount = selectedItems.length;
    const isMultiple = itemCount > 1;

    const dialogTitle =
        title ||
        (isMultiple
            ? intl.formatMessage(DeleteConfirmationDialogResources.titleMultiple, {
                  count: itemCount,
                  itemType: itemType,
                  actionVerb: actionVerb,
              })
            : intl.formatMessage(DeleteConfirmationDialogResources.titleSingle, {
                  itemType: itemType,
                  actionVerb: actionVerb,
              }));

    const confirmationMessage =
        message ||
        (isMultiple
            ? intl.formatMessage(DeleteConfirmationDialogResources.messageMultiple, {
                  count: itemCount,
                  itemType: itemType,
                  actionVerb: actionVerb.toLowerCase(),
              })
            : intl.formatMessage(DeleteConfirmationDialogResources.messageSingle, {
                  itemType: itemType,
                  actionVerb: actionVerb.toLowerCase(),
              }));

    const renderItemIcon = (index: number) => {
        if (itemType === 'data connector') {
            const connectorType = connectorTypes[index];
            const connectorOption = connectorType ? getConnectorTypeOptions(intl).find(option => option.id === connectorType) : null;

            return connectorOption?.img ? (
                <img src={connectorOption.img} className={dialogStyles.itemIcon} alt={connectorOption.name} />
            ) : (
                <DocumentText16Regular />
            );
        }
        return <DocumentText16Regular />;
    };

    const renderItems = () => {
        if (selectedItems.length === 0) return null;

        return (
            <div className={dialogStyles.itemsContainer}>
                {selectedItems.map((itemName, index) => (
                    <div key={itemName} className={dialogStyles.itemRow}>
                        {renderItemIcon(index)}
                        <Text>{itemName}</Text>
                    </div>
                ))}
            </div>
        );
    };

    return (
        <Dialog open={isOpen} onOpenChange={(_, data) => onOpenChange(data.open)}>
            <DialogSurface>
                <DialogBody>
                    <DialogTitle className={dialogStyles.dialogTitle}>
                        <span>{dialogTitle}</span>
                        <Button
                            appearance="transparent"
                            icon={<Dismiss20Regular />}
                            onClick={onCancelDelete}
                            size="small"
                            className={dialogStyles.dismissButton}
                        />
                    </DialogTitle>
                    <DialogContent>
                        <Text>{confirmationMessage}</Text>
                        {renderItems()}
                    </DialogContent>
                    <DialogActions>
                        <Button
                            appearance="primary"
                            onClick={onConfirmDelete}
                            disabled={isOperationInProgress}
                            className={styles.dangerButton}
                        >
                            {actionPositive || actionVerb}
                        </Button>
                        <Button appearance="secondary" onClick={onCancelDelete}>
                            {actionNegative || intl.formatMessage(DeleteConfirmationDialogResources.cancel)}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};

export default DeleteConfirmationDialog;
