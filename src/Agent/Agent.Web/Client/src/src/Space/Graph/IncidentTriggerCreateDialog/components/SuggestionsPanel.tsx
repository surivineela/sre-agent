import { Button, Spinner, Text } from '@fluentui/react-components';
import { ArrowClockwise16Regular } from '@fluentui/react-icons';
import { useFormikContext } from 'formik';
import { FC, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IncidentHandlerClient } from '../../../../Common/Clients/IncidentHandlerClient';
import { IncidentManagementType } from '../../../../Common/Contracts/Azure/SreAgent';
import { ExtendedAgentsGraphResources, IncidentHandlerCreateResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { IncidentHandlerCreateFormValues } from '../../../IncidentManagement/CreateIncidentHandler/IncidentHandlerCreateFormValues';
import { useIncidentTriggerCreateDialogStyles } from '../IncidentTriggerCreateDialog.Styles';
import { FilterSuggestion, FilterSuggestionCard } from './FilterSuggestionCard';

export interface SuggestionsPanelProps {
    incidentPlatformType: IncidentManagementType | undefined;
    suggestionsCache: Map<string, FilterSuggestion[]>;
    setSuggestionsCache: React.Dispatch<React.SetStateAction<Map<string, FilterSuggestion[]>>>;
    teamNamesCache: Map<string, string>;
    setTeamNamesCache: React.Dispatch<React.SetStateAction<Map<string, string>>>;
    loadingSuggestions: boolean;
    setLoadingSuggestions: React.Dispatch<React.SetStateAction<boolean>>;
    appliedSuggestionIndex: number | null;
    setAppliedSuggestionIndex: React.Dispatch<React.SetStateAction<number | null>>;
    currentCacheKey: string | undefined;
    setCurrentCacheKey: React.Dispatch<React.SetStateAction<string | undefined>>;
    suggestionsError: string | undefined;
    setSuggestionsError: React.Dispatch<React.SetStateAction<string | undefined>>;
}

// Helper function to generate cache key
const getCacheKey = (owningTeamId: string | undefined, incidentType: string | undefined): string => {
    return `${owningTeamId || 'none'}|${incidentType || 'none'}`;
};

export const SuggestionsPanel: FC<SuggestionsPanelProps> = ({
    incidentPlatformType,
    suggestionsCache,
    setSuggestionsCache,
    teamNamesCache,
    setTeamNamesCache,
    loadingSuggestions,
    setLoadingSuggestions,
    appliedSuggestionIndex,
    setAppliedSuggestionIndex,
    currentCacheKey,
    setCurrentCacheKey,
    suggestionsError,
    setSuggestionsError,
}) => {
    const intl = useIntl();
    const styles = useIncidentTriggerCreateDialogStyles();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const azPortalContext = useContext(AzPortalContext);
    const { values, setFieldValue } = useFormikContext<IncidentHandlerCreateFormValues>();

    // Track the last owningTeamId we fetched for
    const [lastFetchedOwningTeamId, setLastFetchedOwningTeamId] = useState<string | undefined>(undefined);
    // Track the last cache key that had suggestions to keep displaying them
    const [lastDisplayedCacheKey, setLastDisplayedCacheKey] = useState<string | undefined>(undefined);
    // Track if we're currently applying a suggestion to prevent resetting applied index
    const isApplyingRef = useRef(false);
    // Track the values of the applied suggestion to detect when user changes contradict it
    const [appliedSuggestionValues, setAppliedSuggestionValues] = useState<{
        titleContains: string;
        priority: string;
        incidentType: string;
        filterName: string;
        owningTeamId: string;
    } | null>(null);

    const incidentHandlerClient = useMemo(
        () => IncidentHandlerClient.getInstance(sreAgentEndpoint, azPortalContext.log.bind(azPortalContext)),
        [azPortalContext, sreAgentEndpoint]
    );

    // Get current suggestions from cache based on current form values
    const currentCacheKeyValue = useMemo(() => {
        return getCacheKey(values.owningTeamId, values.incidentType);
    }, [values.owningTeamId, values.incidentType]);

    // Compute suggestions and their actual source cache key together (no side effects in memo)
    const { suggestions: aiSuggestions, sourceCacheKey } = useMemo(() => {
        const currentSuggestions = suggestionsCache.get(currentCacheKeyValue);
        if (currentSuggestions && currentSuggestions.length > 0) {
            // We have current suggestions - use them and mark current as source
            return { suggestions: currentSuggestions, sourceCacheKey: currentCacheKeyValue };
        } else if (currentSuggestions !== undefined && currentSuggestions.length === 0) {
            // Explicitly empty cache entry - show empty with current as source
            return { suggestions: [], sourceCacheKey: currentCacheKeyValue };
        } else if (lastDisplayedCacheKey && suggestionsCache.has(lastDisplayedCacheKey)) {
            // Fall back to last displayed suggestions - source is the last key, not current
            const fallbackSuggestions = suggestionsCache.get(lastDisplayedCacheKey) || [];
            return { suggestions: fallbackSuggestions, sourceCacheKey: lastDisplayedCacheKey };
        }
        return { suggestions: [], sourceCacheKey: undefined };
    }, [suggestionsCache, currentCacheKeyValue, lastDisplayedCacheKey]);

    // Update lastDisplayedCacheKey only when we have a valid source with suggestions
    useEffect(() => {
        if (sourceCacheKey && aiSuggestions.length > 0 && sourceCacheKey !== lastDisplayedCacheKey) {
            setLastDisplayedCacheKey(sourceCacheKey);
        }
    }, [sourceCacheKey, aiSuggestions.length, lastDisplayedCacheKey]);

    // Parse the source cache key (not lastDisplayedCacheKey) to show which config the suggestions are for
    const displayedSuggestionsConfig = useMemo(() => {
        if (!sourceCacheKey) return null;
        const [owningTeamId, incidentType] = sourceCacheKey.split('|');
        const parsedTeamId = owningTeamId === 'none' ? undefined : owningTeamId;
        return {
            owningTeamId: parsedTeamId,
            owningTeamName: parsedTeamId ? teamNamesCache.get(parsedTeamId) : undefined,
            incidentType: incidentType === 'none' ? undefined : incidentType,
        };
    }, [sourceCacheKey, teamNamesCache]);

    // Check if we should show the Generate button
    const hasUncachedIncidentTypeChange = useMemo(() => {
        // Show Generate button if:
        // 1. We have an owningTeamId
        // 2. The current cache key combination doesn't exist in cache
        // 3. We're not currently loading
        return (
            !!values.owningTeamId &&
            !suggestionsCache.has(currentCacheKeyValue) &&
            !loadingSuggestions &&
            lastFetchedOwningTeamId === values.owningTeamId // Only show if we've already fetched for this team
        );
    }, [values.owningTeamId, suggestionsCache, currentCacheKeyValue, loadingSuggestions, lastFetchedOwningTeamId]);

    // Function to fetch suggestions
    const fetchSuggestions = useCallback(
        async (owningTeamId: string, incidentType: string | undefined, cacheKey: string, teamName?: string) => {
            setCurrentCacheKey(cacheKey);
            setLoadingSuggestions(true);
            setSuggestionsError(undefined);
            setAppliedSuggestionIndex(null);

            // Store team name if provided
            if (teamName && owningTeamId) {
                setTeamNamesCache(prev => {
                    const newCache = new Map(prev);
                    newCache.set(owningTeamId, teamName);
                    return newCache;
                });
            }

            azPortalContext.log({
                action: 'fetch-filter-suggestions',
                actionModifier: 'start',
                logLevel: 'info',
                data: {
                    owningTeamId,
                    incidentType,
                    incidentPlatformType,
                    cacheKey,
                },
            });

            try {
                const response = await incidentHandlerClient.getSuggestedFilters(owningTeamId, incidentType);

                if (response.isSuccessful && response.content) {
                    setSuggestionsCache(prevCache => {
                        const newCache = new Map(prevCache);
                        newCache.set(cacheKey, response.content!);
                        return newCache;
                    });
                    setSuggestionsError(undefined);

                    azPortalContext.log({
                        action: 'fetch-filter-suggestions',
                        actionModifier: 'success',
                        logLevel: 'info',
                        data: {
                            owningTeamId,
                            incidentType,
                            suggestionsCount: response.content.length,
                            cacheKey,
                            suggestions: response.content.map(s => ({
                                filterName: s.filterName,
                                incidentCount: s.count,
                            })),
                        },
                    });
                } else {
                    setSuggestionsCache(prevCache => {
                        const newCache = new Map(prevCache);
                        newCache.set(cacheKey, []);
                        return newCache;
                    });
                    setSuggestionsError(
                        response.error?.message || intl.formatMessage(ExtendedAgentsGraphResources.smartFilterLoadingSuggestionsFailure)
                    );

                    azPortalContext.log({
                        action: 'fetch-filter-suggestions',
                        actionModifier: 'failed',
                        logLevel: 'error',
                        data: {
                            owningTeamId,
                            incidentType,
                            cacheKey,
                            error: response.error?.message,
                        },
                    });
                }
            } catch (error) {
                setSuggestionsError(
                    (error as Error)?.message || intl.formatMessage(ExtendedAgentsGraphResources.smartFilterLoadingSuggestionsError)
                );

                azPortalContext.log({
                    action: 'fetch-filter-suggestions',
                    actionModifier: 'exception',
                    logLevel: 'error',
                    data: {
                        owningTeamId,
                        incidentType,
                        cacheKey,
                        error: (error as Error)?.message || error,
                    },
                });
            } finally {
                setLoadingSuggestions(false);
            }
        },
        [
            incidentHandlerClient,
            incidentPlatformType,
            azPortalContext,
            setCurrentCacheKey,
            setLoadingSuggestions,
            setSuggestionsError,
            setAppliedSuggestionIndex,
            setSuggestionsCache,
            setTeamNamesCache,
        ]
    );

    // Auto-fetch only when owningTeamId changes (not incidentType)
    useEffect(() => {
        const shouldFetchSuggestions =
            incidentPlatformType === IncidentManagementType.Icm
                ? !!values.owningTeamId
                : incidentPlatformType && incidentPlatformType !== IncidentManagementType.None;

        // Only auto-fetch if owningTeamId changed (not incidentType)
        const owningTeamIdChanged = lastFetchedOwningTeamId !== values.owningTeamId;

        if (shouldFetchSuggestions && owningTeamIdChanged && values.owningTeamId) {
            const cacheKey = getCacheKey(values.owningTeamId, values.incidentType);
            const hasCachedResults = suggestionsCache.has(cacheKey);

            if (!hasCachedResults) {
                setLastFetchedOwningTeamId(values.owningTeamId);
                fetchSuggestions(values.owningTeamId, values.incidentType, cacheKey, values.owningTeamName);
            } else {
                // Cache hit - just update tracking
                setLastFetchedOwningTeamId(values.owningTeamId);
                setCurrentCacheKey(cacheKey);
                setAppliedSuggestionIndex(null);
                setSuggestionsError(undefined);
            }
        } else if (!shouldFetchSuggestions) {
            // Clear state when conditions are no longer met
            setCurrentCacheKey(undefined);
            setSuggestionsError(undefined);
            setAppliedSuggestionIndex(null);
            setLastFetchedOwningTeamId(undefined);
        }
    }, [
        values.owningTeamId, // Only owningTeamId in dependencies - incidentType changes won't trigger auto-fetch
        incidentPlatformType,
        lastFetchedOwningTeamId,
        suggestionsCache,
        fetchSuggestions,
        setCurrentCacheKey,
        setAppliedSuggestionIndex,
        setSuggestionsError,
    ]);

    // Reset applied index when cache key changes OR when form values contradict the applied suggestion
    // but NOT when we're currently applying a suggestion
    useEffect(() => {
        if (isApplyingRef.current) {
            // Don't reset while applying
            isApplyingRef.current = false;
            return;
        }

        // Check if we have an applied suggestion and if current values still match it
        if (appliedSuggestionIndex !== null && appliedSuggestionValues) {
            const valuesMatch =
                values.titleContains === appliedSuggestionValues.titleContains &&
                values.priority === appliedSuggestionValues.priority &&
                values.incidentType === appliedSuggestionValues.incidentType &&
                values.filterName === appliedSuggestionValues.filterName &&
                values.owningTeamId === appliedSuggestionValues.owningTeamId;

            if (!valuesMatch) {
                // User changed a field that contradicts the applied suggestion
                setAppliedSuggestionIndex(null);
                setAppliedSuggestionValues(null);
            }
        }
    }, [
        values.titleContains,
        values.priority,
        values.incidentType,
        values.filterName,
        values.owningTeamId,
        appliedSuggestionIndex,
        appliedSuggestionValues,
        setAppliedSuggestionIndex,
    ]);

    const handleGenerate = () => {
        const cacheKey = getCacheKey(values.owningTeamId, values.incidentType);

        azPortalContext.log({
            action: 'filter-suggestions-generate',
            actionModifier: 'click',
            logLevel: 'info',
            data: {
                owningTeamId: values.owningTeamId,
                incidentType: values.incidentType,
                cacheKey,
            },
        });

        if (values.owningTeamId) {
            fetchSuggestions(values.owningTeamId, values.incidentType, cacheKey, values.owningTeamName);
        }
    };

    const handleRetry = () => {
        const cacheKey = getCacheKey(values.owningTeamId, values.incidentType);

        azPortalContext.log({
            action: 'filter-suggestions-retry',
            actionModifier: 'click',
            logLevel: 'info',
            data: {
                owningTeamId: values.owningTeamId,
                incidentType: values.incidentType,
                cacheKey,
                previousError: suggestionsError,
            },
        });

        setSuggestionsCache(prevCache => {
            const newCache = new Map(prevCache);
            newCache.delete(cacheKey);
            return newCache;
        });
        setSuggestionsError(undefined);

        if (values.owningTeamId) {
            fetchSuggestions(values.owningTeamId, values.incidentType, cacheKey, values.owningTeamName);
        }
    };

    const handleApplySuggestion = (suggestion: FilterSuggestion, index: number) => {
        // Mark that we're applying a suggestion to prevent cache key change from resetting applied index
        isApplyingRef.current = true;

        azPortalContext.log({
            action: 'filter-suggestions-apply',
            actionModifier: 'click',
            logLevel: 'info',
            data: {
                owningTeamId: values.owningTeamId,
                suggestionIndex: index,
                filterName: suggestion.filterName,
                titleContains: suggestion.titleContains,
                severity: suggestion.severity,
                incidentType: suggestion.incidentType,
                incidentCount: suggestion.count,
                totalSuggestionsAvailable: aiSuggestions.length,
                isFromDifferentSource: sourceCacheKey !== currentCacheKeyValue,
            },
        });

        // If applying a suggestion from a different owning team's cache, update the team to match
        let finalOwningTeamId = values.owningTeamId;
        if (sourceCacheKey && displayedSuggestionsConfig) {
            const sourceTeamId = displayedSuggestionsConfig.owningTeamId;
            const sourceTeamName = displayedSuggestionsConfig.owningTeamName;

            // Update owning team if different from current
            if (sourceTeamId && sourceTeamId !== values.owningTeamId) {
                setFieldValue('owningTeamId', sourceTeamId);
                if (sourceTeamName) {
                    setFieldValue('owningTeamName', sourceTeamName);
                }
                finalOwningTeamId = sourceTeamId;
            }
        }

        // Apply the suggestion filters to the form
        if (suggestion.titleContains) {
            setFieldValue('titleContains', suggestion.titleContains);
        } else {
            setFieldValue('titleContains', '');
        }
        if (suggestion.severity) {
            setFieldValue('priority', suggestion.severity);
        } else {
            setFieldValue('priority', 'ALL');
        }
        if (suggestion.incidentType) {
            setFieldValue('incidentType', suggestion.incidentType);
        }
        if (suggestion.filterName) {
            setFieldValue('filterName', suggestion.filterName);
        }
        setFieldValue('impactedService', 'ALL');
        setAppliedSuggestionIndex(index);

        // Store the applied suggestion values to detect when user changes contradict it
        setAppliedSuggestionValues({
            titleContains: suggestion.titleContains || '',
            priority: suggestion.severity || 'ALL',
            incidentType: suggestion.incidentType || '',
            filterName: suggestion.filterName || '',
            owningTeamId: finalOwningTeamId || '',
        });
    };

    return (
        <div className={styles.suggestionsPanel}>
            <div className={styles.suggestionsPanelHeader}>
                <Text weight="semibold" className={styles.suggestionsPanelTitle}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.smartFilterAISuggestions)}
                </Text>
                {aiSuggestions.length > 0 && !loadingSuggestions && (
                    <Button
                        appearance="subtle"
                        size="small"
                        icon={<ArrowClockwise16Regular />}
                        onClick={handleRetry}
                        title={intl.formatMessage(IncidentHandlerCreateResources.regenerate)}
                        className={styles.retryButton}
                    />
                )}
            </div>
            <Text size={200} className={styles.disclaimerText}>
                {intl.formatMessage(ExtendedAgentsGraphResources.smartFilterDisclaimer)}
            </Text>

            {/* Show Generate button when incidentType changes */}
            {hasUncachedIncidentTypeChange && (
                <div className={styles.generateContainer}>
                    <Text className={styles.generateInfo}>
                        {intl.formatMessage(ExtendedAgentsGraphResources.smartFilterGenerateSuggestion)}
                        <br />
                        <strong>{intl.formatMessage(ExtendedAgentsGraphResources.smartFilterOwningTeam)}:</strong> {values.owningTeamName}
                        <br />
                        <strong>{intl.formatMessage(ExtendedAgentsGraphResources.smartFilterIncidentType)}:</strong>{' '}
                        {values.incidentType || intl.formatMessage(SreAgentResources.all)}
                    </Text>
                    <Button appearance="primary" size="small" onClick={handleGenerate} className={styles.generateButton}>
                        {intl.formatMessage(IncidentHandlerCreateResources.generate)}
                    </Button>
                </div>
            )}

            {loadingSuggestions ? (
                <div className={styles.loadingContainer}>
                    <Spinner size="small" label={intl.formatMessage(ExtendedAgentsGraphResources.smartFilterLoadingSuggestions)} />
                </div>
            ) : suggestionsError ? (
                <div className={styles.errorContainer}>
                    <Text className={styles.errorMessage}>{suggestionsError}</Text>
                    <Button appearance="primary" onClick={handleRetry}>
                        {intl.formatMessage(IncidentHandlerCreateResources.retry)}
                    </Button>
                </div>
            ) : (
                <>
                    {aiSuggestions.length > 0 && displayedSuggestionsConfig && (
                        <div className={styles.suggestionsConfigInfo}>
                            <Text size={200}>{intl.formatMessage(ExtendedAgentsGraphResources.smartFilterShowSuggestion)}</Text>
                            <div className={styles.suggestionsConfigRow}>
                                <Text size={200} weight="semibold">
                                    {intl.formatMessage(ExtendedAgentsGraphResources.smartFilterOwningTeam)}:
                                </Text>
                                <Text size={200}>
                                    {displayedSuggestionsConfig.owningTeamName || displayedSuggestionsConfig.owningTeamId}
                                </Text>
                            </div>
                            <div className={styles.suggestionsConfigRow}>
                                <Text size={200} weight="semibold">
                                    {intl.formatMessage(ExtendedAgentsGraphResources.smartFilterIncidentType)}:
                                </Text>
                                <Text size={200}>
                                    {displayedSuggestionsConfig.incidentType || intl.formatMessage(SreAgentResources.all)}
                                </Text>
                            </div>
                        </div>
                    )}
                    <div className={styles.suggestionsList}>
                        {aiSuggestions.length > 0 ? (
                            aiSuggestions.map((suggestion, index) => (
                                <FilterSuggestionCard
                                    key={index}
                                    suggestion={suggestion}
                                    onApply={() => handleApplySuggestion(suggestion, index)}
                                    isApplied={appliedSuggestionIndex === index}
                                />
                            ))
                        ) : (
                            <Text size={200} className={styles.noSuggestions}>
                                {!currentCacheKey
                                    ? incidentPlatformType === IncidentManagementType.Icm
                                        ? intl.formatMessage(ExtendedAgentsGraphResources.smartFilterOwningTeamCommand)
                                        : intl.formatMessage(ExtendedAgentsGraphResources.smartFilterNoSuggestionsAvailable)
                                    : intl.formatMessage(ExtendedAgentsGraphResources.smartFilterNoSuggestionsFound)}
                            </Text>
                        )}
                    </div>
                </>
            )}
        </div>
    );
};
