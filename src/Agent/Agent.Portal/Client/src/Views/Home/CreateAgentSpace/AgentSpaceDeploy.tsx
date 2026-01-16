import { useCallback } from 'react';
import { useIntl } from 'react-intl';
import { DeploymentTracking } from '../../../Common/Components/DeploymentTracking/DeploymentTracking';
import { TelemetrySource } from '../../../Common/Constants/Telemetry';
import { DeployResources } from '../../../Strings/Resources';

interface AgentSpaceDeployProps {
    deploymentResourceId: string;
}

export const AgentSpaceDeploy = ({ deploymentResourceId }: AgentSpaceDeployProps) => {
    const intl = useIntl();

    const renderErrorDetails = useCallback(
        (errorMessage: string) => (
            <>
                {errorMessage}
                {/* While Agent Spaces are limited to 1P and allowlisted, show an additional helpful message */}
                {errorMessage.toLowerCase().includes('not allowed for tenant') && (
                    <>
                        <br />
                        <br />
                        {intl.formatMessage(DeployResources.tenantRestrictionError)}
                    </>
                )}
            </>
        ),
        [intl]
    );

    return (
        <DeploymentTracking
            deploymentResourceId={deploymentResourceId}
            telemetrySource={TelemetrySource.AgentSpaceCreate}
            renderErrorDetails={renderErrorDetails}
        />
    );
};
