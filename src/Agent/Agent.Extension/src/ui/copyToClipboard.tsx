/*
Copyright (c) Microsoft Corporation. All rights reserved.
*/

import * as React from 'react';
import * as icons from './icons';
import './copyToClipboard.css';

type CopyToClipboardProps = {
  value: string;
};

/**
 * A copy to clipboard button.
 */
export const CopyToClipboard: React.FunctionComponent<CopyToClipboardProps> = ({ value }) => {
  type IconType = 'copy' | 'check' | 'cross';
  const [icon, setIcon] = React.useState<IconType>('copy');

  React.useEffect(() => {
    setIcon('copy');
  }, [value]);

  React.useEffect(() => {
    if (icon === 'check') {
      const timeout = setTimeout(() => {
        setIcon('copy');
      }, 3000);
      return () => clearTimeout(timeout);
    }
  }, [icon]);

  const handleCopy = React.useCallback(() => {
    navigator.clipboard.writeText(value).then(() => {
      setIcon('check');
    }, () => {
      setIcon('cross');
    });
  }, [value]);
  const iconElement = icon === 'check' ? icons.check() : icon === 'cross' ? icons.cross() : icons.copy();
  return <button className='copy-icon' title='Copy to clipboard' aria-label='Copy to clipboard' onClick={handleCopy}>{iconElement}</button>;
};
