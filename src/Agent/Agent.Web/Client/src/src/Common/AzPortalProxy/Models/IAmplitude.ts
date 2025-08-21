export enum SpecialControlValue {
    SubmitForm = 'submitForm',
    DoAction = 'doAction',
    CustomerSuppliedData = 'customerSuppliedData',
    SensitiveData = 'sensitiveData',
}

type ControlTargetTypes = 'checkbox' | 'dropdown' | 'radioButton' | 'textbox' | 'button' | 'link' | 'accordion' | 'combobox';
type NavigationTargetTypes = 'menuItem' | 'tabBlade' | 'tab' | 'button' | 'link' | 'card' | 'image';
type OperationTargetTypes = 'create' | 'update' | 'delete' | 'refresh' | 'load';

type ControlActionVerb = 'changed' | 'clicked';
type NavigationActionVerb =
    | 'menuItem'
    | 'tabItem'
    | 'switchMenuItem'
    | 'openBlade'
    | 'openContextPane'
    | 'openDialog'
    | 'closeCurrentBlade'
    | 'resourceLink'
    | 'externalLink';
type OperationActionVerb =
    | 'validationFailed'
    | 'bladeDisposed'
    | 'deploy'
    | 'save'
    | 'update'
    | 'cancel'
    | 'upload'
    | 'refresh'
    | 'loaded'
    | 'start'
    | 'success'
    | 'failed';

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
