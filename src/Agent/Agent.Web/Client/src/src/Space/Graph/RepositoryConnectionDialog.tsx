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
import { useContext, useState } from 'react';
import { FaGithub } from 'react-icons/fa';
import { VscAzureDevops } from 'react-icons/vsc';
import { FormattedMessage, useIntl } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { ResourceInfoResources, SreAgentResources } from '../../Strings/SREAgentResources';

export const githubRepoRegex = /^https:\/\/github\.com\/[\w-]+\/[\w.-]+(?:\.git)?$/;
export const azdoRepoRegex =
    /^https:\/\/(?:dev\.azure\.com\/[\w-]+\/[\w-]+\/_git\/[\w.-]+|[\w-]+\.visualstudio\.com\/[\w-]+\/_git\/[\w.-]+)$/;

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
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const intl = useIntl();

    const [repoUrl, setRepoUrl] = useState('');
    const [isLinking, setIsLinking] = useState(false);
    const [repoUrlError, setRepoUrlError] = useState('');

    const handleLinkRepository = async () => {
        if (!resourceId || !repoUrl) return;

        setIsLinking(true);

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

            if (!response?.ok) throw new Error('Failed to link repository');

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
            console.error('Failed to link repository:', err);
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
                            placeholder="https://github.com/owner/repo-name or https://dev.azure.com/organization/project/_git/repo or https://organization.visualstudio.com/project/_git/repository-name"
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
    const [isDialogOpen, setIsDialogOpen] = useState(false);

    return (
        <>
            <Link className={className} onClick={() => setIsDialogOpen(true)}>
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
