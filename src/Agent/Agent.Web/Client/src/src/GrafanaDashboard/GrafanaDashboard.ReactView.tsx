import { DetailsList, DetailsListLayoutMode, SelectionMode } from '@fluentui/react';
import {
    Accordion,
    AccordionHeader,
    AccordionItem,
    AccordionPanel,
    Button,
    Dropdown,
    Field,
    Input,
    Link,
    MessageBar,
    MessageBarBody,
    Option,
    Popover,
    PopoverSurface,
    PopoverTrigger,
    Spinner,
    Tooltip
} from '@fluentui/react-components';
import { Info16Regular } from '@fluentui/react-icons';
import { FC, useContext } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../Common/AzPortalProxy/Providers/StartupInfoContext';
import { GrafanaDashboardResources, SettingsTabResources, SreAgentResources } from '../Strings/SREAgentResources';
import { useGrafanaDashboard } from './Hooks/useGrafanaDashboard';
import { useGrafanaDashboardStyles } from './Styles/GrafanaDashboardStyles';

const GrafanaDashboard: FC = () => {
    const intl = useIntl();
    const styles = useGrafanaDashboardStyles();

    const environmentContext = useContext(EnvironmentContext);

    const {
        grafanaEndpoint,
        isUpdating,
        newGrafanaResourceNameErrorMessage,
        agentLoaded,
        newGrafanaResourceName,
        hasRbacWritePermission,
        permissionsLoaded,
        grafanaRbacColumns,
        grafanaRbacRoles,
        isGrafanaUpdating,
        existingGrafanaResources,
        isGrafanaPopoverOpen,
        selectedGrafanaResource,
        newAzureMonitorWorkspaceResourceNameErrorMessage,
        isAzureMonitorWorkspacePopoverOpen,
        newAzureMonitorWorkspaceResourceName,
        selectedAzureMonitorWorkspaceResource,
        existingAzureMonitorWorkspaceResources,
        locationOptions,
        selectedLocation,
        setSelectedLocation,
        setSelectedGrafanaResource,
        setSelectedAzureMonitorWorkspaceResource,
        setNewAzureMonitorWorkspaceResourceName,
        setIsAzureMonitorWorkspacePopoverOpen,
        onAddNewAzureMonitorWorkspaceName,
        onAzureMonitorWorkspaceOptionSelect,
        onGrafanaOptionSelect,
        setIsGrafanaPopoverOpen,
        onAddNewGrafanaResourceName,
        setNewGrafanaResourceName,
        setIsGrafanaDirty,
        setIsAzureMonitorWorkspaceDirty,
        onCreateGrafanaDashboard,
    } = useGrafanaDashboard(environmentContext.resourceId, environmentContext.userInfo?.objectId);

    const grafanaDashboardsUrl = grafanaEndpoint ? `${grafanaEndpoint.replace(/\/+$/, '')}/dashboards` : '';

    return (
        <div className={styles.container}>
            <h2 className={styles.titleGrafanaDashboardHeader}>{intl.formatMessage(SettingsTabResources.grafanaDashboard)}</h2>
            {!hasRbacWritePermission && permissionsLoaded && agentLoaded && !grafanaEndpoint && (
                <MessageBar intent="warning" className={styles.messageBar}>
                    <MessageBarBody>{intl.formatMessage(GrafanaDashboardResources.insufficientPermissions)}</MessageBarBody>
                </MessageBar>
            )}
            <div className={styles.grafanaLogo}>
                <img src="./GrafanaBlueLogo.svg" alt="Grafana" style={{ height: 35 }} />
                <h2 className={styles.titleText}>{intl.formatMessage(SreAgentResources.azureManagedGrafana)}</h2>
            </div>
            <div className={styles.rowCenterAlign}>{intl.formatMessage(GrafanaDashboardResources.description)}</div>
            {grafanaEndpoint ? (
                <div className={styles.grafanaUrlContainer}>
                    <Field
                        label={
                            <div className={styles.grafanaUrlLabelContainer}>
                                {intl.formatMessage(GrafanaDashboardResources.grafanaDashboardUrl)}
                                <Tooltip content={intl.formatMessage(GrafanaDashboardResources.tooltipContent)} relationship="label">
                                    <Info16Regular />
                                </Tooltip>
                            </div>
                        }
                        orientation="horizontal"
                        className={styles.displayFieldLabel}
                    >
                        <Link href={grafanaDashboardsUrl} target="_blank" className={styles.grafanaUrlLinkContainer}>
                            {grafanaDashboardsUrl}
                        </Link>
                    </Field>
                </div>
            ) : (
                <>
                    {!agentLoaded || !permissionsLoaded ? (
                        <Spinner />
                    ) : (
                        <>
                            <Accordion collapsible className={styles.roleGridStyle}>
                                <AccordionItem value="1">
                                    <AccordionHeader>{intl.formatMessage(GrafanaDashboardResources.roleAssignments)}</AccordionHeader>
                                    <AccordionPanel>
                                        <DetailsList
                                            columns={grafanaRbacColumns}
                                            items={grafanaRbacRoles}
                                            layoutMode={DetailsListLayoutMode.justified}
                                            selectionMode={SelectionMode.none}
                                        />
                                    </AccordionPanel>
                                </AccordionItem>
                            </Accordion>
                            <div className={styles.formContainer}>
                                <Field
                                    label={intl.formatMessage(GrafanaDashboardResources.region)}
                                    orientation="horizontal"
                                    required
                                    className={styles.inputFieldLabel}
                                >
                                    <Dropdown
                                        value={selectedLocation}
                                        selectedOptions={selectedLocation ? [selectedLocation] : []}
                                        onOptionSelect={(_, data) => {
                                            setSelectedLocation(data.optionValue || '');
                                        }}
                                        disabled={isUpdating || !hasRbacWritePermission || isGrafanaUpdating}
                                        placeholder={intl.formatMessage(GrafanaDashboardResources.selectRegion)}
                                        className={styles.dropdownFieldStyle}
                                    >
                                        {locationOptions.map(option => (
                                            <Option key={option} value={option}>
                                                {option}
                                            </Option>
                                        ))}
                                    </Dropdown>
                                </Field>
                                <Field
                                    label={intl.formatMessage(GrafanaDashboardResources.grafanaResourceName)}
                                    orientation="horizontal"
                                    required
                                    className={styles.inputFieldLabel}
                                >
                                    <Dropdown
                                        value={
                                            selectedGrafanaResource.id === 'new'
                                                ? selectedGrafanaResource.name +
                                                  intl.formatMessage(GrafanaDashboardResources.newValueDisplay)
                                                : selectedGrafanaResource.name
                                        }
                                        selectedOptions={[selectedGrafanaResource.name]}
                                        onOptionSelect={onGrafanaOptionSelect}
                                        disabled={isUpdating || !hasRbacWritePermission || isGrafanaUpdating}
                                        className={styles.dropdownFieldStyle}
                                    >
                                        {existingGrafanaResources.map(option => (
                                            <Option key={option.id} value={option.name}>
                                                {option.id === 'new'
                                                    ? option.name + intl.formatMessage(GrafanaDashboardResources.newValueDisplay)
                                                    : option.name}
                                            </Option>
                                        ))}
                                    </Dropdown>
                                    <Popover open={isGrafanaPopoverOpen} withArrow trapFocus>
                                        <PopoverTrigger>
                                            <Link
                                                onClick={() => setIsGrafanaPopoverOpen(true)}
                                                className={styles.popoverLink}
                                                disabled={isUpdating || !hasRbacWritePermission || isGrafanaUpdating}
                                            >
                                                {intl.formatMessage(SreAgentResources.new)}
                                            </Link>
                                        </PopoverTrigger>
                                        <PopoverSurface tabIndex={-1} className={styles.popoverContainer}>
                                            <h3>{intl.formatMessage(GrafanaDashboardResources.createNew)}</h3>
                                            <Field
                                                label={intl.formatMessage(GrafanaDashboardResources.grafanaResourceName)}
                                                orientation="horizontal"
                                                validationState={newGrafanaResourceNameErrorMessage ? 'error' : 'success'}
                                                validationMessage={newGrafanaResourceNameErrorMessage}
                                                required
                                                className={styles.inputFieldLabel}
                                            >
                                                <Input
                                                    onChange={(_, input) => {
                                                        setIsGrafanaDirty(true);
                                                        setNewGrafanaResourceName(input.value);
                                                    }}
                                                    value={newGrafanaResourceName}
                                                    placeholder={intl.formatMessage(GrafanaDashboardResources.enterResourceName)}
                                                    disabled={isUpdating || !hasRbacWritePermission || isGrafanaUpdating}
                                                    className={styles.inputTextField}
                                                />
                                            </Field>
                                            <div className={styles.popoverButtonRow}>
                                                <Button
                                                    disabled={!newGrafanaResourceName || !!newGrafanaResourceNameErrorMessage}
                                                    onClick={onAddNewGrafanaResourceName}
                                                    appearance="primary"
                                                >
                                                    {intl.formatMessage(SreAgentResources.add)}
                                                </Button>
                                                <Button
                                                    onClick={() => {
                                                        setIsGrafanaDirty(false);
                                                        setNewGrafanaResourceName('');
                                                        setIsGrafanaPopoverOpen(false);
                                                    }}
                                                >
                                                    {intl.formatMessage(SreAgentResources.cancel)}
                                                </Button>
                                            </div>
                                        </PopoverSurface>
                                    </Popover>
                                </Field>
                                <Field
                                    label={intl.formatMessage(GrafanaDashboardResources.azureMonitorWorkspaceResourceName)}
                                    orientation="horizontal"
                                    required
                                    className={styles.inputFieldLabel}
                                >
                                    <Dropdown
                                        value={
                                            selectedAzureMonitorWorkspaceResource.id === 'new'
                                                ? `${selectedAzureMonitorWorkspaceResource.name} (new)`
                                                : selectedAzureMonitorWorkspaceResource.name
                                        }
                                        selectedOptions={[selectedAzureMonitorWorkspaceResource.name]}
                                        onOptionSelect={onAzureMonitorWorkspaceOptionSelect}
                                        disabled={isUpdating || !hasRbacWritePermission || isGrafanaUpdating}
                                        className={styles.dropdownFieldStyle}
                                    >
                                        {existingAzureMonitorWorkspaceResources.map(option => (
                                            <Option key={option.id} value={option.name}>
                                                {option.id === 'new' ? `${option.name} (new)` : option.name}
                                            </Option>
                                        ))}
                                    </Dropdown>
                                    <Popover open={isAzureMonitorWorkspacePopoverOpen} withArrow trapFocus>
                                        <PopoverTrigger>
                                            <Link
                                                onClick={() => setIsAzureMonitorWorkspacePopoverOpen(true)}
                                                className={styles.popoverLink}
                                                disabled={isUpdating || !hasRbacWritePermission || isGrafanaUpdating}
                                            >
                                                {intl.formatMessage(SreAgentResources.new)}
                                            </Link>
                                        </PopoverTrigger>
                                        <PopoverSurface tabIndex={-1} className={styles.popoverContainer}>
                                            <h3>{intl.formatMessage(GrafanaDashboardResources.createNew)}</h3>
                                            <Field
                                                label={intl.formatMessage(GrafanaDashboardResources.azureMonitorWorkspaceResourceName)}
                                                orientation="horizontal"
                                                validationState={newAzureMonitorWorkspaceResourceNameErrorMessage ? 'error' : 'success'}
                                                validationMessage={newAzureMonitorWorkspaceResourceNameErrorMessage}
                                                required
                                                className={styles.inputFieldLabel}
                                            >
                                                <Input
                                                    onChange={(_, input) => {
                                                        setIsAzureMonitorWorkspaceDirty(true);
                                                        setNewAzureMonitorWorkspaceResourceName(input.value);
                                                    }}
                                                    value={newAzureMonitorWorkspaceResourceName}
                                                    placeholder={intl.formatMessage(GrafanaDashboardResources.enterResourceName)}
                                                    disabled={isUpdating || !hasRbacWritePermission || isGrafanaUpdating}
                                                    className={styles.inputTextField}
                                                />
                                            </Field>
                                            <div className={styles.popoverButtonRow}>
                                                <Button
                                                    disabled={
                                                        !newAzureMonitorWorkspaceResourceName ||
                                                        !!newAzureMonitorWorkspaceResourceNameErrorMessage
                                                    }
                                                    onClick={onAddNewAzureMonitorWorkspaceName}
                                                    appearance="primary"
                                                >
                                                    {intl.formatMessage(SreAgentResources.add)}
                                                </Button>
                                                <Button
                                                    onClick={() => {
                                                        setIsAzureMonitorWorkspaceDirty(false);
                                                        setNewAzureMonitorWorkspaceResourceName('');
                                                        setIsAzureMonitorWorkspacePopoverOpen(false);
                                                    }}
                                                >
                                                    {intl.formatMessage(SreAgentResources.cancel)}
                                                </Button>
                                            </div>
                                        </PopoverSurface>
                                    </Popover>
                                </Field>
                                <div className={styles.buttonRow}>
                                    <Button
                                        disabled={
                                            !agentLoaded ||
                                            isUpdating ||
                                            !selectedGrafanaResource.name ||
                                            !selectedAzureMonitorWorkspaceResource.name ||
                                            !hasRbacWritePermission
                                        }
                                        onClick={onCreateGrafanaDashboard}
                                        appearance="primary"
                                    >
                                        {intl.formatMessage(SreAgentResources.add)}
                                    </Button>
                                    <Button
                                        onClick={() => {
                                            setSelectedAzureMonitorWorkspaceResource({ id: '', name: '' });
                                            setSelectedGrafanaResource({ id: '', name: '' });
                                            setSelectedLocation('');
                                        }}
                                        disabled={
                                            isUpdating ||
                                            !hasRbacWritePermission ||
                                            (!selectedGrafanaResource.name &&
                                                !selectedAzureMonitorWorkspaceResource.name &&
                                                !selectedLocation)
                                        }
                                    >
                                        {intl.formatMessage(SreAgentResources.cancel)}
                                    </Button>
                                </div>
                            </div>
                        </>
                    )}
                </>
            )}
        </div>
    );
};

export default GrafanaDashboard;
