import { useCallback, useContext } from 'react';
import { NavigateOptions, useLocation, useNavigate } from 'react-router-dom';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { PrimaryNavItemValues, SecondaryNavItemValues } from '../Contracts/SreAgentSpace';
import { constructNavItemId } from '../Utilities';

interface NavigationInput {
    primaryNavItemValue: PrimaryNavItemValues;
    secondaryNavItemValue?: SecondaryNavItemValues;
    threadId?: string;
    grandChildKey?: string;
    options?: NavigateOptions;
}

export const useAgentSiteNavigate = () => {
    const location = useLocation();
    const reactRouterNavigate = useNavigate();

    const { logAmplitudeNavigationEvent } = useContext(AzPortalContext);

    const navigate = useCallback(
        (input: NavigationInput) => {
            const navItemId = constructNavItemId(
                input.primaryNavItemValue,
                input.secondaryNavItemValue,
                input.threadId,
                input.grandChildKey
            );
            const pathname = `/views/${navItemId}`;

            logAmplitudeNavigationEvent({
                targetType: 'tab',
                targetAction: 'tabItem',
                targetName: pathname,
                targetFriendlyName: pathname,
            });

            reactRouterNavigate({ ...location, pathname }, input.options);
        },
        [location, reactRouterNavigate, logAmplitudeNavigationEvent]
    );

    return navigate;
};
