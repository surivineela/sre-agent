import { Button, Divider, Image, Persona, Popover, PopoverSurface, PopoverTrigger, Text, tokens } from '@fluentui/react-components';
import { useIntl } from 'react-intl';
import { useAuth } from '../Common/Contexts/AuthContext';
import { PortalResources } from '../Strings/Resources';

export const Navbar = () => {
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
        <div
            style={{
                display: 'flex',
                flexDirection: 'row',
                justifyContent: 'space-between',
                gap: 20,
                height: 44,
                paddingLeft: tokens.spacingHorizontalXL,
                paddingRight: tokens.spacingHorizontalXL,
                alignItems: 'center',
                backgroundColor: tokens.colorNeutralBackground4,
            }}
        >
            <div style={{ display: 'flex', flexDirection: 'row', alignContent: 'center', gap: 8 }}>
                <Image src="./SreAgent.svg" width={18} height={18} alt={intl.formatMessage(PortalResources.azureSreAgents)} />
                <Text weight="semibold">{intl.formatMessage(PortalResources.azureSreAgents)}</Text>
            </div>

            <div>
                <Popover>
                    <PopoverTrigger>
                        <Persona avatar={{ image: { src: '' } }} name={personaName} secondaryText={personaSecondaryText} />
                    </PopoverTrigger>

                    <PopoverSurface>
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
                    </PopoverSurface>
                </Popover>
            </div>
        </div>
    );
};
