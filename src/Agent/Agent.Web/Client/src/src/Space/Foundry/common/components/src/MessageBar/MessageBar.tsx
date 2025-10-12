import type { MessageBarIntent, Slot } from '@fluentui/react-components';
import {
    MessageBar as FluentMessageBar,
    mergeClasses,
    MessageBarActions,
    MessageBarBody,
    MessageBarTitle,
} from '@fluentui/react-components';
import { DismissRegular } from '@fluentui/react-icons';
import type { JSX, ReactNode } from 'react';
import { useState } from 'react';
import useIntl from 'react-intl/src/components/useIntl';
import { ThreadTraceResources } from '../../../../../../Strings/SREAgentResources';
import { Button } from '../Button/Button';
import { useMessageBarStyles } from './MessageBar.Styles';

export interface IMessageBarProps {
    title?: string | ReactNode;
    message: string | ReactNode;
    intent: MessageBarIntent;
    className?: string;
    /**
     * If true, shows a dismiss button that allows the user to hide the message bar.
     * Once dismissed, the message bar will not be shown again.
     * @default true
     */
    dismissible?: boolean;
    /**
     * If dismissible is true, this function will be called when the dismiss button is clicked.
     * This can be useful to remove the message bar from the component tree.
     */
    onDismiss?: () => void;
    /**
     * Additional actions to be displayed in the right side of the message bar.
     * This can be used to add buttons or links for further actions.
     */
    additionalActions?: ReactNode;
    /**
     * ARIA role for the message bar.
     * @default 'alert'
     */
    role?: 'alert' | 'group';
    /**
     * Override the default icon for the message bar.
     */
    icon?: Slot<'div'>;
    /**
     * ARIA live region for the message bar.
     * @default 'polite'
     */
    'aria-live'?: 'assertive' | 'polite' | 'off';
}

export function MessageBar({
    title,
    message,
    intent,
    className,
    dismissible = true,
    onDismiss,
    additionalActions,
    role = 'alert',
    icon,
    'aria-live': ariaLive = 'polite',
}: IMessageBarProps): JSX.Element | null {
    const [visible, setVisible] = useState(true);
    const intl = useIntl();
    const styles = useMessageBarStyles();

    if (!visible) {
        return null;
    }

    return (
        <FluentMessageBar
            aria-live={ariaLive}
            className={mergeClasses(styles.messageBar, className)}
            icon={icon}
            intent={intent}
            role={role}
        >
            <MessageBarBody>
                {title === undefined ? null : <MessageBarTitle>{title}</MessageBarTitle>}
                {message}
            </MessageBarBody>
            {dismissible || additionalActions != null ? (
                <MessageBarActions
                    containerAction={
                        dismissible ? (
                            <Button
                                appearance="subtle"
                                aria-label={intl.formatMessage(ThreadTraceResources.dismiss)}
                                icon={<DismissRegular aria-hidden={true} />}
                                onClick={() => {
                                    setVisible(false);
                                    onDismiss?.();
                                }}
                            />
                        ) : undefined
                    }
                >
                    {additionalActions}
                </MessageBarActions>
            ) : null}
        </FluentMessageBar>
    );
}
