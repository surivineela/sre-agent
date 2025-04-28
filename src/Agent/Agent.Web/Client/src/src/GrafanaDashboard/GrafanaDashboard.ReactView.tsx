import { Button, Field, Input, Link, Spinner } from '@fluentui/react-components';
import { FC, useContext } from 'react';
import { EnvironmentContext } from '../Common/AzPortalProxy/Providers/StartupInfoContext';
import { SreAgentFwLinks } from '../Common/Constants/FwLinks';
import { GrafanaDashboardResources, SreAgentResources } from '../Strings/SREResources.resjson';
import { useGrafanaDashboard } from './Hooks/useGrafanaDashboard';
import { useGrafanaDashboardStyles } from './Styles/GrafanaDashboardStyles';

const GrafanaDashboard: FC = () => {
    const styles = useGrafanaDashboardStyles();

    const environmentContext = useContext(EnvironmentContext);

    const {
        grafanaEndpoint,
        grafanaResourceName,
        isUpdating,
        newGrafanaResourceNameErrorMessage,
        agentLoaded,
        onCreateGrafanaDashboard,
        setGrafanaResourceName,
    } = useGrafanaDashboard(environmentContext.resourceId, environmentContext.userInfo?.objectId);

    return (
        <div className={styles.container}>
            <div className={styles.grafanaLogo}>
                <img src="./Grafana.svg" alt="Grafana" style={{ height: 20 }} />
                <div className={styles.titleText}>{SreAgentResources.grafana}</div>
            </div>
            <div className={styles.rowCenterAlign}>
                {GrafanaDashboardResources.instructions}
                <Link href={SreAgentFwLinks.grafanaDashboardLearnMore} target="_blank">
                    {SreAgentResources.getMoreInfo}
                </Link>
            </div>
            {!!grafanaEndpoint ? (
                <div className={styles.grafanaUrlContainer}>
                    <Field label={GrafanaDashboardResources.grafanaDashboardUrl} orientation="horizontal" className={styles.fieldLabel}>
                        <Link href={grafanaEndpoint} target="_blank">
                            {grafanaEndpoint}
                        </Link>
                    </Field>
                </div>
            ) : (
                <>
                    {!agentLoaded ? (
                        <Spinner />
                    ) : (
                        <>
                            <div className={styles.apiKeyRow}>
                                <Field
                                    label={SreAgentResources.name}
                                    orientation="horizontal"
                                    validationState={newGrafanaResourceNameErrorMessage ? 'error' : 'success'}
                                    validationMessage={newGrafanaResourceNameErrorMessage}
                                    required
                                    className={styles.fieldLabel}
                                >
                                    <Input
                                        onChange={(_, input) => setGrafanaResourceName(input.value)}
                                        placeholder="Grafana resource name"
                                        disabled={isUpdating}
                                        className={styles.inputTextField}
                                    />
                                </Field>
                            </div>
                            <div className={styles.buttonRow}>
                                <Button
                                    disabled={!agentLoaded || isUpdating || !grafanaResourceName || !!newGrafanaResourceNameErrorMessage}
                                    onClick={onCreateGrafanaDashboard}
                                    appearance="primary"
                                >
                                    {SreAgentResources.apply}
                                </Button>
                                <Button
                                    disabled={!agentLoaded || isUpdating || !grafanaResourceName}
                                    onClick={() => setGrafanaResourceName(undefined)}
                                >
                                    {SreAgentResources.cancel}
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
