import { memo, useMemo } from 'react';
import { MessageDescriptor } from 'react-intl';
import { ChatMessageError } from '../../Common/Contracts/Azure/SreAgent';
import { ActivitiesResources } from '../../Strings/SREAgentResources';
import ErrorMessageBar from './ErrorMessageBar';

/**
 * A component that renders error message of a chat message content
 * @param param0
 * @returns
 */
const ErrorMessage = ({ error }: { error: ChatMessageError }) => {
    const { errorTitle, errorMessage } = useMemo(() => {
        let result: { errorTitle?: MessageDescriptor; errorMessage?: MessageDescriptor } = {};

        switch (error) {
            case 'PermissionDenied':
                result = {
                    errorTitle: undefined,
                    errorMessage: ActivitiesResources.insufficientChatPermissions,
                };
        }

        return result;
    }, [error]);

    return <ErrorMessageBar showError={!!errorMessage} title={errorTitle} content={errorMessage} />;
};

export default memo(ErrorMessage);
