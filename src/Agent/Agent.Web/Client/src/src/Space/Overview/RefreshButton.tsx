import { Button, Tooltip } from '@fluentui/react-components';
import { ArrowClockwise16Regular } from '@fluentui/react-icons';
import { FC, memo, useCallback, useState } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../Strings/SREAgentResources';

interface RefreshButtonProps {
    refresh: () => Promise<unknown>;
}

const RefreshButton: FC<RefreshButtonProps> = ({ refresh }) => {
    const intl = useIntl();
    const [isRefreshing, setIsRefreshing] = useState(false);

    const handleRefresh = useCallback(async () => {
        setIsRefreshing(true);
        try {
            await refresh();
        } finally {
            setIsRefreshing(false);
        }
    }, [refresh]);

    return (
        <Tooltip content={intl.formatMessage(SreAgentResources.refresh)} relationship="label">
            <Button
                icon={<ArrowClockwise16Regular />}
                appearance="transparent"
                onClick={handleRefresh}
                disabled={isRefreshing}
                aria-label={intl.formatMessage(SreAgentResources.refresh)}
            />
        </Tooltip>
    );
};

export default memo(RefreshButton);
