import {
    Badge,
    Button,
    Dialog,
    DialogSurface,
    Divider,
    List,
    ListItem,
    Menu,
    MenuButton,
    MenuItem,
    MenuList,
    MenuPopover,
    MenuTrigger,
    mergeClasses,
    MessageBar,
} from '@fluentui/react-components';
import { ChevronDown12Regular, ChevronUp12Regular, PanelRightContractRegular, PanelRightExpandRegular } from '@fluentui/react-icons';
import { useFormikContext } from 'formik';
import { FC, useCallback, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { PlaygroundResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { ExtendedAgent } from '../../../Contracts/ExtendedAgentGraph';
import { buildAgentConfigurationYaml } from '../../ExtendedAgentYamlUtils';
import { AgentPlaygroundFormValues, QualityFinding } from './Contracts';
import { useQuickFixesDialogStyles } from './QuickFixesDialog.Styles';
import { getAgentWithFindingsApplied } from './Utility';
import { YamlDiffView } from './YamlDiffView';

export interface QuickFixesDialogProps extends QuickFixesDialogInnerProps {
    open: boolean;
}
export const QuickFixesDialog: FC<QuickFixesDialogProps> = ({ open, ...rest }) => {
    return (
        <Dialog open={open} modalType="modal">
            <QuickFixesDialogInner {...rest} />
        </Dialog>
    );
};

interface QuickFixesDialogInnerProps {
    agent: ExtendedAgent;
    findings: QualityFinding[];
    onClose: () => void;
    onApply: (agent: ExtendedAgent, save: boolean) => void;
    isStale?: boolean;
}

const QuickFixesDialogInner: FC<QuickFixesDialogInnerProps> = ({ agent, onClose, findings, isStale, onApply }) => {
    const { values } = useFormikContext<AgentPlaygroundFormValues>();
    const [diffPreviewCollapsed, setDiffPreviewCollapsed] = useState<boolean>(false);
    const [qualitySelection, setQualitySelection] = useState<string[]>([]);

    const currentAgentObject = useMemo(() => {
        // Generate the original YAML content without any findings applied
        return getAgentWithFindingsApplied([], values, agent);
    }, [values, agent]);

    const currentYamlContent = useMemo(() => {
        return buildAgentConfigurationYaml(currentAgentObject, false);
    }, [currentAgentObject]);

    const updatedAgentObject = useMemo(() => {
        const selectedFindings = findings.filter(finding => qualitySelection.includes(finding.id));
        return getAgentWithFindingsApplied(selectedFindings, values, agent);
    }, [qualitySelection, findings, values, agent]);

    const updatedYamlContent = useMemo(() => {
        return buildAgentConfigurationYaml(updatedAgentObject, false);
    }, [updatedAgentObject]);

    const styles = useQuickFixesDialogStyles();

    return (
        <DialogSurface className={styles.dialogSurface}>
            <div className={styles.dialogContentRow}>
                <FindingsListPanel
                    diffPreviewCollapsed={diffPreviewCollapsed}
                    qualitySelection={qualitySelection}
                    setQualitySelection={setQualitySelection}
                    findings={findings}
                    onClose={onClose}
                    onApply={save => {
                        onApply(updatedAgentObject, save);
                        onClose();
                    }}
                    updatedAgentObject={updatedAgentObject}
                    isStale={isStale}
                />
                <Divider vertical />
                <DiffPreviewPanel
                    diffPreviewCollapsed={diffPreviewCollapsed}
                    setDiffPreviewCollapsed={setDiffPreviewCollapsed}
                    updatedYamlContent={updatedYamlContent}
                    currentYamlContent={currentYamlContent}
                />
            </div>
        </DialogSurface>
    );
};

interface FindingsListPanelProps {
    diffPreviewCollapsed: boolean;
    qualitySelection: string[];
    setQualitySelection: (selection: string[]) => void;
    findings: QualityFinding[];
    onClose: () => void;
    onApply: (save: boolean) => void;
    updatedAgentObject: ExtendedAgent | null;
    isStale?: boolean;
}

const FindingsListPanel: FC<FindingsListPanelProps> = ({
    diffPreviewCollapsed,
    qualitySelection,
    setQualitySelection,
    findings,
    onClose,
    onApply,
    isStale,
}) => {
    const intl = useIntl();
    const [qualityExpandedDetails, setQualityExpandedDetails] = useState<Record<string, boolean>>({});
    const acceptButtonRef = useRef<HTMLButtonElement>(null);

    const handleToggleFindingDetail = useCallback((findingId: string) => {
        setQualityExpandedDetails(prev => ({
            ...prev,
            [findingId]: !prev[findingId],
        }));
    }, []);

    const styles = useQuickFixesDialogStyles();

    return (
        <div
            className={mergeClasses(
                styles.findingsListPanel,
                diffPreviewCollapsed ? styles.findingsListPanelCollapsed : styles.findingsListPanelExpanded
            )}
        >
            <div className={styles.panelHeader}>
                <h3 className={styles.panelTitle}>{intl.formatMessage(PlaygroundResources.qualityDrawerQuickFixesTitle)}</h3>
            </div>
            <List
                navigationMode="composite"
                className={styles.findingsList}
                selectionMode="multiselect"
                selectedItems={qualitySelection}
                onSelectionChange={(_, data) => {
                    const selectedIds = data.selectedItems as string[];
                    setQualitySelection(selectedIds);
                }}
            >
                {findings.map(finding => {
                    const detailExpanded = !!qualityExpandedDetails[finding.id];
                    const isSelected = qualitySelection.includes(finding.id);
                    return (
                        <FindingListItem
                            key={finding.id}
                            finding={finding}
                            selected={isSelected}
                            detailExpanded={detailExpanded}
                            handleToggleFindingDetail={handleToggleFindingDetail}
                        />
                    );
                })}
            </List>
            <div className={styles.panelFooter}>
                <Button appearance="secondary" className={styles.cancelButton} onClick={() => onClose()}>
                    {intl.formatMessage(SreAgentResources.cancel)}
                </Button>
                <Menu positioning={{ target: acceptButtonRef.current, position: 'below', align: 'end' }}>
                    <MenuTrigger disableButtonEnhancement>
                        <MenuButton ref={acceptButtonRef} appearance="primary" disabled={isStale || qualitySelection.length === 0}>
                            {intl.formatMessage(PlaygroundResources.qualityDrawerAcceptSelectedFixes)}
                        </MenuButton>
                    </MenuTrigger>
                    <MenuPopover>
                        <MenuList>
                            <MenuItem onClick={() => onApply(false)}>
                                {intl.formatMessage(PlaygroundResources.qualityDrawerAcceptSelectedFixesAndContinueEditing)}
                            </MenuItem>
                            <MenuItem onClick={() => onApply(true)}>
                                {intl.formatMessage(PlaygroundResources.qualityDrawerAcceptSelectedFixesAndSave)}
                            </MenuItem>
                        </MenuList>
                    </MenuPopover>
                </Menu>
            </div>
        </div>
    );
};

interface FindingListItemProps {
    finding: QualityFinding;
    selected?: boolean;
    detailExpanded: boolean;
    handleToggleFindingDetail: (findingId: string) => void;
}

const FindingListItem: FC<FindingListItemProps> = ({ finding, selected, detailExpanded, handleToggleFindingDetail }) => {
    const styles = useQuickFixesDialogStyles();

    return (
        <ListItem
            value={finding.id}
            className={mergeClasses(
                styles.listItemRoot,
                styles.listItem,
                styles.listItemBorderRadius,
                selected && styles.watcherFindingItemSelected
            )}
            aria-label={finding.title}
            checkmark={null}
        >
            <div role="gridcell" className={mergeClasses(styles.finding, styles.findingContent)}>
                <Badge
                    appearance="filled"
                    color={finding.expectedLift >= 15 ? 'danger' : finding.expectedLift >= 8 ? 'warning' : 'informative'}
                    size="small"
                />
                <span className={mergeClasses(styles.watcherFindingTitle, !detailExpanded && styles.findingTitleTruncated)}>
                    {finding.title}
                </span>
            </div>
            <div role="gridcell" className={mergeClasses(styles.expander, styles.expanderCell)}>
                <Button
                    appearance="secondary"
                    size="small"
                    onClick={e => {
                        handleToggleFindingDetail(finding.id);
                        e.stopPropagation();
                        e.preventDefault();
                    }}
                    icon={detailExpanded ? <ChevronUp12Regular /> : <ChevronDown12Regular />}
                />
            </div>
            {detailExpanded && (
                <div role="gridcell" className={mergeClasses(styles.description, styles.descriptionContent)}>
                    <span className={styles.watcherFindingRationale}>{finding.rationale}</span>
                    {finding.toolHint && (
                        <MessageBar className={mergeClasses(styles.watcherHint, styles.messageBarTransparent)} layout="singleline">
                            {finding.toolHint}
                        </MessageBar>
                    )}
                    {finding.safetyNote && (
                        <MessageBar className={mergeClasses(styles.watcherHint, styles.messageBarTransparent)} layout="singleline">
                            {finding.safetyNote}
                        </MessageBar>
                    )}
                </div>
            )}
        </ListItem>
    );
};

interface DiffPreviewPanelProps {
    diffPreviewCollapsed: boolean;
    setDiffPreviewCollapsed: (collapsed: boolean) => void;
    updatedYamlContent: string;
    currentYamlContent: string;
}

const DiffPreviewPanel: FC<DiffPreviewPanelProps> = ({
    diffPreviewCollapsed,
    setDiffPreviewCollapsed,
    updatedYamlContent,
    currentYamlContent,
}) => {
    const intl = useIntl();
    const styles = useQuickFixesDialogStyles();

    if (diffPreviewCollapsed)
        return (
            <div className={styles.diffPreviewPanelCollapsed}>
                <Button
                    appearance="subtle"
                    onClick={() => setDiffPreviewCollapsed(false)}
                    icon={<PanelRightExpandRegular />}
                    className={styles.flexNone}
                />
            </div>
        );
    return (
        <div className={styles.diffPreviewPanelExpanded}>
            <div className={styles.diffPreviewHeader}>
                <h3 className={styles.diffPreviewTitle}>{intl.formatMessage(PlaygroundResources.previewChanges)}</h3>
                <Button
                    appearance="subtle"
                    onClick={() => setDiffPreviewCollapsed(true)}
                    icon={<PanelRightContractRegular />}
                    className={styles.flexNone}
                />
            </div>
            <YamlDiffView yamlContent={updatedYamlContent} originalYamlContent={currentYamlContent} />
        </div>
    );
};
