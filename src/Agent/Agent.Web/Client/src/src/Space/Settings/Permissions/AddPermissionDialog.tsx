import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    Dropdown,
    Field,
    Input,
    Option,
} from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { FC, useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { Permission } from '../../../Common/Contracts/Azure/SreAgent';
import { AgentPermissionsResources } from '../../../Strings/SREAgentResources';
import { CrossTenantRoles } from './Permissions';
import { useAddPermissionDialogStyles } from './Permissions.styles';

interface AddPermissionDialogProps {
    isOpen: boolean;
    onOpenChange: (open: boolean) => void;
    onSave: (permission: Permission) => void;
}

export const AddPermissionDialog: FC<AddPermissionDialogProps> = ({ isOpen, onOpenChange, onSave }) => {
    const intl = useIntl();
    const styles = useAddPermissionDialogStyles();

    const [displayName, setDisplayName] = useState('');
    const [role, setRole] = useState('');
    const [objectId, setObjectId] = useState('');
    const [tenantId, setTenantId] = useState('');

    const resetForm = useCallback(() => {
        setDisplayName('');
        setRole('');
        setObjectId('');
        setTenantId('');
    }, []);

    const handleSave = useCallback(() => {
        const permission: Permission = {
            displayName,
            role,
            objectId,
            tenantId,
        };
        onSave(permission);
        resetForm();
        onOpenChange(false);
    }, [displayName, role, objectId, tenantId, onSave, resetForm, onOpenChange]);

    const handleCancel = useCallback(() => {
        resetForm();
        onOpenChange(false);
    }, [resetForm, onOpenChange]);

    const isFormValid = useMemo(
        () => displayName.trim() !== '' && role.trim() !== '' && objectId.trim() !== '' && tenantId.trim() !== '',
        [displayName, role, objectId, tenantId]
    );

    const roleOptions = useMemo(
        () => [
            { key: CrossTenantRoles.StandardUser, display: intl.formatMessage(AgentPermissionsResources.roleStandardUser) },
            { key: CrossTenantRoles.Reader, display: intl.formatMessage(AgentPermissionsResources.roleReader) },
            { key: CrossTenantRoles.Author, display: intl.formatMessage(AgentPermissionsResources.roleAuthor) },
        ],
        [intl]
    );

    const selectedRoleDisplay = useMemo(() => {
        const selectedRole = roleOptions.find(option => option.key === role);
        return selectedRole?.display ?? '';
    }, [roleOptions, role]);

    return (
        <Dialog open={isOpen} onOpenChange={(_, data) => onOpenChange(data.open)}>
            <DialogSurface className={styles.dialogSurface}>
                <DialogBody>
                    <DialogTitle
                        action={
                            <Button
                                appearance="transparent"
                                icon={<Dismiss24Regular />}
                                onClick={handleCancel}
                                aria-label={intl.formatMessage(AgentPermissionsResources.cancel)}
                            />
                        }
                    >
                        {intl.formatMessage(AgentPermissionsResources.addPermission)}
                    </DialogTitle>
                    <DialogContent className={styles.dialogContent}>
                        <Field label={intl.formatMessage(AgentPermissionsResources.displayName)} required>
                            <Input
                                value={displayName}
                                onChange={(_, data) => setDisplayName(data.value)}
                                placeholder={intl.formatMessage(AgentPermissionsResources.displayNamePlaceholder)}
                            />
                        </Field>
                        <Field label={intl.formatMessage(AgentPermissionsResources.role)} required>
                            <Dropdown
                                placeholder={intl.formatMessage(AgentPermissionsResources.rolePlaceholder)}
                                value={selectedRoleDisplay}
                                selectedOptions={role ? [role] : []}
                                onOptionSelect={(_, data) => setRole(data.optionValue ?? '')}
                            >
                                {roleOptions.map(option => (
                                    <Option value={option.key} key={option.key}>
                                        {option.display}
                                    </Option>
                                ))}
                            </Dropdown>
                        </Field>
                        <Field label={intl.formatMessage(AgentPermissionsResources.objectId)} required>
                            <Input
                                value={objectId}
                                onChange={(_, data) => setObjectId(data.value)}
                                placeholder={intl.formatMessage(AgentPermissionsResources.objectIdPlaceholder)}
                            />
                        </Field>
                        <Field label={intl.formatMessage(AgentPermissionsResources.tenantId)} required>
                            <Input
                                value={tenantId}
                                onChange={(_, data) => setTenantId(data.value)}
                                placeholder={intl.formatMessage(AgentPermissionsResources.tenantIdPlaceholder)}
                            />
                        </Field>
                    </DialogContent>
                    <DialogActions className={styles.dialogActions}>
                        <Button appearance="primary" onClick={handleSave} disabled={!isFormValid}>
                            {intl.formatMessage(AgentPermissionsResources.save)}
                        </Button>
                        <Button appearance="secondary" onClick={handleCancel}>
                            {intl.formatMessage(AgentPermissionsResources.cancel)}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
