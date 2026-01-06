import {
    Button,
    Dialog,
    DialogBody,
    DialogSurface,
    DialogTitle,
    MessageBar,
    MessageBarActions,
    MessageBarBody,
    ToolbarButton,
} from '@fluentui/react-components';
import { Dismiss24Regular, WarningFilled } from '@fluentui/react-icons';
import { FC, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { IncidentManagementType } from '../../../Common/Contracts/Azure/SreAgent';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { SreAgentContext } from '../../Contracts/Context';
import { PrimaryNavItemValues, SecondaryNavItemValues } from '../../Contracts/SreAgentSpace';
import { useAgentSiteNavigate } from '../../Hooks/useAgentSiteNavigate';
import { HandlerCreateOrEditInfo, OperationStatus } from '../../IncidentManagement/CreateIncidentHandler/Contracts';
import CreateIncidentHandlerConsolidated from '../../IncidentManagement/CreateIncidentHandler/CreateIncidentHandlerConsolidated';
import { FilterSuggestion } from './components/FilterSuggestionCard';
import { SuggestionsPanel } from './components/SuggestionsPanel';
import { useIncidentTriggerCreateDialogStyles } from './IncidentTriggerCreateDialog.Styles';

export interface IncidentTriggerCreateDialogProps {
    onDismiss: (handlerName?: string, handlerId?: string, isNew?: boolean) => void;
    setHandlerOperationStatus: React.Dispatch<React.SetStateAction<OperationStatus | undefined>>;
    handlerCreateOrEditInfo?: HandlerCreateOrEditInfo;
}

export const IncidentTriggerCreateDialog: FC<IncidentTriggerCreateDialogProps> = props => {
    const intl = useIntl();
    const navigate = useAgentSiteNavigate();
    const styles = useIncidentTriggerCreateDialogStyles();

    const {
        incidentManagement: { incidentPlatformType },
    } = useContext(SreAgentContext);

    const noIncidentPlatformConfigured = useMemo(
        () => !incidentPlatformType || incidentPlatformType === IncidentManagementType.None,
        [incidentPlatformType]
    );

    const { onDismiss, handlerCreateOrEditInfo, setHandlerOperationStatus } = props;

    // Cache suggestions for each combination of owningTeamId and incidentType
    // Key format: "owningTeamId|incidentType"
    const [suggestionsCache, setSuggestionsCache] = useState<Map<string, FilterSuggestion[]>>(new Map());
    const [teamNamesCache, setTeamNamesCache] = useState<Map<string, string>>(new Map());
    const [loadingSuggestions, setLoadingSuggestions] = useState(false);
    const [appliedSuggestionIndex, setAppliedSuggestionIndex] = useState<number | null>(null);
    const [currentCacheKey, setCurrentCacheKey] = useState<string | undefined>(undefined);
    const [suggestionsError, setSuggestionsError] = useState<string | undefined>(undefined);

    return (
        <Dialog
            open={!!handlerCreateOrEditInfo}
            onOpenChange={(_, data) => {
                if (!data.open) {
                    onDismiss();
                }
            }}
        >
            <DialogSurface className={styles.dialogSurface}>
                <DialogBody className={styles.dialogBody}>
                    <div className={styles.dialogTitleWrapper}>
                        <DialogTitle
                            className={styles.dialogTitle}
                            action={
                                <ToolbarButton
                                    aria-label={intl.formatMessage(SreAgentResources.close)}
                                    appearance="transparent"
                                    icon={<Dismiss24Regular />}
                                    onClick={() => onDismiss()}
                                />
                            }
                        >
                            {intl.formatMessage(
                                handlerCreateOrEditInfo?.filter
                                    ? ExtendedAgentsGraphResources.editIncidentTrigger
                                    : handlerCreateOrEditInfo?.incidentTriggerWithLearningsInfo
                                      ? ExtendedAgentsGraphResources.createIncidentTriggerWithLearnings
                                      : ExtendedAgentsGraphResources.createIncidentTrigger
                            )}
                        </DialogTitle>
                    </div>
                    {noIncidentPlatformConfigured && (
                        <MessageBar intent="warning" icon={<WarningFilled />}>
                            <MessageBarBody>
                                {intl.formatMessage(ExtendedAgentsGraphResources.createIncidentTriggerNoPlatformMessage)}
                            </MessageBarBody>
                            <MessageBarActions>
                                <Button
                                    appearance="secondary"
                                    onClick={() => {
                                        navigate({
                                            primaryNavItemValue: PrimaryNavItemValues.Settings,
                                            secondaryNavItemValue: SecondaryNavItemValues.IncidentPlatform,
                                        });
                                    }}
                                >
                                    {intl.formatMessage(ExtendedAgentsGraphResources.createIncidentTriggerNoPlatformButton)}
                                </Button>
                            </MessageBarActions>
                        </MessageBar>
                    )}
                    {handlerCreateOrEditInfo && (
                        <CreateIncidentHandlerConsolidated
                            exitToHome={onDismiss}
                            setHandlerOperationStatus={setHandlerOperationStatus}
                            handlerCreateOrEditInfo={handlerCreateOrEditInfo}
                            suggestionsPanel={
                                incidentPlatformType === IncidentManagementType.Icm ? (
                                    <SuggestionsPanel
                                        incidentPlatformType={incidentPlatformType}
                                        suggestionsCache={suggestionsCache}
                                        setSuggestionsCache={setSuggestionsCache}
                                        teamNamesCache={teamNamesCache}
                                        setTeamNamesCache={setTeamNamesCache}
                                        loadingSuggestions={loadingSuggestions}
                                        setLoadingSuggestions={setLoadingSuggestions}
                                        appliedSuggestionIndex={appliedSuggestionIndex}
                                        setAppliedSuggestionIndex={setAppliedSuggestionIndex}
                                        currentCacheKey={currentCacheKey}
                                        setCurrentCacheKey={setCurrentCacheKey}
                                        suggestionsError={suggestionsError}
                                        setSuggestionsError={setSuggestionsError}
                                    />
                                ) : undefined
                            }
                        />
                    )}
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
