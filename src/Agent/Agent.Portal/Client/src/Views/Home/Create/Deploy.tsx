import { DeploymentTracking } from '../../../Common/Components/DeploymentTracking/DeploymentTracking';
import { TelemetrySource } from '../../../Common/Constants/Telemetry';

interface DeployProps {
    deploymentResourceId: string;
}

export const Deploy = ({ deploymentResourceId }: DeployProps) => {
    return <DeploymentTracking deploymentResourceId={deploymentResourceId} telemetrySource={TelemetrySource.SreAgentCreate} />;
};
