import debounce from 'lodash/debounce';
import { useCallback, useContext, useEffect, useRef, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ResourceMappingClient } from '../../Common/Clients/ResourceMappingClient';
import { ResourceSearchResult } from '../Contracts/Graph';

export const useResourceSearching = (searchText: string) => {
    const [resourcesSearchResult, setResourcesSearchResult] = useState<ResourceSearchResult[]>([]);
    const [moreResourcesSearchResultToLoad, setMoreResourcesSearchResultToLoad] = useState<boolean>(true);
    const [isLoadingInitialResults, setIsLoadingInitialResults] = useState<boolean>(true);

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const resourceMappingClient = ResourceMappingClient.getInstance(sreAgentEndpoint);

    const loadResultsCallId = useRef<number>(0);
    const isLoadingSearchResults = useRef<boolean>(false);
    const intersectionObserverRef = useRef<HTMLDivElement | null>(null);
    const currentScrollTop = useRef<number>(0);
    const resourcesSearchResultDivRef = useRef<HTMLDivElement | null>(null);
    const pageIndexRef = useRef<number>(0);

    const getSearchedResults = async (searchText: string, pageIndex: number) => {
        return resourceMappingClient.searchResource(searchText, pageIndex);
    };

    const loadResults = useCallback(async (): Promise<boolean | undefined> => {
        if (!isLoadingSearchResults.current && !isLoadingInitialResults) {
            const callId = loadResultsCallId.current;
            isLoadingSearchResults.current = true;

            const resultResponse = await getSearchedResults(searchText, pageIndexRef.current);

            if (callId === loadResultsCallId.current) {
                const result = resultResponse.content;
                if (resultResponse.isSuccessful && result) {
                    const { data, has_next_page, page_index } = result;
                    setMoreResourcesSearchResultToLoad(has_next_page);
                    pageIndexRef.current += page_index + 1;
                    setResourcesSearchResult(prev => [...prev, ...data]);
                }

                isLoadingSearchResults.current = false;
                return resultResponse.isSuccessful;
            } else {
                isLoadingSearchResults.current = false;
                return undefined;
            }
        }
    }, [searchText, isLoadingInitialResults]);

    const loadResultsRef = useRef<() => Promise<boolean | undefined>>(loadResults);
    loadResultsRef.current = loadResults;

    useEffect(() => {
        return () => {
            loadResultsCallId.current += 1;
        };
    }, [searchText, isLoadingInitialResults]);

    const handleScroll = debounce(() => {
        loadResults();
    }, 300);

    const onScroll = () => {
        const previousScrollTop = currentScrollTop.current;
        currentScrollTop.current = resourcesSearchResultDivRef.current?.scrollTop || 0;

        if (currentScrollTop.current > previousScrollTop && moreResourcesSearchResultToLoad) {
            handleScroll();
        }
    };

    useEffect(() => {
        const observer = new IntersectionObserver((entries: IntersectionObserverEntry[]) => {
            const entry = entries[0];
            if (entry.isIntersecting && intersectionObserverRef.current) {
                // Unobserve immediately to prevent multiple triggers
                observer.unobserve(intersectionObserverRef.current);

                // Trigger the load request and wait for it to complete
                const loadFunction = loadResultsRef.current;
                if (loadFunction) {
                    loadFunction().finally(() => {
                        if (intersectionObserverRef.current) {
                            observer.observe(intersectionObserverRef.current);
                        }
                    });
                }
            }
        });
        if (observer && intersectionObserverRef.current && !isLoadingInitialResults) {
            observer.observe(intersectionObserverRef.current);
        }

        return () => {
            observer?.disconnect();
        };
    }, [isLoadingInitialResults]);

    useEffect(() => {
        let isSubscribed = true;

        setIsLoadingInitialResults(true);
        setMoreResourcesSearchResultToLoad(true);

        const setInitialResources = async () => {
            pageIndexRef.current = 0;

            setResourcesSearchResult(prev =>
                prev.filter(r => (searchText ? r.name.toLocaleLowerCase().includes(searchText.toLocaleLowerCase()) : true))
            );

            isLoadingSearchResults.current = true;

            const initialResourcesResponse = await getSearchedResults(searchText, pageIndexRef.current);

            const initialResources = initialResourcesResponse.content;

            if (isSubscribed) {
                if (initialResourcesResponse.isSuccessful && initialResources) {
                    const { data, has_next_page, page_index } = initialResources;
                    setMoreResourcesSearchResultToLoad(has_next_page);
                    pageIndexRef.current += page_index + 1;
                    setResourcesSearchResult(data);
                }

                setIsLoadingInitialResults(false);
            }

            isLoadingSearchResults.current = false;
        };

        setInitialResources();

        return () => {
            isSubscribed = false;
        };
    }, [searchText]);

    return {
        resourcesSearchResult,
        moreResourcesSearchResultToLoad,
        isLoadingInitialResults,

        resourcesSearchResultDivRef,
        intersectionObserverRef,

        onScroll,
    };
};
