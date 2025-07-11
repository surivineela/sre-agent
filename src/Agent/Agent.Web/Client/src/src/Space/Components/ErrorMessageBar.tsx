import { MessageBar, MessageBarBody, MessageBarTitle } from '@fluentui/react-message-bar';
import { memo } from 'react';
import { FormattedMessage, MessageDescriptor } from 'react-intl';

const ErrorMessageBar = ({
    showError,
    title,
    content,
}: {
    showError?: boolean;
    title?: MessageDescriptor;
    content?: MessageDescriptor;
}) => {
    return (
        showError && (
            <MessageBar intent={'error'} shape={'rounded'} layout={'multiline'}>
                <MessageBarBody>
                    {title && (
                        <MessageBarTitle>
                            <FormattedMessage {...title} />
                        </MessageBarTitle>
                    )}
                    {content && <FormattedMessage {...content} />}
                </MessageBarBody>
            </MessageBar>
        )
    );
};

export default memo(ErrorMessageBar);
