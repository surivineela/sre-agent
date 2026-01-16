import { Button, Input, makeStyles, Text, tokens } from '@fluentui/react-components';
import { Delete16Regular } from '@fluentui/react-icons';
import { useCallback } from 'react';
import { useIntl } from 'react-intl';
import { PortalResources, RolesAndPermissions } from '../../../Strings/Resources';
import { AllowedActionRow } from '../../Contracts/AgentSpace';
import { newShortGuid } from '../../Utilities/Guid';

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: '8px',
    },
    headerRow: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr 40px',
        gap: '8px',
        paddingBottom: '4px',
    },
    headerCell: {
        fontWeight: 600,
        color: tokens.colorNeutralForeground1,
    },
    row: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr 40px',
        gap: '8px',
        alignItems: 'center',
    },
    input: {
        width: '100%',
    },
    deleteButton: {
        minWidth: '32px',
        padding: '4px',
    },
});

interface AllowedActionsTableProps {
    rows: AllowedActionRow[];
    onChange: (rows: AllowedActionRow[]) => void;
    disabled?: boolean;
}

export const AllowedActionsTable = ({ rows, onChange, disabled = false }: AllowedActionsTableProps) => {
    const intl = useIntl();
    const styles = useStyles();

    const handleFieldChange = useCallback(
        (rowId: string, field: keyof AllowedActionRow, value: string) => {
            const updatedRows = rows.map(row => (row.id === rowId ? { ...row, [field]: value } : row));

            // Auto-add a new row if the user is typing in the last row
            const lastRow = updatedRows[updatedRows.length - 1];
            if (lastRow && (lastRow.actionName || lastRow.extension)) {
                const hasEmptyRow = updatedRows.some(r => !r.actionName && !r.extension);
                if (!hasEmptyRow) {
                    updatedRows.push({
                        id: newShortGuid(),
                        actionName: '',
                        extension: '',
                        approvalRequired: false,
                    });
                }
            }

            onChange(updatedRows);
        },
        [rows, onChange]
    );

    const handleDelete = useCallback(
        (rowId: string) => {
            // Don't allow deleting the last row
            if (rows.length <= 1) {
                return;
            }
            const updatedRows = rows.filter(row => row.id !== rowId);
            onChange(updatedRows);
        },
        [rows, onChange]
    );

    return (
        <div className={styles.container}>
            <div className={styles.headerRow}>
                <Text className={styles.headerCell}>{intl.formatMessage(PortalResources.actionName)}</Text>
                <Text className={styles.headerCell}>{intl.formatMessage(RolesAndPermissions.extensionName)}</Text>
                <div /> {/* Spacer for delete button column */}
            </div>

            {/* Rows */}
            {rows.map(row => (
                <div key={row.id} className={styles.row}>
                    <Input
                        className={styles.input}
                        value={row.actionName}
                        onChange={(_, data) => handleFieldChange(row.id, 'actionName', data.value)}
                        placeholder={intl.formatMessage(PortalResources.actionNamePlaceholder)}
                        disabled={disabled}
                    />
                    <Input
                        className={styles.input}
                        value={row.extension}
                        onChange={(_, data) => handleFieldChange(row.id, 'extension', data.value)}
                        placeholder={intl.formatMessage(PortalResources.extensionPlaceholder)}
                        disabled={disabled}
                    />
                    <Button
                        className={styles.deleteButton}
                        icon={<Delete16Regular />}
                        appearance="subtle"
                        onClick={() => handleDelete(row.id)}
                        disabled={disabled || rows.length <= 1}
                        aria-label={intl.formatMessage(PortalResources.delete)}
                    />
                </div>
            ))}
        </div>
    );
};
