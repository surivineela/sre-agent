import React from 'react';
import { createIntl, createIntlCache, IntlShape, useIntl } from 'react-intl';

const intlCache = createIntlCache();
let INTL: IntlShape | undefined;

/** Used for accessing loc resources outside of components (utils, etc.) */
export const getIntl = () => {
    return (
        INTL ??
        createIntl(
            {
                locale: 'en',
                messages: {},
                defaultLocale: 'en',
            },
            intlCache
        )
    );
};

/** To be used in unit tests (beforeEach resetter) */
export const resetIntl = () => (INTL = undefined);

interface IntlGlobalProviderProps {
    children: React.ReactNode;
}

export const IntlGlobalProvider = ({ children }: IntlGlobalProviderProps) => {
    INTL = useIntl();

    return <>{children}</>;
};
