import { Button, Divider, Persona, Popover, PopoverSurface, PopoverTrigger } from '@fluentui/react-components';
import { useIntl } from 'react-intl';
import { TelemetrySource } from '../../Common/Constants/Telemetry';
import { useAuth } from '../../Common/Contexts/AuthContext';
import { useProfilePhoto } from '../../Common/Hooks/useProfilePhoto';
import { PortalResources } from '../../Strings/Resources';

export const UserAuthContent = () => {
    const intl = useIntl();
    const { signIn, signOut, isAuthenticated, user } = useAuth();
    const { photoUrl } = useProfilePhoto(TelemetrySource.PortalLayout);

    const handleSignInDifferentAccount = () => {
        signIn({ prompt: 'select_account' });
    };

    return (
        <Popover>
            <PopoverTrigger>
                <Persona avatar={{ image: { src: photoUrl } }} />
            </PopoverTrigger>

            <PopoverSurface>
                {isAuthenticated ? (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                        <Persona avatar={{ image: { src: photoUrl } }} name={user?.name} secondaryText={user?.username} />

                        <div>Directory dropdown: {user?.tenantId}</div>

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
