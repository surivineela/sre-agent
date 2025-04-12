import { ArmResourceDescriptor } from '../../../Common/Helpers/ResourceDescriptors';
import { useMemo } from 'react';

export function useAgentDetails(resourceId: string) {
  const { resourceGroup, subscription, resourceName } = useMemo(() => new ArmResourceDescriptor(resourceId), [resourceId]);

  return {
    resourceGroup,
    subscription,
    resourceName,
    region: '',
  };
}
