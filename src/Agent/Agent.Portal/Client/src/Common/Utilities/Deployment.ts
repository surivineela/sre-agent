import { ProvisioningState } from '../Contracts/Deployment';

/**
 * Checks if a deployment provisioning state is in a terminal state
 * @param state The provisioning state to check
 * @returns true if the state is terminal (Succeeded, Failed, or Canceled)
 */
export function isDeploymentStateTerminal(state?: ProvisioningState): boolean {
    if (!state) {
        return false;
    }
    return state === 'Succeeded' || state === 'Failed' || state === 'Canceled';
}
