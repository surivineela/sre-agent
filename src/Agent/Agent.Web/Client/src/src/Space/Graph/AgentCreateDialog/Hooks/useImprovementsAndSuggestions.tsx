import { useCallback, useContext, useEffect, useRef, useState } from 'react';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ExtendedAgentClient } from '../../../../Common/Clients/ExtendedAgentClient';
import { PromptImprovementResponse } from '../../../Contracts/ExtendedAgentGraph';

export interface UseImprovementsAndSuggestionsReturn {
    improvements: PromptImprovementResponse | undefined;
    loadingImprovements: boolean;
    isImprovementResultStale: boolean;
    suggestions: PromptImprovementResponse | undefined;
    loadingSuggestions: boolean;
    isSuggestionResultStale: boolean;
    getImprovements: () => Promise<void>;
    getSuggestions: () => Promise<void>;
    clear: () => void;
}

export const useImprovementsAndSuggestions = (
    prompt: string,
    onImprovementsReturned: (result: PromptImprovementResponse | undefined) => void
): UseImprovementsAndSuggestionsReturn => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const extendedAgentClient = ExtendedAgentClient.getInstance(sreAgentEndpoint);

    const promptForPendingImprovements = useRef<string>('');
    const lastPromptUsedForImprovements = useRef<string>('');
    const [isImprovementResultStale, setIsImprovementResultStale] = useState<boolean>(true);

    const [improvements, setImprovements] = useState<PromptImprovementResponse>();
    const [loadingImprovements, setLoadingImprovements] = useState<boolean>(false);

    const lastPromptUsedForSuggestions = useRef<string>('');
    const [isSuggestionResultStale, setIsSuggestionResultStale] = useState<boolean>(true);

    const promptForPendingSuggestions = useRef<string>('');
    const [suggestions, setSuggestions] = useState<PromptImprovementResponse>();
    const [loadingSuggestions, setLoadingSuggestions] = useState<boolean>(false);

    const getImprovements = useCallback(async () => {
        if (!isImprovementResultStale) {
            onImprovementsReturned(improvements);
        } else if (prompt) {
            const currentPrompt = prompt;
            promptForPendingImprovements.current = currentPrompt;

            setLoadingImprovements(true);
            setImprovements(undefined);
            const response = await extendedAgentClient.getPromptImprovement(currentPrompt);
            if (promptForPendingImprovements.current === currentPrompt) {
                if (response.isSuccessful) {
                    setImprovements(response.content);
                    lastPromptUsedForImprovements.current = currentPrompt;
                    setIsImprovementResultStale(false);
                    onImprovementsReturned(response.content);
                }
                setLoadingImprovements(false);
            }
        }
    }, [prompt, extendedAgentClient, improvements, isImprovementResultStale, onImprovementsReturned]);

    const getSuggestions = useCallback(async () => {
        if (isSuggestionResultStale && prompt) {
            const currentPrompt = prompt;
            promptForPendingSuggestions.current = currentPrompt;

            setLoadingSuggestions(true);
            setSuggestions(undefined);
            const response = await extendedAgentClient.getPromptImprovement(currentPrompt);
            if (promptForPendingSuggestions.current === currentPrompt) {
                if (response.isSuccessful) {
                    setSuggestions(response.content);
                    lastPromptUsedForSuggestions.current = currentPrompt;
                    setIsSuggestionResultStale(false);
                }
                setLoadingSuggestions(false);
            }
        }
    }, [prompt, extendedAgentClient, suggestions, isSuggestionResultStale]);

    const clear = useCallback(() => {
        promptForPendingImprovements.current = '';
        lastPromptUsedForImprovements.current = '';
        setIsImprovementResultStale(true);
        setImprovements(undefined);
        setLoadingImprovements(false);
        promptForPendingSuggestions.current = '';
        lastPromptUsedForSuggestions.current = '';
        setIsSuggestionResultStale(true);
        setSuggestions(undefined);
        setLoadingSuggestions(false);
    }, []);

    useEffect(() => {
        setIsImprovementResultStale(prompt !== lastPromptUsedForImprovements.current);
        setIsSuggestionResultStale(prompt !== lastPromptUsedForSuggestions.current);
    }, [prompt]);

    return {
        improvements,
        loadingImprovements,
        isImprovementResultStale,
        suggestions,
        loadingSuggestions,
        isSuggestionResultStale,
        getImprovements,
        getSuggestions,
        clear,
    };
};
