import { Button, Text } from '@fluentui/react-components';
import { useIntl } from 'react-intl';
import { resolveResourceIcon } from '../../../Common/Helpers/Resources';
import { ConnectorsResources } from '../../../Strings/SREAgentResources';
import { useConnectorsStyles } from './Connectors.styles';

export enum EmptyStateType {
    NoItems,
    NoSearchResults,
}

interface EmptyStateProps {
    variant: EmptyStateType;
    onPrimaryAction: () => void;
    isActionDisabled?: boolean;
}

export const EmptyState: React.FC<EmptyStateProps> = ({ variant, onPrimaryAction, isActionDisabled = false }) => {
    const intl = useIntl();
    const styles = useConnectorsStyles();

    if (variant === EmptyStateType.NoSearchResults) {
        return (
            <div className={`${styles.noSearchResultsContainer} ${styles.textContainer}`}>
                <Text className={styles.secondaryTitle}>{intl.formatMessage(ConnectorsResources.noSearchResults)}</Text>
                <Text className={styles.searchDescription}>{intl.formatMessage(ConnectorsResources.noSearchResultsDescription)}</Text>
            </div>
        );
    }

    return (
        <div className={styles.noItemsContainer}>
            <img
                src={resolveResourceIcon('connectors')}
                style={{ height: 180, width: 180 }}
                alt={intl.formatMessage(ConnectorsResources.connectors)}
            />
            <div className={styles.textContainer}>
                <Text className={styles.primaryTitle}>{intl.formatMessage(ConnectorsResources.emptyStateTitle)}</Text>
                <Text className={styles.description}>{intl.formatMessage(ConnectorsResources.emptyStateDescription)}</Text>
            </div>
            <Button appearance="primary" onClick={onPrimaryAction} disabled={isActionDisabled}>
                {intl.formatMessage(ConnectorsResources.addAConnector)}
            </Button>
        </div>
    );
};

export default EmptyState;
