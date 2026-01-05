import { Button, makeStyles, tokens } from '@fluentui/react-components';
import { memo } from 'react';
import { FormattedMessage } from 'react-intl';
import { SreAgentResources } from '../../Strings/SREAgentResources';

const useThreadFiltersStyles = makeStyles({
    root: {
        padding: '5px 0px',
        margin: '0px 15px',
    },
    filterContainer: {
        display: 'flex',
        alignItems: 'center',
        gap: `${tokens.spacingHorizontalS}`,
        flexWrap: 'wrap',
    },
});

const ThreadFilters = ({
    disabled,
    unreadOnly,
    setUnreadOnly,
}: {
    disabled: boolean;
    unreadOnly: boolean;
    setUnreadOnly: (value: boolean) => void;
}) => {
    const { root, filterContainer } = useThreadFiltersStyles();

    return (
        <div className={root}>
            <div className={filterContainer}>
                <ThreadFilterButton isSelected={unreadOnly} update={setUnreadOnly} disabled={disabled} />
            </div>
        </div>
    );
};

const ThreadFilterButton = memo(
    ({ isSelected, update, disabled }: { isSelected: boolean; update: (value: boolean) => void; disabled: boolean }) => {
        return (
            <Button
                shape={'circular'}
                size={'small'}
                appearance={isSelected ? 'primary' : 'outline'}
                onClick={() => update(!isSelected)}
                disabled={disabled}
            >
                <FormattedMessage {...SreAgentResources.unread} />
            </Button>
        );
    }
);

export default memo(ThreadFilters);
