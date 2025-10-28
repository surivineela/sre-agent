import { useMsal } from '@azure/msal-react';
import { Button, Combobox, Divider, Field, Option, Persona, Popover, PopoverSurface, PopoverTrigger } from '@fluentui/react-components';
import { useCallback, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { TelemetrySource } from '../../Common/Constants/Telemetry';
import { useAuth } from '../../Common/Contexts/AuthContext';
import { useProfilePhoto } from '../../Common/Hooks/useProfilePhoto';
import { useTenants } from '../../Common/Hooks/useTenants';
import { PortalResources } from '../../Strings/Resources';

export const UserAuthContent = () => {
    const intl = useIntl();
    const { instance } = useMsal();
    const { signIn, signOut, isAuthenticated, user } = useAuth();
    const { photoUrl } = useProfilePhoto(TelemetrySource.PortalLayout);
    const { tenants, isLoading: isLoadingTenants } = useTenants(TelemetrySource.PortalLayout);

    const handleSignInDifferentAccount = useCallback(() => {
        signIn({ prompt: 'select_account' });
    }, [signIn]);

    const currentTenantLabel = useMemo(() => {
        const currentTenant = tenants.find(t => t.tenantId === user?.tenantId);
        return currentTenant?.displayName || currentTenant?.defaultDomain || user?.tenantId || '';
    }, [tenants, user?.tenantId]);

    // TODO: Need more knowledge about how to handle this scenario (but AI Foundry and Portal both fully refresh)
    const handleTenantChange = useCallback((_: any, data: any) => {
        const selectedTenant = tenants.find(t => {
            const label = t.displayName || t.defaultDomain || t.tenantId;
            return label === data.optionText;
        });

        if (selectedTenant && selectedTenant.tenantId !== user?.tenantId) {
            // Switch tenant by signing in with the new tenant
            const account = instance.getAllAccounts().find(acc => acc.tenantId === selectedTenant.tenantId);

            if (account) {
                // Set the active account if it already exists
                instance.setActiveAccount(account);
                // Reload to refresh the session with the new tenant
                window.location.reload();
            } else {
                // Sign in with the new tenant
                signIn({
                    prompt: 'select_account',
                    authority: `https://login.microsoftonline.com/${selectedTenant.tenantId}`
                });
            }
        }
    }, [instance, signIn, tenants, user?.tenantId]);

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
                            <Combobox
                                value={currentTenantLabel}
                                disabled={isLoadingTenants || tenants.length === 0}
                                onOptionSelect={handleTenantChange}
                            >
                                {tenants.map(tenant => {
                                    const label = tenant.displayName || tenant.defaultDomain || tenant.tenantId;
                                    return (
                                        <Option key={tenant.tenantId} text={label}>
                                            {label}
                                        </Option>
                                    );
                                })}
                            </Combobox>
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

