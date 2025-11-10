import { CustomHeader } from '../../ConnectorWizardFormik';

export const BearerTokenConnectionString = 'Endpoint={0};AuthType=BearerToken;BearerToken={1};';
export const CustomHeadersConnectionString = 'Endpoint={0};AuthType=CustomHeaders;';

export const getBearerTokenConnectionString = (endpointUrl: string, bearerToken: string) => {
    return BearerTokenConnectionString.replace('{0}', endpointUrl).replace('{1}', bearerToken);
};

export const getCustomHeadersConnectionString = (endpointUrl: string, customHeaders: CustomHeader[]) => {
    let dataSource = CustomHeadersConnectionString.replace('{0}', endpointUrl);
    if (customHeaders && customHeaders.length > 0) {
        const headersString = customHeaders
            .map(header => {
                if (!header.key || !header.value) {
                    return '';
                }
                return `${header.key}=${header.value}`;
            })
            .join(';');
        dataSource += headersString;
    }

    return dataSource;
};
