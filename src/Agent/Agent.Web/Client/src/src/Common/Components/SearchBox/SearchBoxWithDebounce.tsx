import { SearchBox, SearchBoxProps } from '@fluentui/react-components';
import debounce from 'lodash/debounce';
import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import useIntl from 'react-intl/src/components/useIntl';
import { SreAgentResources } from '../../../Strings/SREAgentResources';
import { AzPortalContext } from '../../AzPortalProxy/Providers/AzPortalProxyContext';
import { logFieldValueChange } from '../../Helpers/Telemetry';

const searchDelay = 300; // ms

type SearchBoxWithDebounceProps = { setSearchTerm: React.Dispatch<React.SetStateAction<string>> } & SearchBoxProps;

export const SearchBoxWithDebounce = (props: SearchBoxWithDebounceProps) => {
    const { setSearchTerm } = props;
    const intl = useIntl();
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

    return (
        <SearchBox
            {...props}
            value={inputValue}
            placeholder={props.placeholder ?? intl.formatMessage(SreAgentResources.search)}
            onChange={(_, newValue) => {
                setInputValue(newValue.value);
                debouncedSearchTermHandler(newValue.value);
            }}
        />
    );
};
