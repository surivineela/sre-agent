import type { MessageType } from './MessageType';
import type { LinkProps } from '@fluentui/react-components';
import type { JSX } from 'react';

export interface IMessage {
  messageType: MessageType;
  /**
   * Localized message bar title
   */
  title: string;
  /**
   * Contents of the message to be displayed in the body
   */
  message: string;
  /**
   * Custom react component to be rendered in place of the message string.
   * When this component is provided, the message string will not be rendered.
   */
  messageComponent?: JSX.Element;
  /**
   * Used to uniquely id messages in app insights
   * Displayed below message
   * Included in telemetry for logged messages
   */
  traceId?: string;
  /**
   * ID which can be used for message De-Duping.
   */
  id?: string;
  /**
   * When true the message body will be wrapped in a "pre" tag or equivalent css
   */
  isPreformatted?: boolean;
  details?: object[];
  /**
   * Optional hyperlink to be rendered after message body
   */
  link?: LinkProps;
  /**
   * Optional param to display 'More details' as Link button.
   * Currently used in a one off scenario where we want to have 'Skip validation' as action button and 'More details' as link button
   */
  moreDetailsAsLinkButton?: boolean;
  /**
   * Optional param to set text of 'More details' button.
   */
  moreDetailsButtonText?: string;
  /**
   * More error details to be displayed in a dialog
   */
  showErrorDetailsInDialog?: boolean;
  /**
   * Optional param to hide Message trace in the Message bar.
   * Currently used in a one off scenario where we want to surface message trace on 'More details' panel
   */
  hideMessageTraces?: boolean;
  /**
   * Prevent message from being dismissed
   */
  unDismissable?: boolean;
  /**
   * Displayed below message
   * Included in telemetry for logged messages
   */
  clientRequestId?: string;
  /**
   * Displayed below message
   * Included in telemetry for logged messages
   */
  serviceRequestId?: string;
  /**
   * Displayed below message
   * Included in telemetry for logged messages
   */
  apimRequestId?: string;
  /**
   * Controls whether or not this message is logged in AppInsights
   * as a UxUserError event.
   * MessageType must also be Error.
   * default: false
   */
  skipTelemetry?: boolean;
  /**
   * Included in telemetry for logged messages
   */
  errorCode?: string;
  /**
   * Included in telemetry for logged messages
   * If true the UxUserError event is counted as a user-error and not as a system-error in the metrics dashboard.
   */
  isUserError?: boolean;
  /**
   * Unique name for a message which will be stored in user settings
   * when dismissed.
   * Any future messages with this same name will no longer be displayed.
   */
  persistentMessageId?: string;
  /**
   * If true this message will not be dismissed with the dismissAllMessages action
   */
  sticky?: boolean;
  /**
   * If enabled will allow for expand/collapse of message, initially collapsed.
   * If disabled message will always be in expanded state.
   * default: true
   */
  truncated?: boolean;
  /**
   * Whether or not to place the body of the message on a new line, under title.
   * Otherwise the message body will be rendered in line with the title.
   * For service client errors default is true.
   * default: false
   */
  bodyOnNewLine?: boolean;
  /*
   * If you pass isMultiline as false explicitly, your messageBar will be single line and truncation will happen automatically
   * If your message has actions, and you pass isMultline false, your content will be truncated and will not be expand/collapse-able, even when you zoom
   * Defaults to: true if you provide actions, false if you do not
   */
  isMultiline?: boolean;
  /**
   * Additional actions to be shown. This will be prepended to any default actions that may be created from the message.
   */
  actions?: JSX.Element;

  /**
   * Hide the error feedback portion of the error. Used if space is a concern.
   */
  hideErrorFeedback?: boolean;
  /**
   * If this message is added to a message context it will be called when the message is dismissed from that context.
   * It is not called by the MessageBar or MessageComponent components
   *
   * @param message message that has been dismissed
   */
  onDismiss?: (message: IMessage) => void;
}
