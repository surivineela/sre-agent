import {
    Button,
    Combobox,
    Dialog,
    DialogBody,
    DialogSurface,
    DialogTitle,
    Field,
    MessageBar,
    MessageBarBody,
    Option,
    ToolbarButton,
} from '@fluentui/react-components';
import { Dismiss24Regular, InfoFilled, WarningFilled } from '@fluentui/react-icons';
import { FC, useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { ExtendedAgent } from '../../Contracts/ExtendedAgentGraph';
import { useAddAggentHandoffDialogStyles } from './AddExistingAggentHandoffDialog.Styles';

export interface AddExistingAggentHandoffDialogProps {
    onDismiss: () => void;
    agents: ExtendedAgent[];
    addHandoffToAgent: (sourceAgentName: string, targetAgentName: string) => void;
    handoffInfo?: {
        mode: 'sourcePicker' | 'targetPicker';
        currentAgent?: ExtendedAgent;
    };
}

export const AddExistingAggentHandoffDialog: FC<AddExistingAggentHandoffDialogProps> = ({
    onDismiss,
    agents,
    addHandoffToAgent,
    handoffInfo,
}) => {
    const intl = useIntl();
    const styles = useAddAggentHandoffDialogStyles();

    const [searchQuery, setSearchQuery] = useState<string>('');
    const [selectedAgent, setSelectedAgent] = useState<ExtendedAgent | undefined>(undefined);

    const options = useMemo(() => {
        const filteredAgents = agents.filter(agent => {
            if (!handoffInfo || !handoffInfo.currentAgent || handoffInfo.currentAgent.name === agent.name) {
                return false;
            }

            if (handoffInfo.mode === 'sourcePicker') {
                // We want to show agents that can handoff to the current agent, so we filter out agents that already have the current agent as a handoff target
                return !agent.handoffs?.some(handoffAgent => handoffAgent === handoffInfo.currentAgent?.name);
            } else {
                // We want to show agents that the current agent can handoff to, so we filter out agents that the current agent already has as handoff targets
                return !handoffInfo.currentAgent.handoffs?.some(handoffAgent => handoffAgent === agent.name);
            }
        });

        return filteredAgents.map(agent => ({
            key: agent.name,
            text: agent.name,
        }));
    }, [agents, handoffInfo]);

    const dialogTitle = useMemo(() => {
        if (!handoffInfo) {
            return '';
        }

        return intl.formatMessage(
            handoffInfo.mode === 'sourcePicker'
                ? ExtendedAgentsGraphResources.addHandoffFromExistingAgent
                : ExtendedAgentsGraphResources.addHandoffToExistingAgent
        );
    }, [intl, handoffInfo]);

    const noContextMessage = useMemo(() => {
        if (!handoffInfo) {
            return '';
        }

        return intl.formatMessage(
            handoffInfo.mode === 'sourcePicker'
                ? ExtendedAgentsGraphResources.noTargetAgentSpecified
                : ExtendedAgentsGraphResources.noSourceAgentSpecified
        );
    }, [intl, handoffInfo]);

    const filteredOptions = useMemo(() => {
        if (!searchQuery) {
            return options;
        }
        return options.filter(option => option.text.toLowerCase().includes(searchQuery.toLowerCase()));
    }, [options, searchQuery]);

    const clearAndDismiss = useCallback(() => {
        setSearchQuery('');
        setSelectedAgent(undefined);
        onDismiss();
    }, [onDismiss]);

    return (
        <Dialog
            open={!!handoffInfo}
            onOpenChange={(_, data) => {
                if (!data.open) {
                    clearAndDismiss();
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
                                    onClick={clearAndDismiss}
                                />
                            }
                        >
                            {dialogTitle}
                        </DialogTitle>
                    </div>
                    <div className={styles.dialogContentWrapper}>
                        {handoffInfo && !handoffInfo?.currentAgent ? (
                            <MessageBar intent="warning" icon={<WarningFilled />}>
                                <MessageBarBody>{noContextMessage}</MessageBarBody>
                            </MessageBar>
                        ) : handoffInfo && !agents.length ? (
                            <MessageBar intent="info" icon={<InfoFilled />}>
                                <MessageBarBody>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.noAgentsAvailableForHandoff)}
                                </MessageBarBody>
                            </MessageBar>
                        ) : null}
                        <Field label={intl.formatMessage(ExtendedAgentsGraphResources.subagentName)} required={true}>
                            <Combobox
                                id="agentSearchComboBox"
                                value={searchQuery}
                                placeholder={intl.formatMessage(ExtendedAgentsGraphResources.searchPlaceholder)}
                                onOptionSelect={(_event, data) => {
                                    setSelectedAgent(data.optionValue ? agents.find(agent => agent.name === data.optionValue) : undefined);
                                    setSearchQuery(data.optionValue || '');
                                }}
                                disabled={!options.length}
                                onInput={event => {
                                    const inputValue = (event.target as any).value as string;
                                    setSearchQuery(inputValue);
                                }}
                                positioning={{
                                    position: 'below',
                                    align: 'start',
                                }}
                            >
                                {!filteredOptions?.length ? (
                                    <div className={styles.emptyOptionsMessage}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.noAgentsFoundForHandoff)}
                                    </div>
                                ) : (
                                    <div className={styles.optionsWrapper}>
                                        {filteredOptions?.map(option => (
                                            <Option
                                                key={option.key}
                                                value={option.key}
                                                text={option.text}
                                                checkIcon={null}
                                                className={styles.option}
                                            >
                                                <span className={styles.optionContent}>{option.text}</span>
                                            </Option>
                                        ))}
                                    </div>
                                )}
                            </Combobox>
                        </Field>
                    </div>
                    <div className={styles.buttonsContainer}>
                        <Button
                            appearance="primary"
                            onClick={() => {
                                const sourceAndTarget = {
                                    source: handoffInfo?.mode === 'sourcePicker' ? selectedAgent?.name : handoffInfo?.currentAgent?.name,
                                    target: handoffInfo?.mode === 'sourcePicker' ? handoffInfo?.currentAgent?.name : selectedAgent?.name,
                                };
                                addHandoffToAgent(sourceAndTarget.source!, sourceAndTarget.target!);
                                clearAndDismiss();
                            }}
                            disabled={!selectedAgent}
                        >
                            {intl.formatMessage(ExtendedAgentsGraphResources.addSubagent)}
                        </Button>
                        <Button appearance="secondary" onClick={clearAndDismiss}>
                            {intl.formatMessage(SreAgentResources.cancel)}
                        </Button>
                    </div>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
