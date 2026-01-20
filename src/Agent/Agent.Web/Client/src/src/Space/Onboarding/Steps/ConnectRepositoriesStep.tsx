import { Text } from '@fluentui/react-components';
import { PlugConnectedFilled } from '@fluentui/react-icons';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { OnboardingWizardResources } from '../../../Strings/SREAgentResources';
import { useRepositoriesStepStyles } from '../OnboardingWizard.styles';

/**
 * Step 3: Connect Repositories
 * Placeholder step - no form interaction needed
 */
export const ConnectRepositoriesStep: FC = () => {
    const intl = useIntl();
    const styles = useRepositoriesStepStyles();

    return (
        <div className={styles.container}>
            <PlugConnectedFilled className={styles.icon} aria-hidden="true" />
            <Text className={styles.title}>{intl.formatMessage(OnboardingWizardResources.repositoriesTitle)}</Text>
            <Text className={styles.description}>{intl.formatMessage(OnboardingWizardResources.repositoriesDescription)}</Text>
            <Text className={styles.comingSoonMessage}>{intl.formatMessage(OnboardingWizardResources.repositoriesComingSoon)}</Text>
        </div>
    );
};
