import { Button, makeStyles, tokens } from '@fluentui/react-components';
import { memo } from 'react';
import { FormattedMessage } from 'react-intl';
import { SreAgentResources } from '../../Strings/SREAgentResources';

const useThreadFiltersStyles = makeStyles({
    root: {
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        padding: '15px 0px',
        margin: '0px 15px',
    },
    filterContainer: {
        display: 'flex',
        alignItems: 'center',
        gap: `${tokens.spacingHorizontalS}`,
        flexWrap: 'wrap',
    },
});

const ThreadFilters = ({ unreadOnly, setUnreadOnly }: { unreadOnly: boolean; setUnreadOnly: (value: boolean) => void }) => {
    const { root, filterContainer } = useThreadFiltersStyles();

    return (
        <div className={root}>
            <div className={filterContainer}>
                <ThreadFilterButton isSelected={unreadOnly} update={setUnreadOnly} />
            </div>
        </div>
    );
};

const ThreadFilterButton = memo(({ isSelected, update }: { isSelected: boolean; update: (value: boolean) => void }) => {
    return (
        <Button shape={'circular'} size={'small'} appearance={isSelected ? 'primary' : 'outline'} onClick={() => update(!isSelected)}>
            <FormattedMessage {...SreAgentResources.unread} />
        </Button>
    );
});

export default memo(ThreadFilters);
