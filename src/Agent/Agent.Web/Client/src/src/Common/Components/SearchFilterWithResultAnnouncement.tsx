import { Announced } from '@fluentui/react/lib/Announced';
import { SearchBox } from '@fluentui/react/lib/SearchBox';
import { debounce } from 'lodash';
import { memo, useCallback, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ComponentResources } from '../../Strings/SREAgentResources';

export const SearchFilterWithResultAnnouncement = memo(
    (props: {
        id: string;
        setFilterValue: (value: string) => void;
        gridItemsCount: number;
        filter: string;
        placeHolder?: string;
        disabled?: boolean;
        autoFocus?: boolean;
    }): JSX.Element => {
        const { id, setFilterValue, gridItemsCount, filter, placeHolder, disabled, autoFocus } = props;
        const intl = useIntl();

        // Add onSearch as ariaLabel is only announced if onSearch is triggered
        const onSearch = useCallback(
            (val?: string) => {
                setFilterValue(val || '');
            },
            [setFilterValue]
        );

        const onChange = useCallback(
            debounce((_e: React.ChangeEvent<HTMLInputElement> | undefined, val?: string) => {
                onSearch(val);
            }, 200),
            [onSearch]
        );

        const ariaLabel = useMemo(() => {
            if (gridItemsCount === 0) {
                return filter.length > 0
                    ? intl.formatMessage(ComponentResources.noResultsFoundFor, { searchString: filter })
                    : intl.formatMessage(ComponentResources.noResultsFound);
            } else {
                const resultOrResults =
                    gridItemsCount === 1 ? intl.formatMessage(ComponentResources.result) : intl.formatMessage(ComponentResources.results);
                return filter.length > 0
                    ? intl.formatMessage(ComponentResources.gridItemsCountAriaLabel, {
                          numOfResults: gridItemsCount,
                          results: resultOrResults,
                          searchString: filter,
                      })
                    : intl.formatMessage(ComponentResources.gridItemsCountAriaLabelNoFilter, {
                          numOfResults: gridItemsCount,
                          results: resultOrResults,
                      });
            }
        }, [gridItemsCount, filter, intl]);

        return (
            <>
                {filter && <Announced message={ariaLabel} />}
                <SearchBox
                    id={id}
                    onChange={(event?: React.ChangeEvent<HTMLInputElement>, newValue?: string) => {
                        onChange(event, newValue);
                    }}
                    onSearch={onSearch}
                    placeholder={placeHolder}
                    iconProps={{ iconName: 'Search' }}
                    className="ms-slideDownIn20"
                    disabled={disabled}
                    autoFocus={autoFocus}
                />
            </>
        );
    }
);

SearchFilterWithResultAnnouncement.displayName = 'SearchFilterWithResultAnnouncement';
