import { Button, Card, Text } from '@fluentui/react-components';
import { CheckmarkRegular, WarningRegular } from '@fluentui/react-icons';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { useFilterSuggestionCardStyles } from './FilterSuggestionCard.Styles';

export interface FilterSuggestion {
    filterName: string;
    titleContains: string | null;
    severity: string | null;
    incidentType: string;
    count: number;
}

export interface FilterSuggestionCardProps {
    suggestion: FilterSuggestion;
    onApply: () => void;
    isApplied: boolean;
}

export const FilterSuggestionCard: FC<FilterSuggestionCardProps> = ({ suggestion, onApply, isApplied }) => {
    const intl = useIntl();
    const styles = useFilterSuggestionCardStyles();

    const filterDetails = [];

    if (suggestion.titleContains) {
        filterDetails.push({
            label: intl.formatMessage(ExtendedAgentsGraphResources.incidentTitleContains),
            value: suggestion.titleContains,
        });
    }

    if (suggestion.severity) {
        filterDetails.push({
            label: intl.formatMessage(ExtendedAgentsGraphResources.severityLabel),
            value: suggestion.severity,
        });
    }

    if (suggestion.incidentType) {
        filterDetails.push({
            label: intl.formatMessage(ExtendedAgentsGraphResources.smartFilterIncidentType),
            value: suggestion.incidentType,
        });
    }

    return (
        <Card className={styles.card}>
            <div className={styles.cardHeader}>
                <div className={styles.iconWrapper}>
                    <WarningRegular className={styles.icon} />
                </div>
                <div className={styles.headerContent}>
                    <Text weight="semibold" className={styles.filterName}>
                        {suggestion.filterName}
                    </Text>
                    <Text size={200} className={styles.incidentCount}>
                        {intl.formatMessage(ExtendedAgentsGraphResources.smartFilterIncidentCount)} : {suggestion.count}
                    </Text>
                </div>
            </div>

            <div className={styles.cardBody}>
                {filterDetails.length > 0 ? (
                    filterDetails.map((detail, index) => (
                        <div key={index} className={styles.filterRow}>
                            <Text size={200} className={styles.filterLabel}>
                                {detail.label}:
                            </Text>
                            <Text size={200} className={styles.filterValue}>
                                {detail.value}
                            </Text>
                        </div>
                    ))
                ) : (
                    <Text size={200} className={styles.noFilters}>
                        {intl.formatMessage(ExtendedAgentsGraphResources.smartFilterNoFilterFound)}
                    </Text>
                )}
            </div>

            <div className={styles.cardFooter}>
                <Button
                    appearance="primary"
                    size="small"
                    onClick={onApply}
                    className={isApplied ? styles.appliedButton : styles.applyButton}
                    icon={isApplied ? <CheckmarkRegular /> : undefined}
                    disabled={isApplied}
                >
                    {isApplied ? intl.formatMessage(SreAgentResources.applied) : intl.formatMessage(SreAgentResources.apply)}
                </Button>
            </div>
        </Card>
    );
};
