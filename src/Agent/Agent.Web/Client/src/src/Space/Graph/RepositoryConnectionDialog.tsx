import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    Field,
    Link,
    Textarea,
} from '@fluentui/react-components';
import { useCallback, useContext, useEffect, useRef, useState } from 'react';
import { FaGithub } from 'react-icons/fa';
import { VscAzureDevops } from 'react-icons/vsc';
import { FormattedMessage, useIntl } from 'react-intl';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { GenericErrorResources, ResourceInfoResources, SreAgentResources } from '../../Strings/SREAgentResources';

export const githubRepoRegex = /^https:\/\/(?:github\.com|github\.[\w.-]+\.[\w.-]+)\/[\w.-]+\/[\w.-]+(?:\.git)?$/;
export const azdoRepoRegex =
    /^https:\/\/(?:[\w.-]+@)?(?:dev\.azure\.com\/[\w-]+\/(?:[\w.-]|%[0-9A-Fa-f]{2})+\/_git\/(?:[\w.-]|%[0-9A-Fa-f]{2})+|[\w-]+\.visualstudio\.com\/(?:[\w.-]|%[0-9A-Fa-f]{2})+\/_git\/(?:[\w.-]|%[0-9A-Fa-f]{2})+)$/;

export const getRepoIcon = (url: string) => {
    if (githubRepoRegex.test(url)) {
        return <FaGithub />;
    } else if (azdoRepoRegex.test(url)) {
        return <VscAzureDevops />;
    }
    return null;
};

interface RepositoryConnectionDialogProps {
    isOpen: boolean;
    onOpenChange: (open: boolean) => void;
    resourceId?: string;
    onSuccess?: () => void;
    triggerElement?: React.ReactNode;
}

export const RepositoryConnectionDialog = ({
    isOpen,
    onOpenChange,
    resourceId,
    onSuccess,
    triggerElement,
}: RepositoryConnectionDialogProps) => {
    const { logAmplitudeOperationEvent } = useAzPortalContext();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const intl = useIntl();

    const [repoUrl, setRepoUrl] = useState('');
    const [isLinking, setIsLinking] = useState(false);
    const [repoUrlError, setRepoUrlError] = useState('');

    const handleLinkRepository = async () => {
        if (!resourceId || !repoUrl) return;

        setIsLinking(true);
        logAmplitudeOperationEvent({
            targetType: 'update',
            targetAction: 'start',
            targetName: 'connectRepository',
            targetFriendlyName: 'Connect repository',
        });

        try {
            let response;

            if (githubRepoRegex.test(repoUrl)) {
                response = await fetch(`${sreAgentEndpoint}/api/v1/github/link`, {
                    method: 'POST',
                    headers: {
                        ...getAgentHeaders(),
                        'Content-Type': 'application/json',
                    },
                    body: JSON.stringify({
                        resourceId: resourceId,
                        repoUrl: repoUrl,
                        SubType: '',
                        Namespace: '',
                        ResourceName: '',
                    }),
                });
            } else if (azdoRepoRegex.test(repoUrl)) {
                response = await fetch(`${sreAgentEndpoint}/api/v1/azuredevops/link`, {
                    method: 'POST',
                    headers: {
                        ...getAgentHeaders(),
                        'Content-Type': 'application/json',
                    },
                    body: JSON.stringify({
                        resourceId: resourceId,
                        repoUrl: repoUrl,
                        SubType: '',
                        Namespace: '',
                        ResourceName: '',
                    }),
                });
            } else {
                setRepoUrlError(intl.formatMessage(ResourceInfoResources.repositoryUrlErrorMessage));
                setIsLinking(false);
                return;
            }

            const isSuccessful = response?.ok;

            logAmplitudeOperationEvent({
                targetType: 'update',
                targetAction: isSuccessful ? 'success' : 'failed',
                targetName: 'connectRepository',
                targetFriendlyName: 'Connect repository',
            });

            if (!isSuccessful) throw new Error(intl.formatMessage(GenericErrorResources.failedToLinkRepository));

            // Reset form and close dialog
            setRepoUrl('');
            setRepoUrlError('');
            onOpenChange(false);

            // Call success callback
            if (onSuccess) {
                onSuccess();
            } else {
                // Default behavior: reload page
                window.location.reload();
            }
        } catch (err) {
            console.error(intl.formatMessage(GenericErrorResources.failedToLinkRepository), err);
        } finally {
            setIsLinking(false);
        }
    };

    const validateRepoUrl = (url: string) => {
        if (!azdoRepoRegex.test(url) && !githubRepoRegex.test(url)) {
            setRepoUrlError(intl.formatMessage(ResourceInfoResources.repositoryUrlErrorMessage));
        } else {
            setRepoUrlError('');
        }
    };

    const dialogContent = (
        <DialogSurface>
            <DialogBody>
                <DialogTitle>
                    <FormattedMessage {...ResourceInfoResources.linkRepositoryToResource} />
                </DialogTitle>
                <DialogContent>
                    <Field
                        label={intl.formatMessage(ResourceInfoResources.repositoryUrl)}
                        validationState={repoUrlError ? 'error' : undefined}
                        validationMessage={repoUrlError}
                    >
                        <Textarea
                            placeholder={intl.formatMessage(ResourceInfoResources.repositoryLongUrlPlaceholder)}
                            value={repoUrl}
                            onChange={(_, data) => {
                                setRepoUrl(data.value);
                                validateRepoUrl(data.value);
                            }}
                            style={{ direction: 'ltr' }}
                        />
                    </Field>
                </DialogContent>
                <DialogActions>
                    <Button appearance="primary" disabled={!repoUrl || !!repoUrlError || isLinking} onClick={handleLinkRepository}>
                        {isLinking ? (
                            <FormattedMessage {...ResourceInfoResources.connecting} />
                        ) : (
                            <FormattedMessage {...ResourceInfoResources.connectRepository} />
                        )}
                    </Button>
                    <Button appearance="secondary" onClick={() => onOpenChange(false)}>
                        <FormattedMessage {...SreAgentResources.cancel} />
                    </Button>
                </DialogActions>
            </DialogBody>
        </DialogSurface>
    );

    if (triggerElement) {
        return (
            <Dialog open={isOpen} onOpenChange={(_, data) => onOpenChange(data.open)}>
                {dialogContent}
            </Dialog>
        );
    }

    return (
        <Dialog open={isOpen} onOpenChange={(_, data) => onOpenChange(data.open)}>
            {dialogContent}
        </Dialog>
    );
};

interface ConnectRepositoryLinkProps {
    resourceId?: string;
    onSuccess?: () => void;
    className?: string;
}

export const ConnectRepositoryLink = ({ resourceId, onSuccess, className }: ConnectRepositoryLinkProps) => {
    const { logAmplitudeControlEvent } = useAzPortalContext();
    const [isDialogOpen, setIsDialogOpen] = useState(false);
    const triggerLinkRef = useRef<HTMLAnchorElement | null>(null);
    const previousDialogState = useRef(false);

    const onClickConnectRepository = useCallback(() => {
        setIsDialogOpen(true);

        logAmplitudeControlEvent({
            targetType: 'button',
            targetAction: 'clicked',
            targetName: 'connectRepository',
            targetFriendlyName: 'Connect repository',
            valueObjectName: resourceId || '',
            valueObjectFriendlyName: resourceId || '',
        });
    }, [logAmplitudeControlEvent, resourceId]);

    useEffect(() => {
        if (previousDialogState.current && !isDialogOpen) {
            triggerLinkRef.current?.focus();
        }

        previousDialogState.current = isDialogOpen;
    }, [isDialogOpen]);

    return (
        <>
            <Link className={className} onClick={onClickConnectRepository} ref={triggerLinkRef}>
                <FormattedMessage {...ResourceInfoResources.connectRepository} />
            </Link>
            <RepositoryConnectionDialog
                isOpen={isDialogOpen}
                onOpenChange={setIsDialogOpen}
                resourceId={resourceId}
                onSuccess={onSuccess}
            />
        </>
    );
};
