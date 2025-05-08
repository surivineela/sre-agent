import { DetailsList, DetailsListLayoutMode, SelectionMode } from '@fluentui/react';
import {
    Accordion,
    AccordionHeader,
    AccordionItem,
    AccordionPanel,
    Button,
    Field,
    Input,
    Link,
    MessageBar,
    MessageBarBody,
    Spinner,
    Tooltip,
} from '@fluentui/react-components';
import { Info16Regular } from '@fluentui/react-icons';
import { FC, useContext } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../Common/AzPortalProxy/Providers/StartupInfoContext';
import { GrafanaDashboardResources, SreAgentResources } from '../Strings/SREAgentResources';
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
        setNewGrafanaResourceName,
        setIsDirty,
        onCreateGrafanaDashboard,
    } = useGrafanaDashboard(environmentContext.resourceId, environmentContext.userInfo?.objectId);

    return (
        <div className={styles.container}>
            {!hasRbacWritePermission && permissionsLoaded && agentLoaded && !grafanaEndpoint && (
                <MessageBar intent="warning" className={styles.messageBar}>
                    <MessageBarBody>{intl.formatMessage(GrafanaDashboardResources.insufficientPermissions)}</MessageBarBody>
                </MessageBar>
            )}
            <div className={styles.grafanaLogo}>
                <img src="./GrafanaBlueLogo.svg" alt="Grafana" style={{ height: 35 }} />
                <div className={styles.titleText}>{intl.formatMessage(SreAgentResources.azureManagedGrafana)}</div>
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
                        <Link href={grafanaEndpoint} target="_blank" className={styles.grafanaUrlLinkContainer}>
                            {grafanaEndpoint}
                        </Link>
                    </Field>
                </div>
            ) : (
                <>
                    {!agentLoaded || !permissionsLoaded ? (
                        <Spinner />
                    ) : (
                        <>
                            <Accordion collapsible>
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
                            <Field
                                label={intl.formatMessage(SreAgentResources.name)}
                                orientation="horizontal"
                                validationState={newGrafanaResourceNameErrorMessage ? 'error' : 'success'}
                                validationMessage={newGrafanaResourceNameErrorMessage}
                                required
                                className={styles.inputFieldLabel}
                            >
                                <Input
                                    onChange={(_, input) => {
                                        setIsDirty(true);
                                        setNewGrafanaResourceName(input.value);
                                    }}
                                    value={newGrafanaResourceName}
                                    placeholder={intl.formatMessage(GrafanaDashboardResources.grafanaResourceName)}
                                    disabled={isUpdating || !hasRbacWritePermission || isGrafanaUpdating}
                                    className={styles.inputTextField}
                                />
                            </Field>
                            <div className={styles.buttonRow}>
                                <Button
                                    disabled={
                                        !agentLoaded ||
                                        isUpdating ||
                                        !newGrafanaResourceName ||
                                        !!newGrafanaResourceNameErrorMessage ||
                                        !hasRbacWritePermission
                                    }
                                    onClick={onCreateGrafanaDashboard}
                                    appearance="primary"
                                >
                                    {intl.formatMessage(SreAgentResources.add)}
                                </Button>
                                <Button
                                    onClick={() => setNewGrafanaResourceName('')}
                                    disabled={isUpdating || !newGrafanaResourceName || !hasRbacWritePermission}
                                >
                                    {intl.formatMessage(SreAgentResources.cancel)}
                                </Button>
                            </div>
                        </>
                    )}
                </>
            )}
        </div>
    );
};

export default GrafanaDashboard;
