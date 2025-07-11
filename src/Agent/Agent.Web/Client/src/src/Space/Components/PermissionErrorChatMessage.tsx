import { memo, useContext } from 'react';
import { StreamingContext } from '../Contracts/Context';
import ErrorChatMessage from './ErrorChatMessage';

const PermissionErrorChatMessage = ({ isLoading }: { isLoading?: boolean }) => {
    const { noPermission } = useContext(StreamingContext);

    return !isLoading && noPermission && <ErrorChatMessage error={'PermissionDenied'} />;
};

export default memo(PermissionErrorChatMessage);
