export type LogType = 'Control' | 'Navigation' | 'Operation';

export enum SpecialControlValue {
    SubmitForm = 'submitForm',
    DoAction = 'doAction',
    CustomerSuppliedData = 'customerSuppliedData',
    SensitiveData = 'sensitiveData',
}

export type ControlTargetTypes =
    | 'checkbox'
    | 'dropdown'
    | 'radioButton'
    | 'textbox'
    | 'button'
    | 'link'
    | 'accordion'
    | 'combobox'
    | 'toggle';
export type NavigationTargetTypes = 'menuItem' | 'tabBlade' | 'tab' | 'button' | 'link' | 'card' | 'image';
export type OperationTargetTypes = 'create' | 'update' | 'delete' | 'refresh' | 'load';

export type ControlActionVerb = 'changed' | 'clicked' | 'blurred';
export type NavigationActionVerb =
    | 'menuItem'
    | 'tabItem'
    | 'switchMenuItem'
    | 'openBlade'
    | 'openContextPane'
    | 'openDialog'
    | 'closeCurrentBlade'
    | 'resourceLink'
    | 'externalLink';
export type OperationActionVerb =
    | 'validationFailed'
    | 'bladeDisposed'
    | 'deploy'
    | 'save'
    | 'update'
    | 'cancel'
    | 'upload'
    | 'refresh'
    | 'loaded';

export enum ProductName {
    Unknown = 'Unknown Product',
    SreAgent = 'SRE Agent',
    SreAgentSpace = 'SRE Agent Space',
}

type AmplitudeResourceMetadata = {
    productName: ProductName;
    resourceId: string;
    subscriptionId: string;
    resourceGroup: string;
    resourceName: string;
};

/** Amplitude metadata compiled by `AmplitudeContextProvider` */
export type IncompleteAmplitudeData = AmplitudeResourceMetadata & {
    loggedTime?: never;
    logType?: never;
    /** Currently merged into `metadata` within `logAmplitudeEvent` core util (to avoid updating ingestion) */
    isInternal: boolean;
    /** Currently merged into `metadata` within `logAmplitudeEvent` core util */
    isInternalTenant: boolean;
    metadata?: Record<string, unknown>;
};

/** Complete Amplitude event with data finalized within `logAmplitudeEvent` */
export type CompleteAmplitudeData = Omit<IncompleteAmplitudeData, 'loggedTime' | 'logType'> &
    AmplitudeEvent & {
        loggedTime: string;
        logType: LogType;
    };

type AmplitudeBaseEvent<T, V> = {
    /** The type of the object that the event is for */
    targetType: T;

    /** The verb describing what the object is doing */
    targetAction: V;

    /**
     * The unfriendly name of the target type. Such as variable name of a control
     *
     * Example: if `targetType` is "radioButton", `targetName` might be "ingressRadioButton"
     */
    targetName: string;

    /**
     * The unfriendly name of the value of the `targetName`.
     *
     * Example: if `targetName` is "ingressRadioButton", `targetFriendlyName` might be "Ingress"
     */
    targetFriendlyName: string;
};

export type AmplitudeOperationEvent = AmplitudeBaseEvent<OperationTargetTypes, OperationActionVerb>;
export type AmplitudeControlEvent = AmplitudeBaseEvent<ControlTargetTypes, ControlActionVerb> & {
    /**
     * The unfriendly name of the value of the `targetName`.
     * Such as value of a control. Do not log customer information
     *
     * Example: if `targetName` is "ingressRadioButton", `valueObjectName` might be "limited"
     *
     * Example 2: if `targetName` is "nameTextBox", `valueObjectName` might be "customerSuppliedData"
     */
    valueObjectName: string | SpecialControlValue;

    /**
     * The friendly name of the value of the `valueObjectName`.
     * Such as visual value of a control. Do not log customer information
     *
     * Example: if `valueObjectName` is "limited", `valueObjectFriendlyName` might be "Limited to Container Apps"
     *
     * Example 2: if `valueObjectName` is "customerSuppliedData", `valueObjectName` would also be "customerSuppliedData"
     */
    valueObjectFriendlyName: string | SpecialControlValue;
};
export type AmplitudeNavigationEvent = AmplitudeBaseEvent<NavigationTargetTypes, NavigationActionVerb>;
export type AmplitudeEvent = AmplitudeOperationEvent | AmplitudeControlEvent | AmplitudeNavigationEvent;
