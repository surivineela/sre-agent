import { Button, Field, Input, Link, Spinner } from '@fluentui/react-components';
import { FC, useContext } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../Common/AzPortalProxy/Providers/StartupInfoContext';
import { SreAgentFwLinks } from '../Common/Constants/FwLinks';
import { GrafanaDashboardResources, SreAgentResources } from '../Strings/SREAgentResources';
import { useGrafanaDashboard } from './Hooks/useGrafanaDashboard';
import { useGrafanaDashboardStyles } from './Styles/GrafanaDashboardStyles';

const GrafanaDashboard: FC = () => {
    const intl = useIntl();
    const styles = useGrafanaDashboardStyles();

    const environmentContext = useContext(EnvironmentContext);

    const {
        grafanaEndpoint,
        grafanaResourceName,
        isUpdating,
        newGrafanaResourceNameErrorMessage,
        agentLoaded,
        setIsDirty,
        onCreateGrafanaDashboard,
        setGrafanaResourceName,
    } = useGrafanaDashboard(environmentContext.resourceId, environmentContext.userInfo?.objectId);

    return (
        <div className={styles.container}>
            <div className={styles.grafanaLogo}>
                <img src="./Grafana.svg" alt="Grafana" style={{ height: 20 }} />
                <div className={styles.titleText}>{intl.formatMessage(SreAgentResources.grafana)}</div>
            </div>
            <div className={styles.rowCenterAlign}>
                {intl.formatMessage(GrafanaDashboardResources.instructions)}
                <Link href={SreAgentFwLinks.grafanaDashboardLearnMore} target="_blank">
                    {intl.formatMessage(SreAgentResources.getMoreInfo)}
                </Link>
            </div>
            {grafanaEndpoint ? (
                <div className={styles.grafanaUrlContainer}>
                    <Field
                        label={intl.formatMessage(GrafanaDashboardResources.grafanaDashboardUrl)}
                        orientation="horizontal"
                        className={styles.displayFieldLabel}
                    >
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
                                            setGrafanaResourceName(input.value);
                                        }}
                                        value={grafanaResourceName}
                                        placeholder={intl.formatMessage(GrafanaDashboardResources.grafanaResourceName)}
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
                                    {intl.formatMessage(SreAgentResources.apply)}
                                </Button>
                                <Button
                                    disabled={!agentLoaded || isUpdating || !grafanaResourceName}
                                    onClick={() => setGrafanaResourceName('')}
                                >
                                    {intl.formatMessage(SreAgentResources.discard)}
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
