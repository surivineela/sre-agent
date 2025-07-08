import { MessageBar, MessageBarBody, MessageBarTitle } from '@fluentui/react-message-bar';
import { memo, useContext } from 'react';
import { FormattedMessage } from 'react-intl';
import { ActivitiesResources } from '../../Strings/SREAgentResources';
import { StreamingContext } from '../Contracts/Context';

const ConnectionErrorComponent = ({ showError }: { showError?: boolean }) => {
    const { isReconnecting } = useContext(StreamingContext);

    return (
        isReconnecting &&
        showError && (
            <MessageBar intent={'error'}>
                <MessageBarBody>
                    <MessageBarTitle>
                        <FormattedMessage {...ActivitiesResources.connectionErrorTitle} />
                    </MessageBarTitle>
                    <FormattedMessage {...ActivitiesResources.reconnecting} />
                </MessageBarBody>
            </MessageBar>
        )
    );
};

export default memo(ConnectionErrorComponent);
