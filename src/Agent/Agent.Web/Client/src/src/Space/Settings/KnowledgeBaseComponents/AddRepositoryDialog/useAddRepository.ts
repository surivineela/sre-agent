import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ExtendedAgentClient } from '../../../../Common/Clients/ExtendedAgentClient';
import { generateKnowledgeName, KnowledgeApiClient } from '../../../../Common/Clients/KnowledgeApiClient';
import { useOAuthPopup } from '../../../../Common/Hooks/useOAuthPopup';
import { ResourceInfoResources, SreAgentResources } from '../../../../Strings/SREAgentResources';

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
    errorMessage: string | null;
    handleSignInToGitHub: () => Promise<void>;
    handleSignInWithDifferentAccount: () => Promise<void>;
    handleAddRepository: () => Promise<boolean>;
    resetForm: () => void;
    isFormValid: boolean;
}

export const useAddRepository = (_agentName: string | undefined, _agentLocation: string | undefined): UseAddRepositoryResult => {
    const intl = useIntl();
    const { log } = useContext(AzPortalContext);
    const { resourceId, sreAgentEndpoint } = useContext(EnvironmentContext);

    const [formState, setFormState] = useState<AddRepositoryFormState>({
        repositoryUrl: '',
        displayName: '',
        description: '',
    });

    const [validationErrors, setValidationErrors] = useState<AddRepositoryValidationErrors>({});
    const [isAdding, setIsAdding] = useState(false);
    const [isAuthenticated, setIsAuthenticated] = useState(false);
    const [isLoadingConfig, setIsLoadingConfig] = useState(false);
    const [oauthUrl, setOauthUrl] = useState<string | null>(null);
    const [errorMessage, setErrorMessage] = useState<string | null>(null);

    const client = ExtendedAgentClient.getInstance(sreAgentEndpoint);

    const loadGitHubConfig = useCallback(async () => {
        setIsLoadingConfig(true);
        setErrorMessage(null);

        const response = await client.getGitHubOAuthConfig();
        if (!response.isSuccessful) {
            setErrorMessage(response.error || 'Failed to load GitHub configuration');
            log({
                action: 'loadGitHubConfig',
                actionModifier: 'failed',
                resourceId,
                logLevel: 'error',
                data: { error: response.error },
            });
        } else {
            setOauthUrl(response.content?.oAuthUrl || null);
        }
        setIsLoadingConfig(false);
    }, [client, log, resourceId]);

    const checkAuthStatus = useCallback(async () => {
        const response = await client.getConnectorStatus('github');
        if (response.isSuccessful) {
            setIsAuthenticated(response.content?.healthy || false);
        }
    }, [client]);

    useEffect(() => {
        loadGitHubConfig();
        checkAuthStatus();
    }, [loadGitHubConfig, checkAuthStatus]);

    const handleOAuthSuccess = useCallback(() => {
        setIsAuthenticated(true);
        log({
            action: 'signInToGitHub',
            actionModifier: 'success',
            resourceId,
            logLevel: 'info',
        });
    }, [log, resourceId]);

    const handleOAuthError = useCallback(
        (error: string) => {
            setErrorMessage(error);
            log({
                action: 'signInToGitHub',
                actionModifier: 'failed',
                resourceId,
                logLevel: 'error',
                data: { error },
            });
        },
        [log, resourceId]
    );

    const { openPopup, isAuthenticating } = useOAuthPopup({
        authUrl: oauthUrl || '',
        popupName: 'GitHubOAuth',
        messageType: 'github-oauth-complete',
        onSuccess: handleOAuthSuccess,
        onError: handleOAuthError,
        checkAuthStatus,
    });

    const isSigningIn = isLoadingConfig || isAuthenticating;
    const isGitHubSignedIn = isAuthenticated;
    const gitHubAccount: string | null = isAuthenticated ? 'Connected' : null;

    const repositoryType = useMemo((): RepositoryType => {
        const url = formState.repositoryUrl.trim();
        if (!url) return null;
        if (githubRepoRegex.test(url)) return 'github';
        if (azdoRepoRegex.test(url)) return 'azuredevops';
        return null;
    }, [formState.repositoryUrl]);

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
        log({
            action: 'signInToGitHub',
            actionModifier: 'start',
            resourceId,
            logLevel: 'info',
        });
        openPopup();
    }, [log, openPopup, resourceId]);

    const handleSignInWithDifferentAccount = useCallback(async () => {
        setIsAuthenticated(false);
        openPopup();
    }, [openPopup]);

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

        const knowledgeClient = KnowledgeApiClient.getInstance(sreAgentEndpoint);
        const response = await knowledgeClient.createRepositoryKnowledge({
            name: generateKnowledgeName(formState.displayName),
            displayName: formState.displayName,
            description: formState.description || undefined,
            url: formState.repositoryUrl,
            branch: 'main', // TODO: See if we want to allow branch selection or auto-detect the default (or backend will just handle)
        });

        if (!response.isSuccessful) {
            log({
                action: 'addRepository',
                actionModifier: 'failed',
                resourceId,
                logLevel: 'error',
                data: { error: response.error },
            });
            setIsAdding(false);
            return false;
        }

        log({
            action: 'addRepository',
            actionModifier: 'success',
            resourceId,
            logLevel: 'info',
        });

        setIsAdding(false);
        return true;
    }, [formState, intl, isGitHubSignedIn, log, repositoryType, resourceId, sreAgentEndpoint, validateDisplayName, validateRepositoryUrl]);

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
        errorMessage,
        handleSignInToGitHub,
        handleSignInWithDifferentAccount,
        handleAddRepository,
        resetForm,
        isFormValid,
    };
};
