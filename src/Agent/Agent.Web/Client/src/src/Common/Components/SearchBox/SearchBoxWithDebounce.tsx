import { AriaLiveAnnouncer, SearchBox, SearchBoxProps, useAnnounce } from '@fluentui/react-components';
import debounce from 'lodash/debounce';
import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import useIntl from 'react-intl/src/components/useIntl';
import { SreAgentResources } from '../../../Strings/SREAgentResources';
import { AzPortalContext } from '../../AzPortalProxy/Providers/AzPortalProxyContext';
import { logFieldValueChange } from '../../Helpers/Telemetry';

const searchDelay = 300; // ms

type SearchBoxWithDebounceProps = {
    setSearchTerm: React.Dispatch<React.SetStateAction<string>>;
    /**
     * Set textToAnnnouce if you want search results to be announced for screen readers
     */
    textToAnnounce?: string;
    /**
     * If the search box is part of the component that is rendered directly under the document body or other portal container (e.g. Dialog, Tooltip, etc.),
     * textToAnnounce will not be announced because there is not an AnnounceProvider on top of this component.
     * In this case, set this prop to true to wrap the search box with AnnounceProvider to make sure the text is announced correctly.
     */
    isDirectlyUnderDocumentBody?: boolean;
} & SearchBoxProps;

export const SearchBoxWithDebounce = (props: SearchBoxWithDebounceProps) => {
    if (props.isDirectlyUnderDocumentBody) {
        return (
            <AriaLiveAnnouncer>
                <SearchBoxWithDebounceInner {...props} />
            </AriaLiveAnnouncer>
        );
    }

    return <SearchBoxWithDebounceInner {...props} />;
};

const SearchBoxWithDebounceInner = (props: Omit<SearchBoxWithDebounceProps, 'isDirectlyUnderDocumentBody'>) => {
    const { setSearchTerm, textToAnnounce, placeholder, ...rest } = props;

    const intl = useIntl();
    const { announce } = useAnnounce();

    const { log } = useContext(AzPortalContext);

    const [inputValue, setInputValue] = useState<string>('');

    const handleSearch = useCallback(
        (newValue = '') => {
            setSearchTerm(newValue);
            logFieldValueChange('Searchbox', newValue, log);
        },
        [setSearchTerm, log]
    );

    const debouncedSearchTermHandler = useMemo(() => debounce(handleSearch, searchDelay), [handleSearch]);

    useEffect(() => {
        return () => debouncedSearchTermHandler.cancel();
    }, [debouncedSearchTermHandler]);

    useEffect(() => {
        if (textToAnnounce) {
            announce(textToAnnounce);
        }
    }, [inputValue, textToAnnounce, announce, intl]);

    return (
        <SearchBox
            {...rest}
            value={inputValue}
            placeholder={placeholder ?? intl.formatMessage(SreAgentResources.search)}
            onChange={(_, newValue) => {
                setInputValue(newValue.value);
                debouncedSearchTermHandler(newValue.value);
            }}
        />
    );
};
