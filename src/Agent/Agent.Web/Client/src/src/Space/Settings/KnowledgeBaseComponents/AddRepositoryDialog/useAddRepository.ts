import { useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { OAuthPopup } from '../../../../Common/Clients/OAuthPopupClient';
import { OAuthServiceClient } from '../../../../Common/Clients/OAuthService';
import { ArmResourceDescriptor } from '../../../../Common/Helpers/ResourceDescriptors';
import { ResourceInfoResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { useApiConnection } from '../../Connectors/Hooks/useApiConnection';
import { useConsentLink } from '../../Connectors/Hooks/useConsentLink';

export const githubRepoRegex = /^https:\/\/(?:github\.com|github\.[\w.-]+\.[\w.-]+)\/[\w.-]+\/[\w.-]+(?:\.git)?$/;
export const azdoRepoRegex =
    /^https:\/\/(?:[\w.-]+@)?(?:dev\.azure\.com\/[\w-]+\/(?:[\w.-]|%[0-9A-Fa-f]{2})+\/_git\/(?:[\w.-]|%[0-9A-Fa-f]{2})+|[\w-]+\.visualstudio\.com\/(?:[\w.-]|%[0-9A-Fa-f]{2})+\/_git\/(?:[\w.-]|%[0-9A-Fa-f]{2})+)$/;

export type RepositoryType = 'github' | 'azuredevops' | null;

export interface AddRepositoryFormState {
    repositoryUrl: string;
    displayName: string;
    description: string;
}

export interface AddRepositoryValidationErrors {
    repositoryUrl?: string;
    displayName?: string;
    gitHubAccount?: string;
}

export interface UseAddRepositoryResult {
    formState: AddRepositoryFormState;
    setRepositoryUrl: (url: string) => void;
    setDisplayName: (name: string) => void;
    setDescription: (description: string) => void;
    repositoryType: RepositoryType;
    validationErrors: AddRepositoryValidationErrors;
    isGitHubSignedIn: boolean;
    gitHubAccount: string | null;
    isSigningIn: boolean;
    isAdding: boolean;
    handleSignInToGitHub: () => Promise<void>;
    handleSignInWithDifferentAccount: () => Promise<void>;
    handleAddRepository: () => Promise<boolean>;
    resetForm: () => void;
    isFormValid: boolean;
}

export const useAddRepository = (agentName: string | undefined, agentLocation: string | undefined): UseAddRepositoryResult => {
    const intl = useIntl();
    const { log } = useContext(AzPortalContext);
    const { resourceId, userInfo } = useContext(EnvironmentContext);
    const { subscription, resourceGroup } = new ArmResourceDescriptor(resourceId);

    const [formState, setFormState] = useState<AddRepositoryFormState>({
        repositoryUrl: '',
        displayName: '',
        description: '',
    });

    const [validationErrors, setValidationErrors] = useState<AddRepositoryValidationErrors>({});
    const [isSigningIn, setIsSigningIn] = useState(false);
    const [isAdding, setIsAdding] = useState(false);
    const [gitHubAccount, setGitHubAccount] = useState<string | null>(null);

    const connectionName = 'github';
    const { apiConnection, fetchApiConnection, createApiConnection, deleteApiConnection } = useApiConnection();
    const { consentLink, fetchConsentLink, refreshConsentLink } = useConsentLink(`${agentName}-${connectionName}`);

    const repositoryType = useMemo((): RepositoryType => {
        const url = formState.repositoryUrl.trim();
        if (!url) return null;
        if (githubRepoRegex.test(url)) return 'github';
        if (azdoRepoRegex.test(url)) return 'azuredevops';
        return null;
    }, [formState.repositoryUrl]);

    const isGitHubSignedIn = useMemo(() => {
        return !!apiConnection && !!consentLink && consentLink.status !== 'Unauthenticated' && !!gitHubAccount;
    }, [apiConnection, consentLink, gitHubAccount]);

    const validateRepositoryUrl = useCallback(
        (url: string): string | undefined => {
            if (!url.trim()) {
                return intl.formatMessage(SreAgentResources.fieldRequired);
            }
            if (!githubRepoRegex.test(url) && !azdoRepoRegex.test(url)) {
                return intl.formatMessage(ResourceInfoResources.repositoryUrlErrorMessage);
            }
            return undefined;
        },
        [intl]
    );

    const validateDisplayName = useCallback(
        (name: string): string | undefined => {
            if (!name.trim()) {
                return intl.formatMessage(SreAgentResources.fieldRequired);
            }
            return undefined;
        },
        [intl]
    );

    const setRepositoryUrl = useCallback(
        (url: string) => {
            setFormState(prev => ({ ...prev, repositoryUrl: url }));
            const error = validateRepositoryUrl(url);
            setValidationErrors(prev => ({ ...prev, repositoryUrl: error }));
        },
        [validateRepositoryUrl]
    );

    const setDisplayName = useCallback(
        (name: string) => {
            setFormState(prev => ({ ...prev, displayName: name }));
            const error = validateDisplayName(name);
            setValidationErrors(prev => ({ ...prev, displayName: error }));
        },
        [validateDisplayName]
    );

    const setDescription = useCallback((description: string) => {
        setFormState(prev => ({ ...prev, description }));
    }, []);

    const handleSignInToGitHub = useCallback(async () => {
        setIsSigningIn(true);
        log({
            action: 'signInToGitHub',
            actionModifier: 'start',
            resourceId,
            logLevel: 'info',
        });

        await createApiConnection({
            subscriptionId: subscription,
            resourceGroup,
            connectionName,
            location: agentLocation || '',
            agentName: agentName || '',
        });

        const consentLinkObject = await fetchConsentLink();
        if (consentLinkObject?.link) {
            const oauthPopupClient = new OAuthPopup({ consentUrl: consentLinkObject.link });

            const loginResponse = await oauthPopupClient.loginPromise;
            if (loginResponse.error) {
                log({
                    action: 'signInToGitHub',
                    actionModifier: 'failed',
                    resourceId,
                    logLevel: 'error',
                    data: { error: loginResponse.error },
                });
                setIsSigningIn(false);
                return;
            }

            if (loginResponse.code) {
                await OAuthServiceClient.confirmConsentCodeForConnection({
                    subscriptionId: subscription,
                    resourceGroup,
                    connectionName,
                    code: loginResponse.code,
                    tenantId: userInfo?.directoryId || '',
                    objectId: userInfo?.objectId || '',
                });
            }

            const fetchedConnection = await fetchApiConnection({
                subscriptionId: subscription,
                resourceGroup,
                agentName: agentName || '',
                connectionName,
            });

            await refreshConsentLink();

            const accountName = fetchedConnection?.properties?.authenticatedUser?.name || '';
            setGitHubAccount(accountName);

            log({
                action: 'signInToGitHub',
                actionModifier: 'success',
                resourceId,
                logLevel: 'info',
                data: { account: accountName },
            });
        }

        setIsSigningIn(false);
    }, [
        agentLocation,
        agentName,
        createApiConnection,
        fetchApiConnection,
        fetchConsentLink,
        log,
        refreshConsentLink,
        resourceGroup,
        resourceId,
        subscription,
        userInfo,
    ]);

    const handleSignInWithDifferentAccount = useCallback(async () => {
        setIsSigningIn(true);

        await deleteApiConnection({
            subscriptionId: subscription,
            resourceGroup,
            agentName: agentName || '',
            connectionName,
        });

        setGitHubAccount(null);
        await handleSignInToGitHub();
    }, [agentName, deleteApiConnection, handleSignInToGitHub, resourceGroup, subscription]);

    const handleAddRepository = useCallback(async (): Promise<boolean> => {
        const urlError = validateRepositoryUrl(formState.repositoryUrl);
        const nameError = validateDisplayName(formState.displayName);

        if (urlError || nameError) {
            setValidationErrors({
                repositoryUrl: urlError,
                displayName: nameError,
            });
            return false;
        }

        if (repositoryType === 'github' && !isGitHubSignedIn) {
            setValidationErrors(prev => ({
                ...prev,
                gitHubAccount: intl.formatMessage(SreAgentResources.fieldRequired),
            }));
            return false;
        }

        setIsAdding(true);
        log({
            action: 'addRepository',
            actionModifier: 'start',
            resourceId,
            logLevel: 'info',
            data: {
                repositoryUrl: formState.repositoryUrl,
                displayName: formState.displayName,
                repositoryType,
            },
        });

        // TODO: Implement API call to add repository
        // For now, simulate success
        await new Promise(resolve => setTimeout(resolve, 1000));

        log({
            action: 'addRepository',
            actionModifier: 'success',
            resourceId,
            logLevel: 'info',
        });

        setIsAdding(false);
        return true;
    }, [formState, intl, isGitHubSignedIn, log, repositoryType, resourceId, validateDisplayName, validateRepositoryUrl]);

    const resetForm = useCallback(() => {
        setFormState({
            repositoryUrl: '',
            displayName: '',
            description: '',
        });
        setValidationErrors({});
    }, []);

    const isFormValid = useMemo(() => {
        const hasRequiredFields = formState.repositoryUrl.trim() !== '' && formState.displayName.trim() !== '';
        const hasNoErrors = !validationErrors.repositoryUrl && !validationErrors.displayName;
        const hasGitHubAuthIfNeeded = repositoryType !== 'github' || isGitHubSignedIn;

        return hasRequiredFields && hasNoErrors && hasGitHubAuthIfNeeded;
    }, [formState, validationErrors, repositoryType, isGitHubSignedIn]);

    return {
        formState,
        setRepositoryUrl,
        setDisplayName,
        setDescription,
        repositoryType,
        validationErrors,
        isGitHubSignedIn,
        gitHubAccount,
        isSigningIn,
        isAdding,
        handleSignInToGitHub,
        handleSignInWithDifferentAccount,
        handleAddRepository,
        resetForm,
        isFormValid,
    };
};
