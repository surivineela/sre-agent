import type { IMessage } from './IMessage';

export interface IMessageService {
  /**
   * Gets all messages in the service
   */
  getMessages: (this: IMessageService) => IMessage[];

  /**
   * Adds a message to the service
   */
  addMessage: (this: IMessageService, message: IMessage) => void;

  /**
   * Adds an error message to the context
   */
  addErrorMessage: (
    this: IMessageService,
    title: string,
    message?: string,
    options?: Partial<IMessage>,
  ) => void;

  /**
   * Adds a warning message to the context
   */
  addWarningMessage: (
    this: IMessageService,
    title: string,
    message?: string,
    options?: Partial<IMessage>,
  ) => void;

  /**
   * Adds an info message to the context
   */
  addInfoMessage: (
    this: IMessageService,
    title: string,
    message?: string,
    options?: Partial<IMessage>,
  ) => void;

  /**
   * Adds a success message to the context
   */
  addSuccessMessage: (
    this: IMessageService,
    title: string,
    message?: string,
    options?: Partial<IMessage>,
  ) => void;

  /**
   * Dismisses a specific message by id
   */
  dismissMessage: (this: IMessageService, id: string) => void;

  /**
   * Dismisses all messages
   */
  dismissAllMessages: (this: IMessageService) => void;

  /**
   * Adds a listener that will be called when messages change
   */
  addListener: (this: IMessageService, listener: () => void) => void;

  /**
   * Removes a listener
   */
  removeListener: (this: IMessageService, listener: () => void) => void;
}
