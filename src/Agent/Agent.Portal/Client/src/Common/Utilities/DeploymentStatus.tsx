import { Spinner, tokens } from '@fluentui/react-components';
import { CheckmarkCircle20Filled, DismissCircle20Filled } from '@fluentui/react-icons';
import { IntlShape } from 'react-intl';
import { DeployResources } from '../../Strings/Resources';

/**
 * Returns the appropriate status icon for a deployment operation status.
 * @param status - The deployment operation status string
 * @returns A JSX element representing the status (checkmark, error, or spinner)
 */
export const getDeploymentStatusIcon = (status: string): JSX.Element => {
    const normalizedStatus = status?.toLowerCase();

    if (normalizedStatus === 'succeeded') {
        return <CheckmarkCircle20Filled style={{ color: tokens.colorPaletteGreenForeground1 }} />;
    }

    if (normalizedStatus === 'failed') {
        return <DismissCircle20Filled style={{ color: tokens.colorPaletteRedForeground1 }} />;
    }

    return <Spinner size="tiny" />;
};

/**
 * Returns the localized text for a deployment operation status.
 * @param status - The deployment operation status string
 * @param intl - The react-intl IntlShape object for localization
 * @returns A localized string representing the status
 */
export const getDeploymentStatusText = (status: string, intl: IntlShape): string => {
    const normalizedStatus = status?.toLowerCase();

    switch (normalizedStatus) {
        case 'creating':
            return intl.formatMessage(DeployResources.creating);
        case 'succeeded':
            return intl.formatMessage(DeployResources.succeeded);
        case 'failed':
            return intl.formatMessage(DeployResources.failed);
        case 'updating':
            return intl.formatMessage(DeployResources.updating);
        case 'running':
            return intl.formatMessage(DeployResources.running);
        default:
            return status || intl.formatMessage(DeployResources.running);
    }
};
