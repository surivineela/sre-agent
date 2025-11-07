import {
    Button,
    Divider,
    Dropdown,
    Field,
    Option,
    OptionOnSelectData,
    Persona,
    Popover,
    PopoverSurface,
    PopoverTrigger,
} from '@fluentui/react-components';
import { useCallback } from 'react';
import { useIntl } from 'react-intl';
import { TelemetrySource } from '../../Common/Constants/Telemetry';
import { useAuth } from '../../Common/Contexts/AuthContext';
import { useProfilePhoto } from '../../Common/Hooks/useProfilePhoto';
import { useTenants } from '../../Common/Hooks/useTenants';
import { PortalResources } from '../../Strings/Resources';

export const UserAuthContent = () => {
    const intl = useIntl();
    const { signIn, signOut, switchTenant, isAuthenticated, user } = useAuth();
    const { photoUrl } = useProfilePhoto(TelemetrySource.PortalLayout);
    const { tenants, isLoading: isLoadingTenants } = useTenants(TelemetrySource.PortalLayout);

    const handleSignInDifferentAccount = useCallback(() => {
        signIn();
    }, [signIn]);

    const handleTenantChange = useCallback(
        (_: any, data: OptionOnSelectData) => {
            const selectedTenant = tenants.find(t => t.tenantId === data.optionValue);

            if (selectedTenant && selectedTenant.tenantId !== user?.tenantId) {
                // Switch tenant by signing out and signing back in with the new tenant
                switchTenant(selectedTenant.tenantId);
            }
        },
        [switchTenant, tenants, user?.tenantId]
    );

    return (
        <Popover>
            <PopoverTrigger>
                <Persona avatar={{ image: { src: photoUrl } }} />
            </PopoverTrigger>

            <PopoverSurface>
                {isAuthenticated ? (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                        <Persona avatar={{ image: { src: photoUrl } }} name={user?.name} secondaryText={user?.username} />

                        <Field label={intl.formatMessage(PortalResources.directory)}>
                            <Dropdown
                                value={
                                    tenants.find(t => t.tenantId === user?.tenantId)?.displayName ||
                                    tenants.find(t => t.tenantId === user?.tenantId)?.defaultDomain ||
                                    user?.tenantId ||
                                    ''
                                }
                                selectedOptions={[user?.tenantId || '']}
                                disabled={isLoadingTenants || tenants.length === 0}
                                onOptionSelect={handleTenantChange}
                            >
                                {tenants.map(tenant => {
                                    const label = tenant.displayName || tenant.defaultDomain || tenant.tenantId;
                                    return (
                                        <Option key={tenant.tenantId} value={tenant.tenantId}>
                                            {label}
                                        </Option>
                                    );
                                })}
                            </Dropdown>
                        </Field>

                        <Divider />

                        <div style={{ display: 'flex', flexDirection: 'row', gap: '8px' }}>
                            <Button onClick={handleSignInDifferentAccount}>
                                {intl.formatMessage(PortalResources.signInWithDifferentAccount)}
                            </Button>
                            <Button onClick={() => void signOut()}>{intl.formatMessage(PortalResources.signOut)}</Button>
                        </div>
                    </div>
                ) : (
                    <Button appearance="primary" onClick={() => signIn()}>
                        {intl.formatMessage(PortalResources.signIn)}
                    </Button>
                )}
            </PopoverSurface>
        </Popover>
    );
};
