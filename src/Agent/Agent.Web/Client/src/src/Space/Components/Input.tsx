import { memo, useCallback, useState } from 'react';
import { useChatInputStyles } from '../Styles/Activities.styles';
import { TextField } from '@fluentui/react/lib/TextField';
import { IStyle, mergeStyles } from '@fluentui/react/lib/Styling';
import { Activities } from '../../Strings/SREResources.resjson';
import { IInputProps } from '../Contracts/Activities';
import { Button, tokens } from "@fluentui/react-components";
import { SendRegular } from "@fluentui/react-icons";

const Input = ({ sendMessage, disableInput }: IInputProps) => {
  const [input, setInput] = useState<string>();

  const { root, textFieldContainer, textField, footer } = useChatInputStyles();

  const chatInputHandleSendClick = useCallback(() => {
    const messageToSend = input?.trim() ?? '';

    if (messageToSend) {
      setInput('');
      sendMessage(messageToSend);
    }
  }, [input, sendMessage]);

  return (
    <div className={mergeStyles(root as IStyle)}>
      <div className={mergeStyles(textFieldContainer as IStyle)}>
        <TextField
          placeholder={Activities.chatInputPlaceholder}
          multiline={true}
          autoAdjustHeight={true}
          borderless={true}
          resizable={false}
          type="text"
          autoFocus={true}
          autoComplete="off"
          styles={textField}
          value={input}
          onChange={(_, value?: string) => setInput(value)}
          onKeyDown={event => {
            if (event.key.toLowerCase() === 'g') {
              // Stop the event from propagating to the global shortcuts
              event.stopPropagation();
            } else if (event.key.toLowerCase() === 'enter' && !event.shiftKey && !disableInput) {
              chatInputHandleSendClick();
              event.preventDefault();
              event.stopPropagation();
            }
          }}
        />
        <div className={mergeStyles(footer as IStyle)}>
          <Button icon={<SendRegular style={{ color: tokens.colorBrandForeground1	 }} />} disabled={disableInput} onClick={chatInputHandleSendClick} shape="circular" appearance="subtle" />
        </div>
      </div>
    </div>
  );
};

export default memo(Input);
