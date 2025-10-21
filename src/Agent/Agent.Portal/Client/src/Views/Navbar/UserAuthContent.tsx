import { Button, Divider, Persona } from '@fluentui/react-components';
import { useIntl } from 'react-intl';
import { useAuth } from '../../Common/Contexts/AuthContext';
import { PortalResources } from '../../Strings/Resources';

export const UserAuthContent = () => {
    const intl = useIntl();
    const { signIn, signOut, status, user } = useAuth();

    const isAuthenticated = status === 'authenticated';
    const isPending = status === 'pending';
    const personaName = user?.name;
    const personaSecondaryText = user?.username;

    const handleSignInDifferentAccount = () => {
        void signIn({ prompt: 'select_account' });
    };

    return (
        <div>
            <Persona avatar={{ image: { src: '' } }} name={personaName} secondaryText={personaSecondaryText} />
            <div>Directory dropdown</div>

            <Divider />

            <div>
                {isAuthenticated ? (
                    <>
                        <Button disabled={isPending} onClick={handleSignInDifferentAccount}>
                            {intl.formatMessage(PortalResources.signInWithDifferentAccount)}
                        </Button>
                        <Button disabled={isPending} onClick={() => void signOut()}>
                            {intl.formatMessage(PortalResources.signOut)}
                        </Button>
                    </>
                ) : (
                    <Button appearance="primary" disabled={isPending} onClick={() => void signIn()}>
                        {intl.formatMessage(PortalResources.signIn)}
                    </Button>
                )}
            </div>
        </div>
    );
};
