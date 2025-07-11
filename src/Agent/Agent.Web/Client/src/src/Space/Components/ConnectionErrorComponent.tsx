import { memo, useContext } from 'react';
import { ActivitiesResources } from '../../Strings/SREAgentResources';
import { StreamingContext } from '../Contracts/Context';
import ErrorMessageBar from './ErrorMessageBar';

const ConnectionErrorComponent = ({ isStreamingMessage }: { isStreamingMessage?: boolean }) => {
    const { isReconnecting } = useContext(StreamingContext);

    return (
        <ErrorMessageBar
            showError={isReconnecting && isStreamingMessage}
            title={ActivitiesResources.connectionErrorTitle}
            content={ActivitiesResources.reconnecting}
        />
    );
};

export default memo(ConnectionErrorComponent);
