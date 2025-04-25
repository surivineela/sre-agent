import React, { useEffect, useState } from 'react';
import { MessageFormatElement, IntlProvider as ReactIntlProvider } from 'react-intl';
import { IntlGlobalProvider } from './intl';

const loadLocaleData = async (
    locale: string,
    stringOverrides?: Record<string, string>
): Promise<Record<string, string> | Record<string, MessageFormatElement[]>> => {
    let messages: any = {};
    switch (locale.split('-')[0].toLowerCase()) {
        case 'nl': {
            messages = await import('../compiled/strings.nl.json');
            break;
        }
        case 'pl': {
            messages = await import('../compiled/strings.pl.json');
            break;
        }
        case 'pt': {
            if (locale === 'pt-BR') {
                messages = await import('../compiled/strings.pt-BR.json');
            } else {
                messages = await import('../compiled/strings.pt-PT.json');
            }
            break;
        }
        case 'ru': {
            messages = await import('../compiled/strings.ru.json');
            break;
        }
        case 'sv': {
            messages = await import('../compiled/strings.sv.json');
            break;
        }
        case 'tr': {
            messages = await import('../compiled/strings.tr.json');
            break;
        }
        case 'zh': {
            if (locale === 'zh-Hans') {
                messages = await import('../compiled/strings.zh-Hans.json');
            } else {
                messages = await import('../compiled/strings.zh-Hant.json');
            }
            break;
        }
        case 'fr': {
            messages = await import('../compiled/strings.fr.json');
            break;
        }
        case 'en': {
            if (locale === 'en-XA') {
                messages = await import('../compiled/strings.en-XA.json');
            } else {
                messages = await import('../compiled/strings.json');
            }
            break;
        }
        case 'cs': {
            messages = await import('../compiled/strings.cs.json');
            break;
        }
        case 'de': {
            messages = await import('../compiled/strings.de.json');
            break;
        }
        case 'es': {
            messages = await import('../compiled/strings.es.json');
            break;
        }
        case 'hu': {
            messages = await import('../compiled/strings.hu.json');
            break;
        }
        case 'id': {
            messages = await import('../compiled/strings.id.json');
            break;
        }
        case 'it': {
            messages = await import('../compiled/strings.it.json');
            break;
        }
        case 'ja': {
            messages = await import('../compiled/strings.ja.json');
            break;
        }
        case 'ko': {
            messages = await import('../compiled/strings.ko.json');
            break;
        }
        default: {
            messages = await import('../compiled/strings.json');
            break;
        }
    }

    // Any strings with a key that has symbols gets compiled to be in the default object rather than exported individually as a module
    const defaultMessages = messages.default ?? {};
    return { ...messages, ...defaultMessages, ...(stringOverrides ?? {}) };
};

export interface IntlProviderProps {
    locale: string;
    children?: React.ReactNode;
    stringOverrides?: Record<string, string>;
}

export const IntlProvider = (props: IntlProviderProps) => {
    const { locale, children, stringOverrides } = props;

    const [locMessages, setLocMessages] = useState<Record<string, string> | Record<string, MessageFormatElement[]>>({});

    useEffect(() => {
        const initLocData = async () => {
            const messages = await loadLocaleData(locale, stringOverrides);
            setLocMessages(messages);
        };

        initLocData();
    }, [locale, stringOverrides]);

    return (
        <ReactIntlProvider
            locale={locale}
            defaultLocale='en'
            messages={locMessages}
            onError={(err) => {
                if (err.code === 'MISSING_TRANSLATION') {
                    return;
                }

                throw err;
            }}
        >
            <IntlGlobalProvider>{children}</IntlGlobalProvider>
        </ReactIntlProvider>
    );
};
